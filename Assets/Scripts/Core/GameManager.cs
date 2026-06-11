using System;
using System.Collections;
using BeatHero.Combat;
using BeatHero.Data;
using UnityEngine;

namespace BeatHero.Core
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public int CurrentFloor  { get; private set; }
        public int PlayerHp      { get; private set; }
        public int PlayerMaxHp   { get; private set; }

        public event Action<int> OnFloorChanged;
        public event Action      OnGameOver;
        public event Action      OnFloorCleared;

        [SerializeField] private FloorData          _floorData;
        [SerializeField] private BattleStateMachine _battle;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            if (_battle == null || _floorData == null) return;

            // SceneLoader를 통해 진입한 경우 → 오프닝 완료 후 시작
            if (SceneLoader.Instance != null)
                SceneLoader.Instance.OnSceneOpened += () => StartRun(100);
            else
                StartRun(100);
        }

        public void StartRun(int maxHp)
        {
            InputReader.Instance?.SwitchToGameMap();
            PlayerMaxHp = maxHp;
            PlayerHp    = maxHp;
            CurrentFloor = 1;
            OnFloorChanged?.Invoke(CurrentFloor);
            StartBattleForCurrentFloor();
        }

        private void StartBattleForCurrentFloor()
        {
            if (_battle == null || _floorData == null) return;
            var monster = _floorData.GetMonster(CurrentFloor);
            if (monster == null) return;

            _battle.SetFloorData(monster);
            _battle.StartFloor();
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
            StartCoroutine(CompleteFloorRoutine());
        }

        private IEnumerator CompleteFloorRoutine()
        {
            void Advance()
            {
                CurrentFloor++;
                OnFloorCleared?.Invoke();
                OnFloorChanged?.Invoke(CurrentFloor);
                StartBattleForCurrentFloor();
            }

            if (SceneLoader.Instance != null)
                yield return SceneLoader.Instance.DoTransition(Advance);
            else
                Advance();
        }

        public void RestartRun()
        {
            StartCoroutine(RestartRunRoutine());
        }

        private IEnumerator RestartRunRoutine()
        {
            if (SceneLoader.Instance != null)
                yield return SceneLoader.Instance.DoTransition(() => StartRun(PlayerMaxHp));
            else
                StartRun(PlayerMaxHp);
        }
    }
}
