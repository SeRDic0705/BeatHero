using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace BeatHero.Player
{
    // 플레이어 런타임 스탯 — HP·마나·보호막 보유 및 이벤트 발생
    public class PlayerController : MonoBehaviour
    {
        private const int MAX_MANA = 10;

        public int Hp      { get; private set; }
        public int MaxHp   { get; private set; }
        public int Mana    { get; private set; }
        public int MaxMana => MAX_MANA;
        public bool HasShield { get; private set; }

        [SerializeField] private PlayerAnimationController _anim;

        [Header("참격 VFX (몬스터 통과 시 1회)")]
        [SerializeField] private Animator      _slashVfxAnimator; // 독립 VFX 오브젝트의 Animator
        [SerializeField] private AnimationClip _slashVfxClip;     // 인스펙터 직접 할당
        [SerializeField] private Vector3       _slashVfxOffset = Vector3.zero;

        private PlayableGraph _slashGraph;
        private Coroutine     _slashStopRoutine;

        public event Action<int, int> OnHpChanged;   // (current, max)
        public event Action<int>      OnManaChanged;  // current
        public event Action           OnDeath;
        public event Action           OnShieldGained;
        public event Action           OnShieldLost;

        public void Initialize(int maxHp)
        {
            MaxHp     = maxHp;
            Hp        = maxHp;
            Mana      = 0;
            HasShield = false;
            _anim?.ResetState();
            // 초기 HP/마나를 HUD에 알림 — 미발화 시 첫 피격 전까지 체력바가 0/0으로 표시됨
            OnHpChanged?.Invoke(Hp, MaxHp);
            OnManaChanged?.Invoke(Mana);
        }

        // 암전 직후 등 비주얼만 즉시 초기화할 때 사용 (스탯 건드리지 않음)
        public void ResetVisuals() => _anim?.ResetState();

        // 피해 적용 — 보호막 있으면 1회 흡수
        public void TakeDamage(int amount)
        {
            if (HasShield)
            {
                HasShield = false;
                OnShieldLost?.Invoke();
                return;
            }
            Hp = Mathf.Max(0, Hp - amount);
            OnHpChanged?.Invoke(Hp, MaxHp);
            if (Hp <= 0)
            {
                _anim?.TriggerDeath();
                OnDeath?.Invoke();
            }
            else
            {
                _anim?.TriggerHurt();
            }
        }

        // 차지 취소·피격 시 호출해서 외부에서도 차지 취소 데미지 반영 가능
        public void AddMana(int amount)
        {
            Mana = Mathf.Min(MAX_MANA, Mana + amount);
            OnManaChanged?.Invoke(Mana);
        }

        public bool SpendMana(int amount)
        {
            if (Mana < amount) return false;
            Mana -= amount;
            OnManaChanged?.Invoke(Mana);
            return true;
        }

        public void GainShield()
        {
            HasShield = true;
            OnShieldGained?.Invoke();
        }

        // 층 클리어 시 마나 리셋
        public void ResetMana()
        {
            Mana = 0;
            OnManaChanged?.Invoke(Mana);
        }

        public IEnumerator RushTo(Vector3 target, float duration)
        {
            _anim?.SetRunning(true);
            Vector3 start = transform.position;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                transform.position = Vector3.Lerp(start, target, t / duration);
                yield return null;
            }
            transform.position = target;
            _anim?.SetRunning(false);
        }

        // 최후의 일격 돌진 — finalAttack 애니메이션 + ease 곡선(기본 ease-out: 초반 빠르고 후반 느림).
        // 돌진 중 플레이어 x가 몬스터 x를 지나치는 순간(좌표 교차) 참격 VFX 1회 재생.
        public IEnumerator FinalAttackDash(Vector3 target, float duration, AnimationCurve ease, Vector3 monsterPos)
        {
            _anim?.TriggerFinalAttack();
            Vector3 start = transform.position;
            float crossX  = monsterPos.x;
            float prevX   = start.x;
            bool  slashed = false;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float n = duration > 0f ? t / duration : 1f;
                float u = ease != null ? ease.Evaluate(n) : n;
                transform.position = Vector3.Lerp(start, target, u);

                // prevX와 현재 x가 crossX를 사이에 두면(부호 반전·일치) 통과 → 1회 재생
                if (!slashed && (prevX - crossX) * (transform.position.x - crossX) <= 0f)
                {
                    slashed = true;
                    PlaySlashVfx(monsterPos);
                }
                prevX = transform.position.x;
                yield return null;
            }
            transform.position = target;
        }

        // 참격 VFX — 할당된 AnimationClip을 컨트롤러 없이 단발 재생.
        private void PlaySlashVfx(Vector3 worldPos)
        {
            if (_slashVfxAnimator == null || _slashVfxClip == null) return;

            _slashVfxAnimator.transform.position = worldPos + _slashVfxOffset;
            if (!_slashVfxAnimator.gameObject.activeSelf) _slashVfxAnimator.gameObject.SetActive(true);

            if (_slashGraph.IsValid()) _slashGraph.Destroy();
            AnimationPlayableUtilities.PlayClip(_slashVfxAnimator, _slashVfxClip, out _slashGraph);

            if (_slashStopRoutine != null) StopCoroutine(_slashStopRoutine);
            _slashStopRoutine = StartCoroutine(StopSlashVfxAfter(_slashVfxClip.length));
        }

        private IEnumerator StopSlashVfxAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (_slashGraph.IsValid()) _slashGraph.Destroy();
            if (_slashVfxAnimator != null) _slashVfxAnimator.gameObject.SetActive(false);
            _slashStopRoutine = null;
        }

        private void OnDestroy()
        {
            if (_slashGraph.IsValid()) _slashGraph.Destroy();
        }

        public IEnumerator ExitRight(float duration)
        {
            Vector3 start = transform.position;
            Vector3 target = ScreenEdgeWorld(1.3f, start);
            yield return StartCoroutine(RushTo(target, duration));
        }

        public IEnumerator EnterFromLeft(Vector3 target, float duration)
        {
            transform.position = ScreenEdgeWorld(-0.3f, target);
            yield return StartCoroutine(RushTo(target, duration));
        }

        private static Vector3 ScreenEdgeWorld(float viewportX, Vector3 reference)
        {
            if (Camera.main == null) return reference;
            float depth = Mathf.Abs(Camera.main.transform.position.z - reference.z);
            Vector3 edge = Camera.main.ViewportToWorldPoint(new Vector3(viewportX, 0.5f, depth));
            return new Vector3(edge.x, reference.y, reference.z);
        }
    }
}
