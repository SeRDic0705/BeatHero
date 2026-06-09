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
        private const float INPUT_WINDOW_SEC  = 0.021f;
        private const int   BEATS_PER_PHASE   = 4;
        private const float CHARGE_MULT_PER_BEAT = 0.5f;

        [Header("Dependencies")]
        [SerializeField] private Conductor        _conductor;
        [SerializeField] private GridManager      _grid;
        [SerializeField] private PatternPlayer    _patternPlayer;
        [SerializeField] private InputReader      _input;
        [SerializeField] private PlayerController _player;
        [SerializeField] private PlayerConfig     _playerConfig;

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
            _hazards.Clear();
            _grid.Initialize(monster.gridType);
            _player.Initialize(_playerConfig.maxHp);

            _phase = monster.GetCurrentPhase(1f);
            _conductor.StartSong(_phase.bgm, _phase.bpm);
            SelectRandomPattern();
            _state = State.CallPhase;
            _beatInPhase = 0;
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
            var bu = _patternPlayer.GetUnitAtPosition(PatternPlayer.BeatToUnitPosition(_beatInPhase));
            _grid.ShowShape(bu?.gridEffectShape);

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
            _tileWasDangerAtWindowOpen = _grid.GetDangerAt(_grid.PlayerPosition) != null;
            _inputWindowOpen = true;
            _hasPendingMove  = false;
            _pendingMove     = Vector2.zero;

            if (_attackHeld)
            {
                // 구간 내 키 유지 = 차지 (마나 소모 + 배율 누적)
                _player.SpendMana(1);
                _chargeDamageMultiplier += CHARGE_MULT_PER_BEAT;
                _chargeBeats++;
            }

            yield return new WaitForSeconds(INPUT_WINDOW_SEC * 2f);
            _inputWindowOpen = false;

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
                _player.AddMana(_tileWasDangerAtWindowOpen ? 2 : 1);

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
            _grid.ClearShape();
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
            if (!_inputWindowOpen || _state != State.ResponsePhase) return;
            if (dir.sqrMagnitude < 0.1f) return;
            _pendingMove    = dir;
            _hasPendingMove = true;
        }

        private void OnAttackPressed()
        {
            if (!_inputWindowOpen || _state != State.ResponsePhase) return;
            _attackHeld = true;
        }

        private void OnAttackReleased()
        {
            if (!_attackHeld) return;
            _attackHeld = false;

            if (_inputWindowOpen && _state == State.ResponsePhase)
                FireAttack();
            else
                CancelCharge();
        }

        private void FireAttack()
        {
            _player.SpendMana(1);
            int dmg = Mathf.RoundToInt(_playerConfig.attackPower * _chargeDamageMultiplier);
            _monsterHp = Mathf.Max(0, _monsterHp - dmg);
            AudioManager.Instance?.PlaySFX(_monster.hitSfx);
            ResetCharge();
        }

        private void CancelCharge() => ResetCharge();

        private void ResetCharge()
        {
            _attackHeld             = false;
            _chargeBeats            = 0;
            _chargeDamageMultiplier = 1f;
        }

        private void OnPlayerDeath()
        {
            _state = State.BattleEnd;
            _conductor.Stop();
            GameManager.Instance.RestartRun();
        }
    }
}
