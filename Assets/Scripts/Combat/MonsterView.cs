using System.Collections;
using System.Collections.Generic;
using BeatHero.Audio;
using BeatHero.Data;
using UnityEngine;

namespace BeatHero.Combat
{
    // 씬에 배치된 몬스터 GameObject의 비주얼 담당.
    // OnBattleStarted  → 층 전환마다 스프라이트/애니메이터 교체.
    // OnBeatUnitFired  → BeatUnit 발화마다 스프라이트 순환 + 팝 이동.
    // OnMonsterCellEffectFired → CellEffect 타입별 SFX + VFX 재생.
    [RequireComponent(typeof(SpriteRenderer))]
    public class MonsterView : MonoBehaviour
    {
        [SerializeField] private float _popUnits      = 0.2f;
        [SerializeField] private float _shakeAmount   = 0.15f;
        [SerializeField] private float _shakeDuration = 0.2f;

        private SpriteRenderer      _renderer;
        private Animator            _animator;
        private BattleStateMachine  _battle;

        private MonsterData _currentMonster;
        private int         _spriteIndex;
        private Vector3     _baseLocalPos;
        private bool        _yFlip;
        private readonly HashSet<CellEffectFeedback> _sfxPlayedThisBeat = new();

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _animator = GetComponent<Animator>();

            _battle = Object.FindAnyObjectByType<BattleStateMachine>();
            if (_battle != null)
            {
                _battle.OnBattleStarted          += SetMonster;
                _battle.OnBeatUnitFired          += AdvanceSprite;
                _battle.OnMonsterCellEffectFired += PlayCellEffectFeedback;
                _battle.OnBossPhaseChanged       += OnBossPhaseChanged;
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
                _battle.OnBattleStarted          -= SetMonster;
                _battle.OnBeatUnitFired          -= AdvanceSprite;
                _battle.OnMonsterCellEffectFired -= PlayCellEffectFeedback;
                _battle.OnBossPhaseChanged       -= OnBossPhaseChanged;
            }
        }

        private void SetMonster(MonsterData data)
        {
            _currentMonster  = data;
            _spriteIndex     = 0;
            _baseLocalPos    = transform.localPosition;
            _yFlip           = false;
            _renderer.sprite = data.sprites.Count > 0 ? data.sprites[0] : null;

            if (_animator != null)
                _animator.runtimeAnimatorController = data.animator;
        }

        private void AdvanceSprite()
        {
            if (_currentMonster == null || _currentMonster.sprites.Count == 0) return;

            _renderer.sprite = _currentMonster.sprites[_spriteIndex];
            _spriteIndex = (_spriteIndex + 1) % _currentMonster.sprites.Count;

            _yFlip = !_yFlip;
            transform.localPosition = _baseLocalPos + Vector3.up * (_yFlip ? _popUnits : -_popUnits);
            _sfxPlayedThisBeat.Clear();
        }

        private void OnBossPhaseChanged()
        {
            if (_animator != null)
                _animator.SetTrigger("PhaseChange");
            StartCoroutine(ShakeRoutine());
        }

        private IEnumerator ShakeRoutine()
        {
            float elapsed = 0f;
            while (elapsed < _shakeDuration)
            {
                elapsed += Time.deltaTime;
                Vector2 offset = Random.insideUnitCircle * _shakeAmount;
                transform.localPosition = _baseLocalPos + new Vector3(offset.x, offset.y, 0f);
                yield return null;
            }
            transform.localPosition = _baseLocalPos;
        }

        private void PlayCellEffectFeedback(CellEffectFeedback feedback, Vector3 worldPos)
        {
            if (feedback.vfxFrames != null && feedback.vfxFrames.Length > 0)
                VFXPool.Instance?.Play(feedback.vfxFrames, feedback.vfxFps, worldPos);
            if (_sfxPlayedThisBeat.Add(feedback))
                AudioManager.Instance?.PlaySFX(feedback.activateSfx);
        }
    }
}
