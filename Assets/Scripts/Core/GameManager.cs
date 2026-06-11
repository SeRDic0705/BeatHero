using System;
using System.Collections;
using BeatHero.Audio;
using BeatHero.Combat;
using BeatHero.Data;
using BeatHero.Player;
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
            var player      = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
            var monsterView = UnityEngine.Object.FindAnyObjectByType<MonsterView>();

            InputReader.Instance?.SwitchToUIMap();

            // 최후의 일격 전진
            if (player != null && monsterView != null)
                yield return StartCoroutine(player.RushTo(monsterView.transform.position, 1f));

            // 사망 SFX
            AudioManager.Instance?.PlaySFX(_battle.CurrentMonster?.deathSfx);

            // 오른쪽 퇴장 + 아이리스 닫힘 동시
            if (SceneLoader.Instance != null && player != null)
            {
                var exitCoroutine  = StartCoroutine(player.ExitRight(1f));
                var wipeOutRoutine = StartCoroutine(SceneLoader.Instance.WipeOut(1f));
                yield return exitCoroutine;
                yield return wipeOutRoutine;
            }

            // 암전 — 다음 층 세팅
            CurrentFloor++;
            OnFloorCleared?.Invoke();
            OnFloorChanged?.Invoke(CurrentFloor);
            var monster = _floorData.GetMonster(CurrentFloor);
            if (monster != null)
                _battle.SetFloorData(monster); // 내부에서 player.transform.position = centerPos

            // centerPos 캡처 후 플레이어 왼쪽 밖으로 배치
            if (player != null)
            {
                Vector3 centerPos = player.transform.position;

                // 아이리스 열림 + 왼쪽에서 등장 동시
                if (SceneLoader.Instance != null)
                {
                    player.transform.position = GetLeftEdge(player.transform.position);
                    var enterCoroutine = StartCoroutine(player.EnterFromLeft(centerPos, 1f));
                    var wipeInRoutine  = StartCoroutine(SceneLoader.Instance.WipeIn(1f));
                    yield return enterCoroutine;
                    yield return wipeInRoutine;
                }
            }

            InputReader.Instance?.SwitchToGameMap();

            if (monster != null)
                _battle.StartFloor();
        }

        private static Vector3 GetLeftEdge(Vector3 reference)
        {
            if (Camera.main == null) return reference;
            float depth = Mathf.Abs(Camera.main.transform.position.z - reference.z);
            Vector3 edge = Camera.main.ViewportToWorldPoint(new Vector3(-0.3f, 0.5f, depth));
            return new Vector3(edge.x, reference.y, reference.z);
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
