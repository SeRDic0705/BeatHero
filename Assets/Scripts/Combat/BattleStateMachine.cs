using System.Collections;
using System.Collections.Generic;
using BeatHero.Audio;
using BeatHero.Core;
using BeatHero.Data;
using BeatHero.Player;
using UnityEngine;

namespace BeatHero.Combat
{
    // Call & Response 전투 루프 총괄.
    // 의존: Conductor, GridManager, PatternPlayer, PlayerController, PlayerConfig, InputReader
    public class BattleStateMachine : MonoBehaviour
    {
        private const int   BEATS_PER_PHASE      = 4;
        private const float CHARGE_MULT_PER_BEAT = 0.5f;

        [Header("Input Timing")]
        [SerializeField] private float _judgmentWindowSec = 0.021f; // 판정구간 반폭 (비트 전후 각각)
        [SerializeField] private float _failZoneSec       = 0.021f; // 판정 실패구간 반폭 (판정구간 바깥)
        [SerializeField] private float _tapGraceSec       = 0.15f;  // 탭 릴리즈 유예 (새 프레스 한정)

        [Header("Audio")]
        [SerializeField] private AudioClip _callBeatSfx;
        [SerializeField] private bool _pitchSweepOnTransition = true;

        [Header("Dependencies")]
        [SerializeField] private Conductor                  _conductor;
        [SerializeField] private GridManager                _grid;
        [SerializeField] private PatternPlayer              _patternPlayer;
        private InputReader _input => InputReader.Instance;
        [SerializeField] private PlayerController           _player;
        [SerializeField] private PlayerAnimationController  _playerAnim;
        [SerializeField] private PlayerConfig               _playerConfig;

        public event System.Action<MonsterData> OnBattleStarted;
        public event System.Action<int, int> OnMonsterHpChanged; // (current, max)
        public event System.Action<double> OnBeatUnitFired; // beatDurationSec: 현재 BeatUnit의 재생 길이(초)
        public event System.Action OnEffectiveBeatFired; // ResponsePhase + gridEffectShape != null 인 비트만
        public event System.Action OnMonsterHit;         // 플레이어 공격이 몬스터에 실제로 데미지를 입혔을 때
        public event System.Action<CellEffectFeedback, Vector3> OnMonsterCellEffectFired;
        public event System.Action OnBossPhaseChanged; // 전환 완충 마디 박자마다 발동

        public MonsterData CurrentMonster => _monster;
        private MonsterData     _monster;
        private CombatPhaseData _phase;
        private int             _monsterHp;

        private List<ActiveHazard> _hazards = new();

        private enum State { Idle, CallPhase, ResponsePhase, PhaseTransition, BattleEnd }
        private State _state = State.Idle;
        private bool  _phraseRunning;

        private bool   _paused;
        private double _pauseDelta;

        // 페이즈 전환 대기 — 다음 프레이즈 시작 시각에 맞춰 SwitchPhaseAt 호출
        private bool      _pendingPhaseSwitch;
        private AudioClip _pendingBgm;
        private float     _pendingBpm;

        // 입력 윈도우
        private bool    _inputWindowOpen;
        private bool    _attackHeld;
        private int     _chargeBeats;
        private float   _chargeDamageMultiplier = 1f;
        private bool    _tileWasDangerAtWindowOpen;
        private Vector2 _pendingMove;
        private bool    _hasPendingMove;

        // 비트 전 선행 입력 버퍼 + 소진 플래그 (타임스탬프는 DSP 기준 — Time.time 미사용)
        private double  _lastMoveTime          = -1.0;
        private Vector2 _lastMoveDir;
        private double  _lastAttackPressTime   = -1.0;
        private double  _lastAttackReleaseTime = -1.0;
        private double  _tapGraceEndTime       = -1.0;
        private bool    _attackKeyDown;      // 공격 키가 물리적으로 눌린 상태
        private bool    _beatInputConsumed;  // true면 이번 비트 추가 입력 무시

        private void Awake()
        {
            _conductor.OnBeat    += OnBeat;
            _conductor.OnPaused  += OnConductorPaused;
            _conductor.OnResumed += OnConductorResumed;
            _player.OnDeath      += OnPlayerDeath;
        }

        private void Start()
        {
            _input.OnMoveInput      += OnMoveInput;
            _input.OnAttackPressed  += OnAttackPressed;
            _input.OnAttackReleased += OnAttackReleased;
        }

        private void OnDestroy()
        {
            _conductor.OnBeat    -= OnBeat;
            _conductor.OnPaused  -= OnConductorPaused;
            _conductor.OnResumed -= OnConductorResumed;
            _player.OnDeath      -= OnPlayerDeath;
            if (_input != null)
            {
                _input.OnMoveInput      -= OnMoveInput;
                _input.OnAttackPressed  -= OnAttackPressed;
                _input.OnAttackReleased -= OnAttackReleased;
            }
        }

        // 1단계: 현재 층에 등장할 몬스터·bpm·bgm·패턴을 미리 세팅/로드. 클럭은 아직 시작 안 함.
        public void SetFloorData(MonsterData monster)
        {
            StopAllCoroutines();
            _phraseRunning = false;
            _monster   = monster;
            _monsterHp = monster.maxHp;
            OnBattleStarted?.Invoke(_monster);
            OnMonsterHpChanged?.Invoke(_monsterHp, _monster.maxHp);
            _hazards.Clear();
            _grid.Initialize(monster.gridType);
            _player.Initialize(_playerConfig.maxHp);

            _pendingPhaseSwitch = false;
            _phase = monster.GetCurrentPhase(1f);
            _conductor.PrepareSong(_phase.bgm, _phase.bpm);
            // 콜 효과음 프리로드 — 미로드 시 첫 PlayScheduled에서 로딩 지연으로 첫 박이 밀린다.
            if (_callBeatSfx != null && _callBeatSfx.loadState != AudioDataLoadState.Loaded)
                _callBeatSfx.LoadAudioData();
            SelectRandomPattern();
            _player.transform.position = _grid.GetTileWorldPosition(_grid.PlayerPosition);
            _state = State.Idle;
        }

        // 2단계: 층 시작. Conductor가 dspTime을 시작시간으로 저장하고 1마디 뒤 beat0에서
        // BGM 재생과 CallPhase(OnBeat→HandlePhrasePair)가 동시에 시작된다.
        public void StartFloor()
        {
            _conductor.StartFloor();
            _state = State.CallPhase;
        }

        private void OnBeat(int beatIndex)
        {
            // 프레이즈 시작 비트에만 반응 — 서브비트 처리는 코루틴 내부에서
            if (_state == State.CallPhase && !_phraseRunning)
                StartCoroutine(HandlePhrasePair(_conductor.GetBeatDspTime(beatIndex)));
        }

        // ── 프레이즈 사이클 ─────────────────────────────────────
        private IEnumerator HandlePhrasePair(double phraseStartDsp)
        {
            _phraseRunning = true;
            double phraseDurationSec = _conductor.SecPerBeat * BEATS_PER_PHASE;

            yield return StartCoroutine(HandleCallPhrase(phraseStartDsp));

            _state = State.ResponsePhase;
            double responseDsp = phraseStartDsp + phraseDurationSec;
            yield return StartCoroutine(HandleResponsePhrase(responseDsp));

            TransitionToNextPattern();
            _phraseRunning = false;

            double nextPhraseStart = responseDsp + phraseDurationSec;
            if (_pendingPhaseSwitch)
            {
                // 전환 완충 마디 삽입: 신 BPM으로 4박 연출 후 CallPhase + 신 BGM 동시 시작
                _conductor.SwitchPhaseWithTransition(nextPhraseStart, _pendingBgm, _pendingBpm, BEATS_PER_PHASE, _pitchSweepOnTransition);
                _pendingPhaseSwitch = false;
                StartCoroutine(TransitionMeasureRoutine(nextPhraseStart));
            }
            else if (_state == State.CallPhase)
            {
                StartCoroutine(HandlePhrasePair(nextPhraseStart));
            }
        }

        // ── CallPhase ──────────────────────────────────────────
        private IEnumerator HandleCallPhrase(double phraseStartDsp)
        {
            _grid.SetResponsePhase(false);
            double secPerUnit = _conductor.SecPerBeat / PatternPlayer.UNITS_PER_BEAT;
            int unitOffset = 0;

            foreach (var bu in _patternPlayer.CurrentPattern.beatUnits)
            {
                double noteStartDsp = phraseStartDsp + secPerUnit * unitOffset;

                // WaitUntil 전에 SFX 예약 — 리드타임 최대화로 DSP 정확도 확보
                // gridEffectShape == null이면 빈 비트이므로 효과음 스킵
                if (bu.gridEffectShape != null)
                    AudioManager.Instance?.PlaySFXScheduled(_callBeatSfx, noteStartDsp);

                // _pauseDelta = 누적 일시정지 시간. noteStartDsp는 원래 절대시각이므로
                // noteStartDsp + _pauseDelta = 프레이즈 경계 포함 모든 일시정지 반영한 목표 시각.
                yield return new WaitUntil(() => !_paused && AudioSettings.dspTime >= noteStartDsp + _pauseDelta);

                OnBeatUnitFired?.Invoke(secPerUnit * (int)bu.noteLength);
                _grid.ShowShape(bu.gridEffectShape);

                unitOffset += (int)bu.noteLength;
            }
        }

        // ── ResponsePhase ──────────────────────────────────────
        private IEnumerator HandleResponsePhrase(double phraseStartDsp)
        {
            // CallPhase 마지막 박자를 1박 동안 표시 후 제거 (즉시 ClearShape하면 마지막 장판이 1프레임만 보임)
            yield return new WaitUntil(() => !_paused && AudioSettings.dspTime >= phraseStartDsp + _pauseDelta);
            _grid.SetResponsePhase(true);
            _grid.ClearShape();
            double secPerUnit = _conductor.SecPerBeat / PatternPlayer.UNITS_PER_BEAT;
            int unitOffset = 0;

            foreach (var bu in _patternPlayer.CurrentPattern.beatUnits)
            {
                double noteStartDsp = phraseStartDsp + secPerUnit * unitOffset;
                yield return new WaitUntil(() => !_paused && AudioSettings.dspTime >= noteStartDsp + _pauseDelta);

                OnBeatUnitFired?.Invoke(secPerUnit * (int)bu.noteLength);
                if (bu.gridEffectShape != null) OnEffectiveBeatFired?.Invoke();
                _grid.UpdateDangerMap(bu.gridEffectShape); // 타일 색상 변경 없이 판정맵만 갱신
                PlayShapeFeedbacks(bu.gridEffectShape);

                double beatTime     = noteStartDsp;
                double preJudgStart = beatTime - _judgmentWindowSec;
                double preFailStart = preJudgStart - _failZoneSec;

                if (_attackHeld && !_attackKeyDown)
                {
                    bool inPreBuffer = _lastAttackReleaseTime >= preJudgStart;
                    bool inTapGrace  = _tapGraceEndTime > 0f && _lastAttackReleaseTime <= _tapGraceEndTime;
                    if (inPreBuffer || inTapGrace) FireAttack();
                    else CancelCharge();
                    _lastAttackReleaseTime = -1f;
                    _tapGraceEndTime       = -1f;
                }

                bool preMoveJudge   = _lastMoveTime >= preJudgStart;
                bool preMovesFail   = !preMoveJudge && _lastMoveTime >= preFailStart;
                bool preAttackJudge = _lastAttackPressTime >= preJudgStart;
                bool preAttackFail  = !preAttackJudge && _lastAttackPressTime >= preFailStart;

                bool continuingCharge = _attackHeld;

                _beatInputConsumed = preMoveJudge || preMovesFail || preAttackJudge || preAttackFail;
                _hasPendingMove    = preMoveJudge;
                _pendingMove       = preMoveJudge ? _lastMoveDir : Vector2.zero;

                if (preAttackJudge)
                {
                    if (_attackKeyDown && _player.Mana > 0) { _attackHeld = true; _playerAnim?.SetChargeStage(1); }
                    else if (!_attackKeyDown) FireAttack();
                }

                _lastMoveTime        = -1.0;
                _lastAttackPressTime = -1.0;

                _tileWasDangerAtWindowOpen = _grid.GetDangerAt(_grid.PlayerPosition) != null;
                _inputWindowOpen = true;

                // 판정 윈도우 닫힘 = 비트 + 판정구간 반폭 (DSP 절대시각 기준)
                double windowCloseDsp = noteStartDsp + _judgmentWindowSec;
                yield return new WaitUntil(() => !_paused && AudioSettings.dspTime >= windowCloseDsp + _pauseDelta);
                _inputWindowOpen   = false;
                _beatInputConsumed = false;

                if (_attackHeld && !_attackKeyDown)
                    FireAttack();
                else if (!continuingCharge && _attackHeld && _attackKeyDown)
                    _tapGraceEndTime = AudioSettings.dspTime + _tapGraceSec;
                else if (continuingCharge && _attackHeld && _attackKeyDown)
                {
                    if (_player.SpendMana(1))
                    {
                        _chargeDamageMultiplier += CHARGE_MULT_PER_BEAT;
                        _chargeBeats++;
                        _playerAnim?.SetChargeStage(Mathf.Min(_chargeBeats, 3) + 1);
                    }
                    _tapGraceEndTime = -1f;
                }

                if (_hasPendingMove) ProcessMovement(_pendingMove);
                JudgeTile();
                _grid.SetHazards(_hazards);

                unitOffset += (int)bu.noteLength;
            }

            // 장애물 틱은 프레이즈 단위 — 음표 수와 무관
            TickHazards();
            _grid.SetHazards(_hazards);
            // 마지막 그리드는 다음 페이즈 첫 ShowShape가 덮을 때까지 유지(깜빡임 방지).
            // 전투 종료 시점의 정리는 EndBattle에서 처리.
        }

        private void ProcessMovement(Vector2 raw)
        {
            var delta = Vector2Int.zero;
            if (Mathf.Abs(raw.x) > Mathf.Abs(raw.y))
                delta.x = raw.x > 0 ? 1 : -1;
            else
                delta.y = raw.y > 0 ? 1 : -1;

            bool moved = _grid.TryMovePlayer(delta);
            if (moved)
            {
                _player.transform.position = _grid.GetTileWorldPosition(_grid.PlayerPosition);
                _player.AddMana(_tileWasDangerAtWindowOpen ? 2 : 1);
            }

            // 이동 시 차지 취소 (이동과 공격 배타적)
            if (moved && _attackHeld)
                CancelCharge();
        }

        private void JudgeTile()
        {
            // 장애물 위에 있으면 데미지
            foreach (var h in _hazards)
                if (h.Position == _grid.PlayerPosition)
                {
                    _player.TakeDamage(CalcMonsterDamage());
                    return;
                }

            var effect = _grid.GetDangerAt(_grid.PlayerPosition);
            if (effect == null) return;

            if (effect is DamageEffect)
            {
                _player.TakeDamage(CalcMonsterDamage());
            }
            else if (effect is PersistentHazardEffect hazardEffect)
            {
                _hazards.Add(new ActiveHazard
                {
                    Position = _grid.PlayerPosition,
                    Effect   = hazardEffect,
                    RemainingResponsePhases = hazardEffect.durationResponsePhases
                });
                _player.TakeDamage(CalcMonsterDamage());
                // 피격 시 차지 취소
                if (_attackHeld) CancelCharge();
            }
            else if (effect is ShieldEffect)
            {
                _player.GainShield();
            }
        }

        private int CalcMonsterDamage()
        {
            float multiplier = _patternPlayer.CurrentPattern != null
                ? _patternPlayer.CurrentPattern.damageMultiplier
                : 1f;
            return Mathf.RoundToInt(_monster.attackPower * multiplier);
        }

        private void TickHazards()
        {
            for (int i = _hazards.Count - 1; i >= 0; i--)
            {
                _hazards[i].RemainingResponsePhases--;
                if (_hazards[i].RemainingResponsePhases <= 0)
                    _hazards.RemoveAt(i);
            }
        }

        // ── 패턴 전환 ──────────────────────────────────────────
        private void TransitionToNextPattern()
        {
            CheckBossPhaseTransition();

            if (_monsterHp <= 0)
            {
                AudioManager.Instance?.PlaySFX(_monster.deathSfx);
                EndBattle(cleared: true);
                return;
            }

            SelectRandomPattern();
            _state = State.CallPhase;
        }

        // ── 전환 완충 마디 ────────────────────────────────────
        // 신 BPM으로 BEATS_PER_PHASE박 동안 연출 발동 후 CallPhase 시작
        private IEnumerator TransitionMeasureRoutine(double startDsp)
        {
            _state = State.PhaseTransition;
            double secPerBeat = _conductor.SecPerBeat; // 이미 신 BPM
            for (int i = 0; i < BEATS_PER_PHASE; i++)
            {
                double beatDsp = startDsp + i * secPerBeat;
                yield return new WaitUntil(() => !_paused && AudioSettings.dspTime >= beatDsp + _pauseDelta);
                OnBossPhaseChanged?.Invoke();
                _grid.FlashTransition();
            }
            _state = State.CallPhase;
            double callPhaseStart = startDsp + BEATS_PER_PHASE * secPerBeat;
            StartCoroutine(HandlePhrasePair(callPhaseStart));
        }

        private void EndBattle(bool cleared)
        {
            if (_state == State.BattleEnd) return;
            _state = State.BattleEnd;
            _pendingPhaseSwitch = false;
            _conductor.Stop();
            // 전투 종료 — 유지되던 마지막 위험 그리드를 비운다(다음 층 미존재 시에도 잔상 방지).
            _grid.ClearShape();
            if (cleared)
            {
                _player.ResetMana();
                GameManager.Instance.CompleteFloor();
            }
        }

        private void SelectRandomPattern()
        {
            if (_phase.patterns == null || _phase.patterns.Count == 0) return;
            _patternPlayer.SetPattern(_phase.patterns[Random.Range(0, _phase.patterns.Count)]);
        }

        private void CheckBossPhaseTransition()
        {
            if (_monster is not BossMonsterData boss) return;
            float hpPct = (float)_monsterHp / _monster.maxHp;
            var newPhase = boss.GetCurrentPhase(hpPct);
            if (newPhase == _phase) return;
            _phase = newPhase;
            // BGM 전환은 HandlePhrasePair에서 nextPhraseStart 시각에 맞춰 처리
            _pendingPhaseSwitch = true;
            _pendingBgm         = _phase.bgm;
            _pendingBpm         = _phase.bpm;
        }

        // ── 입력 핸들러 ────────────────────────────────────────
        private void OnMoveInput(Vector2 dir)
        {
            if (_state != State.ResponsePhase) return;
            if (dir.sqrMagnitude < 0.1f) return;

            // 버퍼는 항상 최신 입력으로 갱신 — 비트 타이밍 판정은 HandleResponseBeat에서
            _lastMoveDir  = dir;
            _lastMoveTime = AudioSettings.dspTime;

            // 윈도우가 열린 구간에서만 첫 입력 처리 후 슬롯 소진
            if (!_inputWindowOpen) return;
            if (_beatInputConsumed) return;
            _beatInputConsumed = true;
            _pendingMove    = dir;
            _hasPendingMove = true;
        }

        private void OnAttackPressed()
        {
            _attackKeyDown = true;
            if (_state != State.ResponsePhase) return;

            // 버퍼는 항상 최신 입력으로 갱신
            _lastAttackPressTime = AudioSettings.dspTime;

            // 윈도우가 열린 구간에서만 첫 입력 처리 후 슬롯 소진
            if (!_inputWindowOpen) return;
            if (_beatInputConsumed) return;
            if (_player.Mana <= 0) return; // 마나 부족 시 차지 시작 불가
            _beatInputConsumed = true;
            _attackHeld = true;
            _playerAnim?.SetChargeStage(1);
        }

        private void OnAttackReleased()
        {
            _attackKeyDown = false;
            if (!_attackHeld) return;

            _lastAttackReleaseTime = AudioSettings.dspTime;

            if (_state != State.ResponsePhase)
            {
                _attackHeld = false;
                CancelCharge();
                return;
            }

            // 유효 구간 안이면 즉시 발동 — window-close 대기 없이 SFX·HP 반영
            if (_inputWindowOpen)
            {
                FireAttack();
                return;
            }
            if (_tapGraceEndTime > 0.0 && AudioSettings.dspTime <= _tapGraceEndTime)
            {
                FireAttack();
                return;
            }
            // 그 외: 다음 비트 pre-buffer 또는 window-close 안전망에서 처리
        }

        private void FireAttack()
        {
            // 차지 공격(multiplier > 1)은 유지 중 이미 마나를 지불했으므로 추가 비용 없음
            // 탭 공격(multiplier = 1)은 기존대로 마나 1 소비
            if (_chargeDamageMultiplier <= 1f && !_player.SpendMana(1)) return;
            _hasPendingMove = false; // 공격 발동 → 같은 비트 이동 무효
            _playerAnim?.TriggerAttack();
            int dmg = Mathf.RoundToInt(_playerConfig.attackPower * _chargeDamageMultiplier);
            _monsterHp = Mathf.Max(0, _monsterHp - dmg);
            OnMonsterHpChanged?.Invoke(_monsterHp, _monster.maxHp);
            OnMonsterHit?.Invoke();
            AudioManager.Instance?.PlaySFX(_monster.hitSfx);
            ResetCharge();
        }

        private void CancelCharge() => ResetCharge();

        private void PlayShapeFeedbacks(GridEffectShape shape)
        {
            if (shape == null) return;
            foreach (var (pos, effect) in shape.AllCellsWithPosition())
            {
                if (effect == null) continue;
                if (!_monster.effectFeedbacks.TryGetValue(effect, out var fb) || fb == null) continue;
                OnMonsterCellEffectFired?.Invoke(fb, _grid.GetTileWorldPosition(pos));
            }
        }

        private void ResetCharge()
        {
            _attackHeld             = false;
            _chargeBeats            = 0;
            _chargeDamageMultiplier = 1f;
            _lastAttackReleaseTime  = -1f;
            _tapGraceEndTime        = -1f;
            _playerAnim?.SetChargeStage(0);
        }

        private void OnConductorPaused()                     => _paused = true;
        private void OnConductorResumed(double d) { _pauseDelta += d; _paused = false; }

        private void OnPlayerDeath()
        {
            StopAllCoroutines(); // 프레이즈 루프 즉시 종료
            _phraseRunning = false;
            _state = State.BattleEnd;
            _conductor.Stop();
            GameManager.Instance.RestartRun();
        }
    }
}
