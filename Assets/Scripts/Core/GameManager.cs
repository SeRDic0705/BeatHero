using System;
using UnityEngine;

namespace BeatHero.Core
{
    // 런 전체 상태 관리: 현재 층, 플레이어 HP, 사망/클리어 처리
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public int CurrentFloor { get; private set; }
        public int PlayerHp { get; private set; }
        public int PlayerMaxHp { get; private set; }

        public event Action<int> OnFloorChanged;
        public event Action OnGameOver;
        public event Action OnFloorCleared;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void StartRun(int maxHp)
        {
            PlayerMaxHp = maxHp;
            PlayerHp = maxHp;
            CurrentFloor = 1;
            OnFloorChanged?.Invoke(CurrentFloor);
        }

        // 피해 적용 — 0 이하면 게임오버 트리거
        public void ApplyDamage(int amount)
        {
            PlayerHp = Mathf.Max(0, PlayerHp - amount);
            if (PlayerHp <= 0)
                OnGameOver?.Invoke();
        }

        public void HealPlayer(int amount)
        {
            PlayerHp = Mathf.Min(PlayerMaxHp, PlayerHp + amount);
        }

        // 층 클리어 — HP 유지, 다음 층으로
        public void CompleteFloor()
        {
            CurrentFloor++;
            OnFloorCleared?.Invoke();
            OnFloorChanged?.Invoke(CurrentFloor);
        }

        // 게임오버 후 1층 재시작 (씬 전환은 SceneLoader가 처리)
        public void RestartRun()
        {
            StartRun(PlayerMaxHp);
        }
    }
}
