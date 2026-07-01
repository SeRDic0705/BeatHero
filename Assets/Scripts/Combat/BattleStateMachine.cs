using System.Collections;
using System.Collections.Generic;
using BeatHero.Audio;
using BeatHero.Core;
using BeatHero.Data;
using BeatHero.Player;
using UnityEngine;

namespace BeatHero.Combat
{
    // 타이밍 판정 결과 — Fast/Slow 피드백 및 추후 Perfect 확장에 사용
    public enum TimingResult { Fast, Slow }

    // Call & Response 전투 루프 총괄.
    // 의존: Conductor, GridManager, PatternPlayer, PlayerController, PlayerConfig, InputReader
    public class BattleStateMachine : MonoBehaviour
    {
        private const int   BEATS_PER_PHASE = 4;
        private const int   MANA_GAIN_MOVE  = 1; // 일반 이동 시 획득
        private const int   MANA_GAIN_DODGE = 2; // 회피(위험 타일에서 이동) 시 획득

        [Header("Charge Attack")]
        [SerializeField] private float _chargeMultPerBeat = 0.5f;   // 차지 유지 1박당 데미지 배율 증가량

        [Header("Block")]
        [SerializeField] private int _blockManaCost = 1;

        [Header("Input Timing")]
        [SerializeField] private float _judgmentWindowSec = 0.021f; // 판정구간 반폭 (비트 전후 각각)
        [SerializeField] private float _failZoneSec       = 0.021f; // 판정 실패구간 반폭 (판정구간 바깥)

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
        public event System.Action<double, bool> OnBeatUnitFired; // beatDurationSec, hasGridEffect
        public event System.Action OnEffectiveBeatFired; // ResponsePhase + gridEffectShape != null 인 비트만
        public event System.Action OnPlayerBlockAbsorbed; // 방어로 피해를 흡수한 순간
        public event System.Action<bool> OnMonsterHit;   // 플레이어 공격이 몬스터에 데미지를 입혔을 때 (isLethal = 이 타격으로 HP가 0이 됨)
        public event System.Action OnMonsterDefeated;     // 데미지로 몬스터 HP가 0이 된 순간(프레이즈 즉시 종료 직전)
        public event System.Action<CellEffectFeedback, Vector3> OnMonsterCellEffectFired;
        public event System.Action OnBossPhaseChanged; // 전환 완충 마디 박자마다 발동
        public event System.Action OnCallPhaseStarted;     // CallPhase(공격 예고) 시작
        public event System.Action OnResponsePhaseStarted; // ResponsePhase(플레이어 대응) 시작
        public event System.Action<TimingResult> OnTimingMissed; // 입력이 판정윈도우 밖(Fast/Slow)일 때

        public MonsterData CurrentMonster => _monster;
        private MonsterData     _monster;
        private CombatPhaseData _phase;
        private int             _monsterHp;
        private int             _patternIndex; // 순차 출력(isRandom=false)용 런타임 인덱스

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
        private double  _lastMoveTime               = -1.0;
        private Vector2 _lastMoveDir;
        private double  _lastBasicAttackPressTime   = -1.0;
        private double  _lastAttackPressTime        = -1.0;
        private double  _lastAttackReleaseTime      = -1.0;
        private double  _lastBlockPressTime         = -1.0;
        private bool    _blockActive;        // 이번 박자 JudgeTile에서 피해 무효화 예약
        private bool    _chargeKeyDown;      // K키가 물리적으로 눌린 상태
        private bool    _beatInputConsumed;  // true면 이번 비트 추가 입력 무시

        // 타이밍 미스 감지
        private double _lateZoneEndDsp;     // Late Zone(Slow 감지) 종료 DSP 절대시각
        private bool   _slowInputReceived;  // Late Zone 구간 내 입력 수신 여부

        private void Awake()
        {
            _conductor.OnBeat    += OnBeat;
            _conductor.OnPaused  += OnConductorPaused;
            _conductor.OnResumed += OnConductorResumed;
            _player.OnDeath      += OnPlayerDeath;
        }

        private void Start()
        {
            _input.OnMoveInput           += OnMoveInput;
            _input.OnBasicAttackPressed  += OnBasicAttackPressed;
            _input.OnChargeAttackPressed  += OnChargeAttackPressed;
            _input.OnChargeAttackReleased += OnChargeAttackReleased;
            _input.OnBlockPressed        += OnBlockPressed;
        }

        private void OnDestroy()
        {
            _conductor.OnBeat    -= OnBeat;
            _conductor.OnPaused  -= OnConductorPaused;
            _conductor.OnResumed -= OnConductorResumed;
            _player.OnDeath      -= OnPlayerDeath;
            if (_input != null)
            {
                _input.OnMoveInput            -= OnMoveInput;
                _input.OnBasicAttackPressed   -= OnBasicAttackPressed;
                _input.OnChargeAttackPressed  -= OnChargeAttackPressed;
                _input.OnChargeAttackReleased -= OnChargeAttackReleased;
                _input.OnBlockPressed         -= OnBlockPressed;
            }
        }

        // 1단계: 현재 층에 등장할 몬스터·bpm·bgm·패턴을 미리 세팅/로드. 클럭은 아직 시작 안 함.
        // keepPlayerHp=true면 플레이어 HP 유지(층 전환). false면 풀 회복(런 시작).
        public void SetFloorData(MonsterData monster, bool keepPlayerHp = false)
        {
            StopAllCoroutines();
            _phraseRunning = false;
            _monster   = monster;
            _monsterHp = monster.maxHp;
            OnBattleStarted?.Invoke(_monster);
            OnMonsterHpChanged?.Invoke(_monsterHp, _monster.maxHp);
            _hazards.Clear();
            _grid.Initialize(monster.gridType);
            _player.Initialize(_playerConfig.maxHp, keepPlayerHp);

            _pendingPhaseSwitch = false;
            _phase = monster.GetCurrentPhase(1f);
            _conductor.PrepareSong(_phase.bgm, _phase.bpm);
            // 콜 효과음 프리로드 — 미로드 시 첫 PlayScheduled에서 로딩 지연으로 첫 박이 밀린다.
            if (_callBeatSfx != null && _callBeatSfx.loadState != AudioDataLoadState.Loaded)
                _callBeatSfx.LoadAudioData();
            _patternIndex = 0;
            SelectNextPattern();
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
            bool firstNote = true;

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

                // CallPhase 시작 알림은 첫 비트가 "실제로" 발화되는 이 시점에. 코루틴 시작 시점에 울리면
                // 다음 프레이즈는 ResponsePhase 종료 직후(=다음 CallPhase 약 1박 전) 시작되므로 한 박 빨리 울린다.
                if (firstNote) { OnCallPhaseStarted?.Invoke(); firstNote = false; }
                OnBeatUnitFired?.Invoke(secPerUnit * (int)bu.noteLength, bu.gridEffectShape != null);
                _grid.ShowShape(bu.gridEffectShape, secPerUnit * (int)bu.noteLength);

                unitOffset += (int)bu.noteLength;
            }
        }

        // ── ResponsePhase ──────────────────────────────────────
        private IEnumerator HandleResponsePhrase(double phraseStartDsp)
        {
            // CallPhase 마지막 박자를 1박 동안 표시 후 제거 (즉시 ClearShape하면 마지막 장판이 1프레임만 보임)
            yield return new WaitUntil(() => !_paused && AudioSettings.dspTime >= phraseStartDsp + _pauseDelta);
            _grid.SetResponsePhase(true);
            OnResponsePhaseStarted?.Invoke();
            _grid.ClearShape();
            double secPerUnit = _conductor.SecPerBeat / PatternPlayer.UNITS_PER_BEAT;
            int unitOffset = 0;

            foreach (var bu in _patternPlayer.CurrentPattern.beatUnits)
            {
                double noteStartDsp = phraseStartDsp + secPerUnit * unitOffset;
                yield return new WaitUntil(() => !_paused && AudioSettings.dspTime >= noteStartDsp + _pauseDelta);

                OnBeatUnitFired?.Invoke(secPerUnit * (int)bu.noteLength, bu.gridEffectShape != null);
                if (bu.gridEffectShape != null) OnEffectiveBeatFired?.Invoke();
                _grid.UpdateDangerMap(bu.gridEffectShape); // 타일 색상 변경 없이 판정맵만 갱신
                PlayShapeFeedbacks(bu.gridEffectShape);

                double beatTime     = noteStartDsp;
                double preJudgStart = beatTime - _judgmentWindowSec;
                double preFailStart = preJudgStart - _failZoneSec;

                if (_attackHeld && !_chargeKeyDown)
                {
                    bool inPreBuffer = _lastAttackReleaseTime >= preJudgStart;
                    if (inPreBuffer) FireAttack();
                    else CancelCharge();
                    _lastAttackReleaseTime = -1f;
                }

                bool preMoveJudge        = _lastMoveTime >= preJudgStart;
                bool preMovesFail        = !preMoveJudge && _lastMoveTime >= preFailStart;
                bool preBasicAttackJudge = _lastBasicAttackPressTime >= preJudgStart;
                bool preBasicAttackFail  = !preBasicAttackJudge && _lastBasicAttackPressTime >= preFailStart;
                bool preChargeJudge      = _lastAttackPressTime >= preJudgStart;
                bool preChargeFail       = !preChargeJudge && _lastAttackPressTime >= preFailStart;
                bool preBlockJudge       = _lastBlockPressTime >= preJudgStart;
                bool preBlockFail        = !preBlockJudge && _lastBlockPressTime >= preFailStart;
                bool fastHappened        = preMovesFail || preBasicAttackFail || preChargeFail || preBlockFail;

                bool continuingCharge = _attackHeld;

                _beatInputConsumed = preMoveJudge || preMovesFail || preBasicAttackJudge || preBasicAttackFail
                                   || preChargeJudge || preChargeFail || preBlockJudge || preBlockFail;
                _hasPendingMove    = preMoveJudge;
                _pendingMove       = preMoveJudge ? _lastMoveDir : Vector2.zero;

                if (preBasicAttackJudge && !_attackHeld)
                {
                    _beatInputConsumed = true;
                    FireAttack(); // _chargeBeats == 0 → 기본공격, FireAttack 내에서 마나 소모
                }
                else if (preChargeJudge)
                {
                    if (_chargeKeyDown && _player.SpendMana(1))
                    {
                        _attackHeld = true;
                        _chargeBeats++;
                        _playerAnim?.SetChargeStage(1);
                    }
                    // K키가 이미 릴리즈됐으면 무시 (차지는 홀드 필수)
                }
                else if (preBlockJudge && !_attackHeld)
                {
                    _beatInputConsumed = true;
                    TryActivateBlock();
                }

                _lastMoveTime             = -1.0;
                _lastAttackPressTime      = -1.0;
                _lastBasicAttackPressTime = -1.0;
                _lastBlockPressTime       = -1.0;

                _tileWasDangerAtWindowOpen = _grid.GetDangerAt(_grid.PlayerPosition) != null;
                _inputWindowOpen = true;

                // 판정 윈도우 닫힘 = 비트 + 판정구간 반폭 (DSP 절대시각 기준)
                double windowCloseDsp = noteStartDsp + _judgmentWindowSec;
                yield return new WaitUntil(() => !_paused && AudioSettings.dspTime >= windowCloseDsp + _pauseDelta);

                bool hadInWindowPress  = _beatInputConsumed;
                _inputWindowOpen       = false;
                _beatInputConsumed     = false;
                _slowInputReceived     = false;
                _lateZoneEndDsp        = windowCloseDsp + _failZoneSec;

                if (_attackHeld && !_chargeKeyDown)
                    FireAttack();
                else if (_attackHeld && _chargeKeyDown)
                {
                    if (_player.SpendMana(1))
                    {
                        _chargeBeats++;
                        _playerAnim?.SetChargeStage(Mathf.Min(_chargeBeats, 3) + 1);
                    }
                }

                if (_hasPendingMove) ProcessMovement(_pendingMove);
                JudgeTile();
                _grid.SetHazards(_hazards);

                if (fastHappened)
                    OnTimingMissed?.Invoke(TimingResult.Fast);

                // Late Zone(_failZoneSec) 만료까지 대기 — 그 사이 입력이 오면 _slowInputReceived가 세팅됨
                yield return new WaitUntil(() => !_paused && AudioSettings.dspTime >= _lateZoneEndDsp + _pauseDelta);

                if (!fastHappened && _slowInputReceived && !hadInWindowPress)
                    OnTimingMissed?.Invoke(TimingResult.Slow);

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
                _player.AddMana(_tileWasDangerAtWindowOpen ? MANA_GAIN_DODGE : MANA_GAIN_MOVE);
            }

            // 이동 시 차지 취소 (이동과 공격 배타적)
            if (moved && _attackHeld)
                CancelCharge();
        }

        private void JudgeTile()
        {
            bool blocked = _blockActive;
            _blockActive = false;

            // 장애물 위에 있으면 데미지
            foreach (var h in _hazards)
                if (h.Position == _grid.PlayerPosition)
                {
                    if (blocked) { FireBlockAbsorbFeedback(); return; }
                    _player.TakeDamage(CalcMonsterDamage());
                    if (_attackHeld) CancelCharge(); // 피격 시 차지 취소
                    return;
                }

            var effect = _grid.GetDangerAt(_grid.PlayerPosition);
            if (effect == null)
            {
                if (blocked) _playerAnim?.ClearBlock();
                return;
            }
            if (blocked) { FireBlockAbsorbFeedback(); return; }

            if (effect is DamageEffect)
            {
                _player.TakeDamage(CalcMonsterDamage());
                if (_attackHeld) CancelCharge(); // 피격 시 차지 취소
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
                EndBattle(cleared: true);
                return;
            }

            SelectNextPattern();
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
            // 진행 중이던 프레이즈 코루틴 즉시 중단(OnPlayerDeath와 동일 패턴).
            StopAllCoroutines();
            _phraseRunning = false;
            _state = State.BattleEnd;
            _pendingPhaseSwitch = false;
            // 전투 종료 — 유지되던 마지막 위험 그리드를 비운다(다음 층 미존재 시에도 잔상 방지).
            _grid.ClearShape();
            if (cleared)
            {
                // BGM은 즉시 끊지 않고 아이리스가 닫히는 시점(돌진+퇴장)까지 페이드아웃.
                _conductor.FadeOutAndStop(GameManager.Instance.ClearFadeDuration);
                _player.ResetMana();
                GameManager.Instance.CompleteFloor();
            }
            else
            {
                _conductor.Stop();
            }
        }

        // isRandom=true면 균등 랜덤, false면 0번부터 순서대로 순환 선택(튜토리얼용 고정 순서).
        private void SelectNextPattern()
        {
            var patterns = _phase.patterns;
            if (patterns == null || patterns.Count == 0) return;

            int index;
            if (_monster.isRandom)
            {
                index = Random.Range(0, patterns.Count);
            }
            else
            {
                index = _patternIndex % patterns.Count;
                _patternIndex = (_patternIndex + 1) % patterns.Count;
            }
            _patternPlayer.SetPattern(patterns[index]);
        }

        private void CheckBossPhaseTransition()
        {
            if (_monster is not BossMonsterData boss) return;
            float hpPct = (float)_monsterHp / _monster.maxHp;
            var newPhase = boss.GetCurrentPhase(hpPct);
            if (newPhase == _phase) return;
            _phase = newPhase;
            _patternIndex = 0; // 새 페이즈는 패턴 리스트가 바뀌므로 순차 인덱스 리셋
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
            if (!_inputWindowOpen)
            {
                if (AudioSettings.dspTime < _lateZoneEndDsp) _slowInputReceived = true;
                return;
            }
            if (_beatInputConsumed) return;
            _beatInputConsumed = true;
            _pendingMove    = dir;
            _hasPendingMove = true;
        }

        private void OnBasicAttackPressed()
        {
            if (_state != State.ResponsePhase) return;
            if (_attackHeld) return; // 차지 중 기본공격 무시

            _lastBasicAttackPressTime = AudioSettings.dspTime;

            if (!_inputWindowOpen)
            {
                if (AudioSettings.dspTime < _lateZoneEndDsp) _slowInputReceived = true;
                return;
            }
            if (_beatInputConsumed) return;
            _beatInputConsumed = true;
            FireAttack(); // _chargeBeats == 0 → 기본공격, 마나는 FireAttack 내에서 소모
        }

        private void OnChargeAttackPressed()
        {
            _chargeKeyDown = true;
            if (_state != State.ResponsePhase) return;

            _lastAttackPressTime = AudioSettings.dspTime;

            if (!_inputWindowOpen)
            {
                if (AudioSettings.dspTime < _lateZoneEndDsp) _slowInputReceived = true;
                return;
            }
            if (_beatInputConsumed) return;
            if (!_player.SpendMana(1)) return;
            _beatInputConsumed = true;
            _attackHeld = true;
            _chargeBeats++;
            _playerAnim?.SetChargeStage(1);
        }

        private void OnChargeAttackReleased()
        {
            _chargeKeyDown = false;
            if (!_attackHeld) return;

            _lastAttackReleaseTime = AudioSettings.dspTime;

            if (_state != State.ResponsePhase)
            {
                _attackHeld = false;
                CancelCharge();
                return;
            }

            if (_inputWindowOpen)
            {
                _beatInputConsumed = true;
                FireAttack();
                return;
            }
            if (AudioSettings.dspTime < _lateZoneEndDsp) _slowInputReceived = true;
            // 유예 구간 없음 — pre-buffer에서만 처리
        }

        private void OnBlockPressed()
        {
            if (_state != State.ResponsePhase) return;
            if (_attackHeld) return; // 차지 중 방어 불가

            _lastBlockPressTime = AudioSettings.dspTime;

            if (!_inputWindowOpen)
            {
                if (AudioSettings.dspTime < _lateZoneEndDsp) _slowInputReceived = true;
                return;
            }
            if (_beatInputConsumed) return;
            _beatInputConsumed = true;
            TryActivateBlock();
        }

        private void FireBlockAbsorbFeedback()
        {
            _playerAnim?.TriggerBlockAbsorb();
            AudioManager.Instance?.PlaySFX(_playerConfig.blockAbsorbSfx);
            OnPlayerBlockAbsorbed?.Invoke();
        }

        private void TryActivateBlock()
        {
            if (!_player.SpendMana(_blockManaCost)) return;
            _blockActive = true;
            _playerAnim?.TriggerBlock();
            AudioManager.Instance?.PlaySFX(_playerConfig.blockSfx);
        }

        private void FireAttack()
        {
            // 탭 공격(차지 0단계): 여기서 마나 1 소비, 기본 공격력
            // 차지 공격: 시작/유지 비트에서 이미 마나 소비 → (공격력 + 차지배율) × 차지단계
            if (_chargeBeats == 0 && !_player.SpendMana(1)) return;
            _hasPendingMove = false; // 공격 발동 → 같은 비트 이동 무효
            _playerAnim?.TriggerAttack();
            int dmg = _chargeBeats > 0
                ? Mathf.RoundToInt(_playerConfig.attackPower * (1 + _chargeMultPerBeat) * _chargeBeats)
                : _playerConfig.attackPower;
            _monsterHp = Mathf.Max(0, _monsterHp - dmg);
            OnMonsterHpChanged?.Invoke(_monsterHp, _monster.maxHp);
            OnMonsterHit?.Invoke(_monsterHp <= 0);
            AudioManager.Instance?.PlaySFX(_playerConfig.hitSfx); // 모든 몬스터 공통 피격음(플레이어 보유)
            ResetCharge();

            // HP가 0이 되면 프레이즈를 끝까지 기다리지 않고 즉시 종료.
            if (_monsterHp <= 0)
            {
                OnMonsterDefeated?.Invoke(); // 몬스터 Hurt 마지막 프레임 정지
                EndBattle(cleared: true);
            }
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
