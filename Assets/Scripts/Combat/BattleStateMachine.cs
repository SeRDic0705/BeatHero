using System.Collections;
using System.Collections.Generic;
using BeatHero.Core;
using BeatHero.Data;
using UnityEngine;

namespace BeatHero.Combat
{
    // Call & Response 전투 루프 총괄.
    // 의존: Conductor, GridManager, PatternPlayer, GameManager, InputReader, MonsterData
    public class BattleStateMachine : MonoBehaviour
    {
        private const float INPUT_WINDOW_SEC = 0.021f;
        private const int   BEATS_PER_PHASE  = 4;
        private const int   MAX_MANA         = 5;

        [Header("Dependencies")]
        [SerializeField] private Conductor    _conductor;
        [SerializeField] private GridManager  _grid;
        [SerializeField] private PatternPlayer _patternPlayer;
        [SerializeField] private InputReader  _input;

        private MonsterData    _monster;
        private CombatPhaseData _phase;
        private int             _monsterHp;
        private int             _playerMana;

        private List<ActiveHazard> _hazards = new();

        private enum State { Idle, CallPhase, ResponsePhase, BattleEnd }
        private State _state = State.Idle;
        private int   _beatInPhase;  // 0~3

        // ResponsePhase 입력 처리용
        private bool   _inputWindowOpen;
        private bool   _attackPressed;
        private bool   _attackHeld;
        private int    _chargeBeats;
        private float  _chargeDamageMultiplier = 1f;
        private bool   _tileWasDangerAtWindowOpen;
        private Vector2 _pendingMove;
        private bool    _hasPendingMove;

        // 누적 차지 배율 (박자당 +0.5)
        private const float CHARGE_MULT_PER_BEAT = 0.5f;

        private void Awake()
        {
            _conductor.OnBeat += OnBeat;
            _input.OnMoveInput += OnMoveInput;
            _input.OnAttackPressed += OnAttackPressed;
            _input.OnAttackReleased += OnAttackReleased;
        }

        private void OnDestroy()
        {
            _conductor.OnBeat -= OnBeat;
            _input.OnMoveInput -= OnMoveInput;
            _input.OnAttackPressed -= OnAttackPressed;
            _input.OnAttackReleased -= OnAttackReleased;
        }

        public void StartBattle(MonsterData monster)
        {
            _monster   = monster;
            _monsterHp = monster.maxHp;
            _playerMana = 0;
            _hazards.Clear();
            _grid.Initialize(monster.gridType);

            float hpPercent = 1f;
            _phase = monster.GetCurrentPhase(hpPercent);
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
            // 현재 박자에 맞는 BeatUnit의 shape 표시
            var bu = _patternPlayer.GetUnitAtPosition(PatternPlayer.BeatToUnitPosition(_beatInPhase));
            _grid.ShowShape(bu?.gridEffectShape);

            _beatInPhase++;
            if (_beatInPhase >= BEATS_PER_PHASE)
                TransitionToResponsePhase();
        }

        private void TransitionToResponsePhase()
        {
            _state = State.ResponsePhase;
            _beatInPhase = 0;
        }

        // ── ResponsePhase ──────────────────────────────────────
        private IEnumerator HandleResponseBeat()
        {
            // 입력 윈도우 열기 전: 위험 타일 여부 기록
            _tileWasDangerAtWindowOpen = _grid.GetDangerAt(_grid.PlayerPosition) != null;
            _inputWindowOpen = true;
            _hasPendingMove = false;
            _pendingMove = Vector2.zero;

            if (_attackHeld)
            {
                // ResponsePhase 구간 내 키 유지 = 차지
                _playerMana = Mathf.Max(0, _playerMana - 1);
                _chargeDamageMultiplier += CHARGE_MULT_PER_BEAT;
                _chargeBeats++;
            }

            yield return new WaitForSeconds(INPUT_WINDOW_SEC * 2f);
            _inputWindowOpen = false;

            // 이동 처리
            if (_hasPendingMove)
                ProcessMovement(_pendingMove);

            // 판정: 위험 타일
            JudgeTile();

            // 장애물 카운트 차감
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
                // 마나: 아슬아슬 회피 or 일반 이동
                _playerMana = Mathf.Min(MAX_MANA, _playerMana + (_tileWasDangerAtWindowOpen ? 2 : 1));
            }
            // 이동 후 차지는 유지 (이동과 공격은 배타적이므로 차지 취소)
            if (_attackHeld && moved)
                CancelCharge();
        }

        private void JudgeTile()
        {
            var effect = _grid.GetDangerAt(_grid.PlayerPosition);
            if (effect == null)
            {
                // 장애물 위에 있는지 별도 체크
                foreach (var h in _hazards)
                    if (h.Position == _grid.PlayerPosition)
                    {
                        int dmg = Mathf.RoundToInt(_monster.attackPower * GetCurrentPatternMultiplier());
                        GameManager.Instance.ApplyDamage(dmg);
                        return;
                    }
                return;
            }

            if (effect is DamageEffect)
            {
                int dmg = Mathf.RoundToInt(_monster.attackPower * GetCurrentPatternMultiplier());
                GameManager.Instance.ApplyDamage(dmg);
            }
            else if (effect is PersistentHazardEffect hazardEffect)
            {
                // 장애물 등록 + 즉시 데미지
                _hazards.Add(new ActiveHazard
                {
                    Position = _grid.PlayerPosition,
                    Effect = hazardEffect,
                    RemainingResponsePhases = hazardEffect.durationResponsePhases
                });
                int dmg = Mathf.RoundToInt(_monster.attackPower * GetCurrentPatternMultiplier());
                GameManager.Instance.ApplyDamage(dmg);
            }
            else if (effect is ShieldEffect)
            {
                // 보호막은 Phase 4 플레이어에서 처리 예정
            }
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
                _state = State.BattleEnd;
                _conductor.Stop();
                GameManager.Instance.CompleteFloor();
                return;
            }

            SelectRandomPattern();
            _state = State.CallPhase;
            _beatInPhase = 0;
        }

        private void SelectRandomPattern()
        {
            if (_phase.patterns == null || _phase.patterns.Count == 0) return;
            int idx = Random.Range(0, _phase.patterns.Count);
            _patternPlayer.SetPattern(_phase.patterns[idx]);
        }

        private void CheckBossPhaseTransition()
        {
            if (_monster is not BossMonsterData boss) return;
            float hpPercent = (float)_monsterHp / _monster.maxHp;
            var newPhase = boss.GetCurrentPhase(hpPercent);
            if (newPhase == _phase) return;

            _phase = newPhase;
            _conductor.SwitchPhaseAtNextMeasure(_phase.bgm, _phase.bpm);
        }

        // ── 입력 핸들러 ────────────────────────────────────────
        private void OnMoveInput(Vector2 dir)
        {
            if (!_inputWindowOpen || _state != State.ResponsePhase) return;
            if (dir.sqrMagnitude < 0.1f) return;
            _pendingMove = dir;
            _hasPendingMove = true;
        }

        private void OnAttackPressed()
        {
            if (!_inputWindowOpen || _state != State.ResponsePhase) return;
            _attackHeld = true;
            _attackPressed = true;
        }

        private void OnAttackReleased()
        {
            if (!_attackHeld) return;
            _attackHeld = false;

            if (_inputWindowOpen && _state == State.ResponsePhase)
                FireAttack();
            else
                CancelCharge(); // 구간 밖 릴리즈 = 취소
        }

        private void FireAttack()
        {
            // 마지막 릴리즈 박자 마나 소모
            _playerMana = Mathf.Max(0, _playerMana - 1);
            int dmg = Mathf.RoundToInt(10 * _chargeDamageMultiplier); // 기본 데미지 10, Phase 4에서 공식 완성
            _monsterHp = Mathf.Max(0, _monsterHp - dmg);
            ResetCharge();
        }

        private void CancelCharge()
        {
            // 소모된 마나 반환 없음
            ResetCharge();
        }

        private void ResetCharge()
        {
            _attackHeld = false;
            _attackPressed = false;
            _chargeBeats = 0;
            _chargeDamageMultiplier = 1f;
        }

        private float GetCurrentPatternMultiplier()
        {
            var bu = _patternPlayer.GetUnitAtPosition(PatternPlayer.BeatToUnitPosition(_beatInPhase));
            return bu != null ? (_patternPlayer.Current?.gridEffectShape != null
                ? GetPatternDamageMultiplier() : 1f) : 1f;
        }

        private float GetPatternDamageMultiplier()
        {
            // PatternPlayer가 현재 재생 중인 PatternData의 multiplier
            // 현재는 1f 반환 (정확한 연결은 PatternPlayer에 currentPattern 프로퍼티 추가 시 개선)
            return 1f;
        }
    }
}
