using System.Collections;
using BeatHero.Audio;
using BeatHero.Data;
using UnityEngine;

namespace BeatHero.Combat
{
    // 씬에 배치된 몬스터 GameObject의 비주얼 담당.
    // OnBattleStarted         → 층 전환마다 MonsterData 클립으로 OverrideController 교체.
    // OnBeatUnitFired(sec)    → BeatUnit 재생 길이에 맞춰 Idle 속도 조정 + 재생.
    // OnMonsterHit            → Hurt 트리거.
    // OnMonsterCellEffectFired→ CellEffect 타입별 SFX + VFX 재생.
    // PlayDeath()             → GameManager가 시네마틱 타이밍에 직접 호출.
    [RequireComponent(typeof(SpriteRenderer), typeof(Animator))]
    public class MonsterView : MonoBehaviour
    {
        private static readonly int HurtHash  = Animator.StringToHash("hurt");
        private static readonly int DeathHash = Animator.StringToHash("death");

        // 모든 몬스터가 공유하는 상태머신. Inspector에서 MonsterBaseAnimator 연결.
        [SerializeField] private RuntimeAnimatorController _baseController;
        [SerializeField] private float _shakeAmount   = 0.15f;
        [SerializeField] private float _shakeDuration = 0.2f;

        private Animator            _animator;
        private BattleStateMachine  _battle;

        private MonsterData _currentMonster;
        private float       _idleClipLength = 1f;
        private Vector3     _baseLocalPos;
        private readonly System.Collections.Generic.HashSet<CellEffectFeedback> _sfxPlayedThisBeat = new();

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _battle   = Object.FindAnyObjectByType<BattleStateMachine>();
            if (_battle != null)
            {
                _battle.OnBattleStarted          += SetMonster;
                _battle.OnBeatUnitFired          += OnBeatUnit;
                _battle.OnMonsterHit             += OnHit;
                _battle.OnMonsterCellEffectFired += PlayCellEffectFeedback;
                _battle.OnBossPhaseChanged       += OnBossPhaseChanged;
            }
        }

        private void Start()
        {
            _baseLocalPos = transform.localPosition;
            if (_battle != null && _battle.CurrentMonster != null)
                SetMonster(_battle.CurrentMonster);
        }

        private void OnDestroy()
        {
            if (_battle != null)
            {
                _battle.OnBattleStarted          -= SetMonster;
                _battle.OnBeatUnitFired          -= OnBeatUnit;
                _battle.OnMonsterHit             -= OnHit;
                _battle.OnMonsterCellEffectFired -= PlayCellEffectFeedback;
                _battle.OnBossPhaseChanged       -= OnBossPhaseChanged;
            }
        }

        private void SetMonster(MonsterData data)
        {
            _currentMonster = data;
            _baseLocalPos   = transform.localPosition;
            if (_animator == null || _baseController == null) return;

            // MonsterBaseAnimator 상태머신을 공유하고 몬스터별 클립만 교체
            var overrideCtrl = new AnimatorOverrideController(_baseController);
            overrideCtrl["Idle"]  = data.idleClip;
            overrideCtrl["Hurt"]  = data.hurtClip;
            overrideCtrl["Death"] = data.deathClip;
            _animator.runtimeAnimatorController = overrideCtrl;

            // Idle State 클립 길이 자동 추출
            _animator.Play("Idle", 0, 0f);
            _animator.Update(0f);
            _idleClipLength = _animator.GetCurrentAnimatorStateInfo(0).length;
        }

        // BeatUnit 발화마다 Idle을 해당 BeatUnit 길이에 정확히 맞춰 재생.
        private void OnBeatUnit(double beatDurationSec)
        {
            if (_currentMonster == null || _animator == null || beatDurationSec <= 0) return;

            _animator.speed = _idleClipLength / (float)beatDurationSec;
            _animator.Play(Animator.StringToHash("Idle"), 0, 0f);
            _sfxPlayedThisBeat.Clear();
        }

        private void OnHit()
        {
            if (_animator == null) return;
            _animator.SetTrigger(HurtHash);
        }

        public void PlayDeath()
        {
            if (_animator == null) return;
            _animator.SetTrigger(DeathHash);
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
