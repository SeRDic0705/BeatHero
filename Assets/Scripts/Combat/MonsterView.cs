using System.Collections;
using BeatHero.Audio;
using BeatHero.Core;
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
        private static readonly int HurtHash       = Animator.StringToHash("hurt");
        private static readonly int HurtStateHash  = Animator.StringToHash("Hurt");
        private static readonly int DeathStateHash = Animator.StringToHash("Death");
        private static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");
        private static readonly int FlashColorId  = Shader.PropertyToID("_FlashColor");

        private const int LEAD_IN_BEATS = 4; // 리드인 1마디 = 4박(Conductor.BEATS_PER_MEASURE와 동일)

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

        [Header("Hit FX - 참격 VFX (처치 피니셔와 공통)")]
        [SerializeField] private SlashVfxPlayer _slashVfx;     // 평타·처치가 공유하는 참격 재생기
        [SerializeField] private AnimationClip  _hitSlashClip; // 평타 참격 클립(SlashVfx.anim)

        private Animator            _animator;
        private BattleStateMachine  _battle;
        private Conductor           _conductor;
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
        private Coroutine     _leadInRoutine;

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
                _battle.OnMonsterDefeated        += FreezeOnHurt;
                _battle.OnMonsterCellEffectFired += PlayCellEffectFeedback;
                _battle.OnBossPhaseChanged       += OnBossPhaseChanged;
            }
            _conductor = Object.FindAnyObjectByType<Conductor>();
            if (_conductor != null) _conductor.OnFloorLeadIn += OnFloorLeadIn;
        }

        private void Start()
        {
            _baseLocalPos = transform.localPosition;
            _baseScale    = transform.localScale;
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
                _battle.OnMonsterDefeated        -= FreezeOnHurt;
                _battle.OnMonsterCellEffectFired -= PlayCellEffectFeedback;
                _battle.OnBossPhaseChanged       -= OnBossPhaseChanged;
            }
            if (_conductor != null) _conductor.OnFloorLeadIn -= OnFloorLeadIn;
        }

        private void SetMonster(MonsterData data)
        {
            _currentMonster = data;
            _baseLocalPos   = transform.localPosition;
            if (_animator == null || _baseController == null) return;

            // 이전 층에서 FreezeOnHurt로 speed=0 고정됐을 수 있으므로 복원.
            _animator.speed = 1f;

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

        // 층 시작(StartFloor) ~ 첫 비트(beat0) 사이 1마디 동안 Idle을 박자에 맞춰 4번 재생.
        // beat0부터는 OnBeatUnit(프레이즈)이 이어받으므로 그 전까지만 흔든다.
        private void OnFloorLeadIn(double beat0Dsp, float bpm)
        {
            if (_leadInRoutine != null) StopCoroutine(_leadInRoutine);
            _leadInRoutine = StartCoroutine(LeadInBob(beat0Dsp, bpm));
        }

        private IEnumerator LeadInBob(double beat0Dsp, float bpm)
        {
            if (_currentMonster == null || _animator == null || bpm <= 0f) yield break;
            double secPerBeat = 60.0 / bpm;
            for (int i = 0; i < LEAD_IN_BEATS; i++)
            {
                double beatDsp = beat0Dsp - (LEAD_IN_BEATS - i) * secPerBeat;
                while (AudioSettings.dspTime < beatDsp) yield return null;
                if (AudioSettings.dspTime >= beat0Dsp) break; // beat0 도달 — 프레이즈가 이어받음
                _animator.speed = (float)(_idleClipLength / secPerBeat);
                _animator.Play(Animator.StringToHash("Idle"), 0, 0f);
            }
            _leadInRoutine = null;
        }

        private void OnHit(bool isLethal)
        {
            if (_animator != null) _animator.SetTrigger(HurtHash);

            // 타격 피드백 동시 발동. 연타 대비 각 코루틴은 재시작.
            RestartRoutine(ref _flashRoutine,     FlashRoutine());
            RestartRoutine(ref _knockbackRoutine, KnockbackRoutine());
            RestartRoutine(ref _squashRoutine,    SquashRoutine());

            // 평타 참격 — 치명타(처치 타격) 땐 생략하고 돌진 피니셔의 치명타 참격만 보여준다.
            if (!isLethal) _slashVfx?.Play(_hitSlashClip, transform.position);
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

        // HP가 0이 된 순간 호출 — Hurt 클립 마지막 프레임에서 정지(피격 포즈 유지).
        // 넉백·스쿼시 등 transform 기반 FX는 그 위에서 계속 재생된다.
        private void FreezeOnHurt()
        {
            if (_animator == null) return;
            // OnHit에서 세팅된 hurt 트리거가 남아 있으면 다음 프레임에 Hurt가 한 번 더
            // 발동(2회 재생)하고 이후 death 전이와도 경합하므로 반드시 소비시킨다.
            _animator.ResetTrigger(HurtHash);
            _animator.Play(HurtStateHash, 0, 1f); // Hurt 상태 마지막 프레임으로 점프
            _animator.Update(0f);                 // SpriteRenderer에 즉시 반영
            _animator.speed = 0f;                 // 정지
        }

        public void PlayDeath()
        {
            if (_animator == null) return;
            _animator.speed = 1f;              // FreezeOnHurt로 멈춰 있던 상태 해제
            _animator.ResetTrigger(HurtHash);  // 잔여 hurt 트리거 제거(Death가 묻히는 것 방지)
            // 트리거 대신 Death 상태를 직접 재생 — 전이 경합 없이 확실히 재생.
            _animator.Play(DeathStateHash, 0, 0f);
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
