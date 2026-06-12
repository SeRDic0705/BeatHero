using System;
using System.Collections;
using BeatHero.Audio;
using BeatHero.Combat;
using BeatHero.Data;
using BeatHero.Player;
using BeatHero.UI;
using UnityEngine;

namespace BeatHero.Core
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public int CurrentFloor  { get; private set; }
        public int PlayerHp      { get; private set; }
        public int PlayerMaxHp   { get; private set; }

        public int TotalBeats { get; private set; }

        public event Action<int> OnFloorChanged;
        public event Action      OnGameOver;
        public event Action      OnFloorCleared;

        [SerializeField] private FloorData          _floorData;
        [SerializeField] private BattleStateMachine _battle;
        [SerializeField] private ResultScreen       _resultScreen;

        [Header("Transition Timing")]
        [SerializeField] private float _rushDuration       = 1f;
        [SerializeField] private float _exitDuration       = 1f;
        [SerializeField] private float _blackoutMinWait    = 0.5f;
        [SerializeField] private float _enterDuration      = 1f;

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

            if (_battle != null)
                _battle.OnEffectiveBeatFired += () => TotalBeats++;
        }

        private void Start()
        {
            if (SceneLoader.Instance != null)
                SceneLoader.Instance.OnSceneOpened += OnSceneOpened;
            else if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "GameScene")
                StartRun(100);
        }

        private void OnSceneOpened()
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "GameScene")
                StartRun(100);
        }

        public void StartRun(int maxHp)
        {
            // DontDestroyOnLoad이므로 씬 재로드 후 씬-로컬 레퍼런스가 파괴됨 → 다시 찾아 갱신
            // FindObjectsInactive.Include 필수 — ResultCanvas는 기본값 inactive이므로 Exclude하면 못 찾음
            if (_battle == null)
            {
                _battle = UnityEngine.Object.FindAnyObjectByType<BattleStateMachine>(FindObjectsInactive.Include);
                if (_battle != null)
                    _battle.OnEffectiveBeatFired += () => TotalBeats++;
            }
            if (_resultScreen == null)
                _resultScreen = UnityEngine.Object.FindAnyObjectByType<ResultScreen>(FindObjectsInactive.Include);

            InputReader.Instance?.SwitchToGameMap();
            PlayerMaxHp  = maxHp;
            PlayerHp     = maxHp;
            TotalBeats   = 0;
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

        private bool IsBossFloor(int floor) => _floorData.GetMonster(floor) is Data.BossMonsterData;
        private bool IsFinalFloor(int floor) => _floorData != null && floor == _floorData.floors.Count;

        private IEnumerator CompleteFloorRoutine()
        {
            int completedFloor = CurrentFloor;
            var player         = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
            var monsterView    = UnityEngine.Object.FindAnyObjectByType<MonsterView>();

            InputReader.Instance?.SwitchToUIMap();

            // 최후의 일격 전진
            if (player != null && monsterView != null)
                yield return StartCoroutine(player.RushTo(monsterView.transform.position, _rushDuration));

            // 사망 SFX
            AudioManager.Instance?.PlaySFX(_battle.CurrentMonster?.deathSfx);

            // 오른쪽 퇴장 + 아이리스 닫힘 동시
            if (SceneLoader.Instance != null && player != null)
            {
                var exitCoroutine  = StartCoroutine(player.ExitRight(_exitDuration));
                var wipeOutRoutine = StartCoroutine(SceneLoader.Instance.WipeOut(_exitDuration));
                yield return exitCoroutine;
                yield return wipeOutRoutine;
            }

            // 최종 클리어
            if (IsFinalFloor(completedFloor))
            {
                OnFloorCleared?.Invoke();
                if (_resultScreen != null)
                {
                    bool done = false;
                    _resultScreen.Show(ResultScreen.Mode.FinalClear, completedFloor, TotalBeats,
                        onTitle: () => { done = true; });

                    yield return new WaitUntil(() => done);
                    SceneLoader.Instance?.LoadTitle();
                }
                yield break;
            }

            // 암전 — 다음 층 세팅
            CurrentFloor++;
            OnFloorCleared?.Invoke();
            OnFloorChanged?.Invoke(CurrentFloor);
            var monster = _floorData.GetMonster(CurrentFloor);
            if (monster != null)
                _battle.SetFloorData(monster);

            // 보스 클리어 결과창 (암전 중 표시)
            if (IsBossFloor(completedFloor) && _resultScreen != null)
            {
                bool confirmed = false;
                _resultScreen.Show(ResultScreen.Mode.BossClear, completedFloor, TotalBeats,
                    onConfirm: () => confirmed = true);
                yield return new WaitUntil(() => confirmed);
            }

            // 암전 최소 대기
            yield return new WaitForSeconds(_blackoutMinWait);

            // centerPos 캡처 후 왼쪽 등장 + 아이리스 열림
            if (player != null)
            {
                Vector3 centerPos = player.transform.position;
                player.transform.position = GetLeftEdge(player.transform.position);

                if (SceneLoader.Instance != null)
                {
                    var enterCoroutine = StartCoroutine(player.EnterFromLeft(centerPos, _enterDuration));
                    var wipeInRoutine  = StartCoroutine(SceneLoader.Instance.WipeIn(_enterDuration));
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
            OnGameOver?.Invoke();
            InputReader.Instance?.SwitchToUIMap();

            if (_resultScreen != null)
            {
                bool chosen = false;
                bool retry  = false;
                _resultScreen.Show(ResultScreen.Mode.GameOver, CurrentFloor, TotalBeats,
                    onRetry: () => { chosen = true; retry = true; },
                    onTitle: () => { chosen = true; retry = false; });
                yield return new WaitUntil(() => chosen);

                if (retry)
                {
                    if (SceneLoader.Instance != null)
                        yield return SceneLoader.Instance.DoTransition(() => StartRun(PlayerMaxHp));
                    else
                        StartRun(PlayerMaxHp);
                }
                else
                {
                    SceneLoader.Instance?.LoadTitle();
                }
            }
            else
            {
                if (SceneLoader.Instance != null)
                    yield return SceneLoader.Instance.DoTransition(() => StartRun(PlayerMaxHp));
                else
                    StartRun(PlayerMaxHp);
            }
        }
    }
}
