using System;
using System.Collections;
using UnityEngine;

namespace BeatHero.Player
{
    // 플레이어 런타임 스탯 — HP·마나·보호막 보유 및 이벤트 발생
    public class PlayerController : MonoBehaviour
    {
        private const int MAX_MANA = 5;

        public int Hp      { get; private set; }
        public int MaxHp   { get; private set; }
        public int Mana    { get; private set; }
        public bool HasShield { get; private set; }

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
        }

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
            if (Hp <= 0) OnDeath?.Invoke();
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
            Vector3 start = transform.position;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                transform.position = Vector3.Lerp(start, target, t / duration);
                yield return null;
            }
            transform.position = target;
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
