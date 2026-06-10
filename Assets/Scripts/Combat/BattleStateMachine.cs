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

        [Header("Dependencies")]
        [SerializeField] private Conductor        _conductor;
        [SerializeField] private GridManager      _grid;
        [SerializeField] private PatternPlayer    _patternPlayer;
        [SerializeField] private InputReader      _input;
        [SerializeField] private PlayerController _player;
        [SerializeField] private PlayerConfig     _playerConfig;

        public event System.Action<MonsterData> OnBattleStarted;
        public event System.Action<int, int> OnMonsterHpChanged; // (current, max)

        private MonsterData     _monster;
        private CombatPhaseData _phase;
        private int             _monsterHp;

        private List<ActiveHazard> _hazards = new();

        private enum State { Idle, CallPhase, ResponsePhase, BattleEnd }
        private State _state = State.Idle;
        private int   _beatInPhase;

        // 입력 윈도우
        private bool    _inputWindowOpen;
        private bool    _attackHeld;
        private int     _chargeBeats;
        private float   _chargeDamageMultiplier = 1f;
        private bool    _tileWasDangerAtWindowOpen;
        private Vector2 _pendingMove;
        private bool    _hasPendingMove;

        // 비트 전 선행 입력 버퍼 + 소진 플래그
        private float   _lastMoveTime          = -1f;
        private Vector2 _lastMoveDir;
        private float   _lastAttackPressTime   = -1f;
        private float   _lastAttackReleaseTime = -1f;
        private float   _tapGraceEndTime      = -1f;
        private bool    _attackKeyDown;      // 공격 키가 물리적으로 눌린 상태
        private bool    _beatInputConsumed;  // true면 이번 비트 추가 입력 무시

        private void Awake()
        {
            _conductor.OnBeat    += OnBeat;
            _input.OnMoveInput   += OnMoveInput;
            _input.OnAttackPressed  += OnAttackPressed;
            _input.OnAttackReleased += OnAttackReleased;
            _player.OnDeath      += OnPlayerDeath;
        }

        private void OnDestroy()
        {
            _conductor.OnBeat       -= OnBeat;
            _input.OnMoveInput      -= OnMoveInput;
            _input.OnAttackPressed  -= OnAttackPressed;
            _input.OnAttackReleased -= OnAttackReleased;
            _player.OnDeath         -= OnPlayerDeath;
        }

        public void StartBattle(MonsterData monster)
        {
            _monster   = monster;
            _monsterHp = monster.maxHp;
            OnBattleStarted?.Invoke(_monster);
            OnMonsterHpChanged?.Invoke(_monsterHp, _monster.maxHp);
            _hazards.Clear();
            _grid.Initialize(monster.gridType);
            _player.Initialize(_playerConfig.maxHp);

            _phase = monster.GetCurrentPhase(1f);
            _conductor.StartSong(_phase.bgm, _phase.bpm);
            SelectRandomPattern();
            _state = State.CallPhase;
            _beatInPhase = 0;
            _player.transform.position = _grid.GetTileWorldPosition(_grid.PlayerPosition);
        }

        private void OnBeat(int beatIndex)
        {
            if (_state == State.CallPhase)
                HandleCallBeat();
            else if (_state == State.ResponsePhase)
                StartCoroutine(HandleResponseBeat());
        }

        // ── CallPhase ──────────────────────────────────────────
        private void HandleCallBeat()
        {
            _grid.SetResponsePhase(false);
            var bu = _patternPlayer.GetUnitAtPosition(PatternPlayer.BeatToUnitPosition(_beatInPhase));
            _grid.ShowShape(bu?.gridEffectShape);
            AudioManager.Instance?.PlaySFX(_callBeatSfx);

            _beatInPhase++;
            if (_beatInPhase >= BEATS_PER_PHASE)
            {
                _state = State.ResponsePhase;
                _beatInPhase = 0;
            }
        }

        // ── ResponsePhase ──────────────────────────────────────
        private IEnumerator HandleResponseBeat()
        {
            // CallPhase와 동일 패턴을 비트 타이밍에 재표시
            _grid.SetResponsePhase(true);
            var bu = _patternPlayer.GetUnitAtPosition(PatternPlayer.BeatToUnitPosition(_beatInPhase));
            _grid.ShowShape(bu?.gridEffectShape);

            float beatTime      = Time.time;
            float preJudgStart  = beatTime - _judgmentWindowSec;
            float preFailStart  = preJudgStart - _failZoneSec;

            // 선행 릴리즈 처리: 윈도우 밖에서 뗀 상태로 비트가 도달한 경우
            // (_attackHeld=true이면서 키가 이미 올라와 있음 = OnAttackReleased가 defer한 것)
            if (_attackHeld && !_attackKeyDown)
            {
                bool inPreBuffer = _lastAttackReleaseTime >= preJudgStart;
                bool inTapGrace  = _tapGraceEndTime > 0f && _lastAttackReleaseTime <= _tapGraceEndTime;
                if (inPreBuffer || inTapGrace)
                    FireAttack(); // 사전버퍼 또는 탭유예 내 릴리즈 → 공격 발동
                else
                    CancelCharge(); // 유효구간 밖 릴리즈 → 취소
                _lastAttackReleaseTime = -1f;
                _tapGraceEndTime       = -1f;
            }

            // 선행 입력 분류 (이동)
            bool preMoveJudge = _lastMoveTime >= preJudgStart;
            bool preMovesFail = !preMoveJudge && _lastMoveTime >= preFailStart;
            // 선행 입력 분류 (공격 프레스)
            bool preAttackJudge = _lastAttackPressTime >= preJudgStart;
            bool preAttackFail  = !preAttackJudge && _lastAttackPressTime >= preFailStart;

            bool continuingCharge = _attackHeld; // 이전 비트부터 홀드 중 (선행 릴리즈 처리 후 스냅샷)

            // 판정 / 실패구간 선행 입력 → 슬롯 소진 처리
            // continuingCharge는 제외: 차지 중에도 이동으로 차지 취소 가능해야 함
            _beatInputConsumed = preMoveJudge || preMovesFail
                               || preAttackJudge || preAttackFail;

            _hasPendingMove = preMoveJudge;
            _pendingMove    = preMoveJudge ? _lastMoveDir : Vector2.zero;

            // 선행 공격 판정: 키가 아직 눌린 상태면 차지 시작, 이미 뗐으면 탭 발사
            if (preAttackJudge)
            {
                if (_attackKeyDown)
                    _attackHeld = true;
                else
                    FireAttack(); // 비트 전 탭 공격 → 즉시 발사
            }

            // 버퍼 소진
            _lastMoveTime        = -1f;
            _lastAttackPressTime = -1f;

            _tileWasDangerAtWindowOpen = _grid.GetDangerAt(_grid.PlayerPosition) != null;
            _inputWindowOpen = true;

            yield return new WaitForSeconds(_judgmentWindowSec);
            _inputWindowOpen   = false;
            _beatInputConsumed = false; // 다음 비트를 위해 초기화

            // 윈도우 종료 시 공격 처리 — 이동과 동일한 타이밍으로 발동
            if (_attackHeld && !_attackKeyDown)
            {
                FireAttack(); // 탭 or 차지 릴리즈: 윈도우 안에서 뗀 경우
            }
            else if (!continuingCharge && _attackHeld && _attackKeyDown)
            {
                // 새 프레스, 아직 홀드 중 → 탭 유예 시작 (느린 탭과 차지 의도 구분 유예)
                _tapGraceEndTime = Time.time + _tapGraceSec;
            }
            else if (continuingCharge && _attackHeld && _attackKeyDown)
            {
                // 차지 유지: 윈도우 종료까지 홀드 = 차지 의도 확정, 마나 후납
                _player.SpendMana(1);
                _chargeDamageMultiplier += CHARGE_MULT_PER_BEAT;
                _chargeBeats++;
                _tapGraceEndTime = -1f;
            }

            if (_hasPendingMove) ProcessMovement(_pendingMove);

            JudgeTile();
            TickHazards();
            _grid.SetHazards(_hazards);

            _beatInPhase++;
            if (_beatInPhase >= BEATS_PER_PHASE)
                TransitionToNextPattern();
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
            AudioManager.Instance?.PlaySFX(effect.feedback?.activateSfx);
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
            _beatInPhase = 0;
        }

        private void EndBattle(bool cleared)
        {
            _state = State.BattleEnd;
            _conductor.Stop();
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
            _conductor.SwitchPhaseAtNextMeasure(_phase.bgm, _phase.bpm);
        }

        // ── 입력 핸들러 ────────────────────────────────────────
        private void OnMoveInput(Vector2 dir)
        {
            if (_state != State.ResponsePhase) return;
            if (dir.sqrMagnitude < 0.1f) return;

            // 버퍼는 항상 최신 입력으로 갱신 — 비트 타이밍 판정은 HandleResponseBeat에서
            _lastMoveDir  = dir;
            _lastMoveTime = Time.time;

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
            _lastAttackPressTime = Time.time;

            // 윈도우가 열린 구간에서만 첫 입력 처리 후 슬롯 소진
            if (!_inputWindowOpen) return;
            if (_beatInputConsumed) return;
            _beatInputConsumed = true;
            _attackHeld = true;
        }

        private void OnAttackReleased()
        {
            _attackKeyDown = false;
            if (!_attackHeld) return;

            _lastAttackReleaseTime = Time.time;

            if (_state != State.ResponsePhase)
            {
                _attackHeld = false;
                CancelCharge();
            }
            // ResponsePhase: _attackHeld 유지, HandleResponseBeat에서 처리
            // 윈도우 안/밖 모두 window-close 또는 사전버퍼 체크에서 발동
        }

        private void FireAttack()
        {
            _hasPendingMove = false; // 공격 발동 → 같은 비트 이동 무효
            _player.SpendMana(1);
            int dmg = Mathf.RoundToInt(_playerConfig.attackPower * _chargeDamageMultiplier);
            _monsterHp = Mathf.Max(0, _monsterHp - dmg);
            OnMonsterHpChanged?.Invoke(_monsterHp, _monster.maxHp);
            AudioManager.Instance?.PlaySFX(_monster.hitSfx);
            ResetCharge();
        }

        private void CancelCharge() => ResetCharge();

        private void ResetCharge()
        {
            _attackHeld             = false;
            _chargeBeats            = 0;
            _chargeDamageMultiplier = 1f;
            _lastAttackReleaseTime  = -1f;
            _tapGraceEndTime        = -1f;
        }

        private void OnPlayerDeath()
        {
            _state = State.BattleEnd;
            _conductor.Stop();
            GameManager.Instance.RestartRun();
        }
    }
}
