using BeatHero.Data;
using UnityEngine;

namespace BeatHero.Combat
{
    // 씬에 배치된 몬스터 GameObject의 비주얼 담당.
    // BattleStateMachine.OnBattleStarted 구독 → 층 전환마다 스프라이트/애니메이터 자동 교체.
    // BattleStateMachine.OnBeatUnitFired 구독 → BeatUnit 발화마다 스프라이트 순환.
    [RequireComponent(typeof(SpriteRenderer))]
    public class MonsterView : MonoBehaviour
    {
        private SpriteRenderer      _renderer;
        private Animator            _animator;
        private BattleStateMachine  _battle;

        private MonsterData _currentMonster;
        private int         _spriteIndex;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _animator = GetComponent<Animator>();

            _battle = Object.FindAnyObjectByType<BattleStateMachine>();
            if (_battle != null)
            {
                _battle.OnBattleStarted += SetMonster;
                _battle.OnBeatUnitFired += AdvanceSprite;
            }
        }

        private void Start()
        {
            if (_battle != null && _battle.CurrentMonster != null)
                SetMonster(_battle.CurrentMonster);
        }

        private void OnDestroy()
        {
            if (_battle != null)
            {
                _battle.OnBattleStarted -= SetMonster;
                _battle.OnBeatUnitFired -= AdvanceSprite;
            }
        }

        private void SetMonster(MonsterData data)
        {
            _currentMonster = data;
            _spriteIndex    = 0;
            _renderer.sprite = data.sprites.Count > 0 ? data.sprites[0] : null;

            if (_animator != null)
                _animator.runtimeAnimatorController = data.animator;
        }

        private void AdvanceSprite()
        {
            if (_currentMonster == null || _currentMonster.sprites.Count == 0) return;
            _renderer.sprite = _currentMonster.sprites[_spriteIndex];
            _spriteIndex = (_spriteIndex + 1) % _currentMonster.sprites.Count;
        }
    }
}
