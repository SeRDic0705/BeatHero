using System;
using BeatHero.Combat;
using BeatHero.Data;
using UnityEngine;

namespace BeatHero.Core
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public int CurrentFloor { get; private set; }
        public int PlayerHp { get; private set; }
        public int PlayerMaxHp { get; private set; }

        public event Action<int> OnFloorChanged;
        public event Action OnGameOver;
        public event Action OnFloorCleared;

        [SerializeField] private FloorData _floorData;
        [SerializeField] private BattleStateMachine _battle;

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

        private void Start()
        {
            if (_battle != null && _floorData != null)
                StartRun(100);
        }

        public void StartRun(int maxHp)
        {
            PlayerMaxHp = maxHp;
            PlayerHp = maxHp;
            CurrentFloor = 1;
            OnFloorChanged?.Invoke(CurrentFloor);
            StartBattleForCurrentFloor();
        }

        private void StartBattleForCurrentFloor()
        {
            if (_battle == null || _floorData == null) return;
            var monster = _floorData.GetMonster(CurrentFloor);
            if (monster != null) _battle.StartBattle(monster);
        }

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

        public void CompleteFloor()
        {
            CurrentFloor++;
            OnFloorCleared?.Invoke();
            OnFloorChanged?.Invoke(CurrentFloor);
            StartBattleForCurrentFloor();
        }

        public void RestartRun()
        {
            StartRun(PlayerMaxHp);
        }
    }
}
