using System.Collections;
using BeatHero.Audio;
using BeatHero.Data;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

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
        private static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");
        private static readonly int FlashColorId  = Shader.PropertyToID("_FlashColor");

        // 모든 몬스터가 공유하는 상태머신. Inspector에서 MonsterBaseAnimator 연결.
        [SerializeField] private RuntimeAnimatorController _baseController;
        // MonsterBaseAnimator 각 State에 할당된 베이스 클립 — override 키로 사용.
        [SerializeField] private AnimationClip _baseIdleClip;
        [SerializeField] private AnimationClip _baseHurtClip;
        [SerializeField] private AnimationClip _baseDeathClip;
        [SerializeField] private float _shakeAmount   = 0.15f;
        [SerializeField] private float _shakeDuration = 0.2f;

        [Header("Hit FX - 화이트 플래시")]
        [SerializeField] private Color _flashColor = Color.white;
        [SerializeField, Range(0f, 1f)] private float _flashStrength = 1f;
        [SerializeField] private float _flashDuration = 0.08f;

        [Header("Hit FX - 넉백")]
        [SerializeField] private float   _knockbackDistance = 0.25f;
        [SerializeField] private Vector2 _knockbackDir      = new(0f, 1f); // 탑뷰: +Y = 플레이어 반대쪽(뒤)
        [SerializeField] private float   _knockbackOutTime  = 0.05f;
        [SerializeField] private float   _knockbackBackTime = 0.12f;

        [Header("Hit FX - 스쿼시&스트레치")]
        [SerializeField] private Vector2 _squashScale    = new(1.15f, 0.85f); // 가로↑ 세로↓
        [SerializeField] private float   _squashOutTime  = 0.05f;
        [SerializeField] private float   _squashBackTime = 0.12f;

        [Header("Hit FX - 히트 파티클(AnimationClip 직접 할당)")]
        [SerializeField] private Animator      _hitVfxAnimator; // 전용 자식 VFX 오브젝트의 Animator
        [SerializeField] private AnimationClip _hitVfxClip;     // 인스펙터에서 직접 할당
        [SerializeField] private Vector3       _hitVfxOffset = Vector3.zero;

        private Animator            _animator;
        private BattleStateMachine  _battle;
        private SpriteRenderer      _renderer;
        private MaterialPropertyBlock _mpb;

        private MonsterData _currentMonster;
        private float       _idleClipLength = 1f;
        private Vector3     _baseLocalPos;
        private Vector3     _baseScale = Vector3.one;
        private readonly System.Collections.Generic.HashSet<CellEffectFeedback> _sfxPlayedThisBeat = new();

        private Coroutine     _flashRoutine;
        private Coroutine     _knockbackRoutine;
        private Coroutine     _squashRoutine;
        private Coroutine     _hitVfxStopRoutine;
        private PlayableGraph _hitVfxGraph;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _renderer = GetComponent<SpriteRenderer>();
            _mpb      = new MaterialPropertyBlock();
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
            _baseScale    = transform.localScale;
            if (_hitVfxAnimator != null) _hitVfxAnimator.gameObject.SetActive(false);
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
            if (_hitVfxGraph.IsValid()) _hitVfxGraph.Destroy();
        }

        private void SetMonster(MonsterData data)
        {
            _currentMonster = data;
            _baseLocalPos   = transform.localPosition;
            if (_animator == null || _baseController == null) return;

            // MonsterBaseAnimator 상태머신을 공유하고 몬스터별 클립만 교체.
            // 클립 이름이 아닌 오브젝트 참조를 키로 사용해 이름 의존성 제거.
            var overrideCtrl = new AnimatorOverrideController(_baseController);
            if (data.idleClip  != null && _baseIdleClip  != null) overrideCtrl[_baseIdleClip]  = data.idleClip;
            if (data.hurtClip  != null && _baseHurtClip  != null) overrideCtrl[_baseHurtClip]  = data.hurtClip;
            if (data.deathClip != null && _baseDeathClip != null) overrideCtrl[_baseDeathClip] = data.deathClip;
            _animator.runtimeAnimatorController = overrideCtrl;

            // Play(0f) + Update(0f): Idle 첫 프레임을 SpriteRenderer에 즉시 반영 + 클립 길이 추출
            if (data.idleClip != null)
            {
                _animator.Play("Idle", 0, 0f);
                _animator.Update(0f);
                _idleClipLength = _animator.GetCurrentAnimatorStateInfo(0).length;
            }
        }

        // BeatUnit 발화마다 Idle 재생. gridEffect 없는 None 비트는 첫 프레임에 정지.
        private void OnBeatUnit(double beatDurationSec, bool hasGridEffect)
        {
            if (_currentMonster == null || _animator == null || beatDurationSec <= 0) return;

            if (!hasGridEffect)
            {
                _animator.speed = 0f;
                _animator.Play(Animator.StringToHash("Idle"), 0, 0f);
                _animator.Update(0f);
                return;
            }

            _animator.speed = _idleClipLength / (float)beatDurationSec;
            _animator.Play(Animator.StringToHash("Idle"), 0, 0f);
            _sfxPlayedThisBeat.Clear();
        }

        private void OnHit()
        {
            if (_animator != null) _animator.SetTrigger(HurtHash);

            // 4종 타격 피드백 동시 발동. 연타 대비 각 코루틴은 재시작.
            RestartRoutine(ref _flashRoutine,     FlashRoutine());
            RestartRoutine(ref _knockbackRoutine, KnockbackRoutine());
            RestartRoutine(ref _squashRoutine,    SquashRoutine());
            PlayHitVfx();
        }

        // 돌고 있으면 멈추고 다시 시작 — 차지 등 연타 시 안전.
        private void RestartRoutine(ref Coroutine handle, IEnumerator routine)
        {
            if (handle != null) StopCoroutine(handle);
            handle = StartCoroutine(routine);
        }

        // 1. 화이트 플래시: _FlashAmount를 _flashStrength→0으로 보간.
        private IEnumerator FlashRoutine()
        {
            if (_renderer == null) { _flashRoutine = null; yield break; }
            float elapsed = 0f;
            while (elapsed < _flashDuration)
            {
                elapsed += Time.deltaTime;
                float t = _flashDuration > 0f ? elapsed / _flashDuration : 1f;
                SetFlash(Mathf.Lerp(_flashStrength, 0f, t));
                yield return null;
            }
            SetFlash(0f);
            _flashRoutine = null;
        }

        private void SetFlash(float amount)
        {
            _renderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(FlashColorId, _flashColor);
            _mpb.SetFloat(FlashAmountId, amount);
            _renderer.SetPropertyBlock(_mpb);
        }

        // 2. 넉백: 뒤로 빠르게 밀렸다가 EaseOut으로 복귀.
        private IEnumerator KnockbackRoutine()
        {
            Vector3 target = _baseLocalPos + (Vector3)(_knockbackDir.normalized * _knockbackDistance);
            float elapsed = 0f;
            while (elapsed < _knockbackOutTime)
            {
                elapsed += Time.deltaTime;
                float t = _knockbackOutTime > 0f ? elapsed / _knockbackOutTime : 1f;
                transform.localPosition = Vector3.Lerp(_baseLocalPos, target, t);
                yield return null;
            }
            transform.localPosition = target;

            elapsed = 0f;
            while (elapsed < _knockbackBackTime)
            {
                elapsed += Time.deltaTime;
                float t = _knockbackBackTime > 0f ? elapsed / _knockbackBackTime : 1f;
                t = 1f - (1f - t) * (1f - t); // EaseOutQuad
                transform.localPosition = Vector3.Lerp(target, _baseLocalPos, t);
                yield return null;
            }
            transform.localPosition = _baseLocalPos;
            _knockbackRoutine = null;
        }

        // 3. 스쿼시&스트레치: 찌그러졌다가 EaseOut으로 복귀.
        private IEnumerator SquashRoutine()
        {
            Vector3 target = Vector3.Scale(_baseScale, new Vector3(_squashScale.x, _squashScale.y, 1f));
            float elapsed = 0f;
            while (elapsed < _squashOutTime)
            {
                elapsed += Time.deltaTime;
                float t = _squashOutTime > 0f ? elapsed / _squashOutTime : 1f;
                transform.localScale = Vector3.Lerp(_baseScale, target, t);
                yield return null;
            }
            transform.localScale = target;

            elapsed = 0f;
            while (elapsed < _squashBackTime)
            {
                elapsed += Time.deltaTime;
                float t = _squashBackTime > 0f ? elapsed / _squashBackTime : 1f;
                t = 1f - (1f - t) * (1f - t); // EaseOutQuad
                transform.localScale = Vector3.Lerp(target, _baseScale, t);
                yield return null;
            }
            transform.localScale = _baseScale;
            _squashRoutine = null;
        }

        // 4. 히트 파티클: 할당된 AnimationClip을 컨트롤러 없이 단발 재생.
        private void PlayHitVfx()
        {
            if (_hitVfxAnimator == null || _hitVfxClip == null) return;

            _hitVfxAnimator.transform.position = transform.position + _hitVfxOffset;
            if (!_hitVfxAnimator.gameObject.activeSelf) _hitVfxAnimator.gameObject.SetActive(true);

            if (_hitVfxGraph.IsValid()) _hitVfxGraph.Destroy();
            AnimationPlayableUtilities.PlayClip(_hitVfxAnimator, _hitVfxClip, out _hitVfxGraph);

            if (_hitVfxStopRoutine != null) StopCoroutine(_hitVfxStopRoutine);
            _hitVfxStopRoutine = StartCoroutine(StopHitVfxAfter(_hitVfxClip.length));
        }

        private IEnumerator StopHitVfxAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (_hitVfxGraph.IsValid()) _hitVfxGraph.Destroy();
            if (_hitVfxAnimator != null) _hitVfxAnimator.gameObject.SetActive(false);
            _hitVfxStopRoutine = null;
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
