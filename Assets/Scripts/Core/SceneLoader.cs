using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BeatHero.Core
{
    public class SceneLoader : MonoBehaviour
    {
        public static SceneLoader Instance { get; private set; }

        private const string SCENE_TITLE   = "TitleScene";
        private const string SCENE_GAME    = "GameScene";
        private const float  WIPE_DURATION = 0.5f;

        [SerializeField] private Image _wipeImage;

        private Material _wipeMat;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (_wipeImage != null)
            {
                _wipeMat = new Material(_wipeImage.material);
                _wipeImage.material = _wipeMat;
                _wipeImage.gameObject.SetActive(false);
            }
        }

        public void LoadGame()  => StartCoroutine(LoadWithWipe(SCENE_GAME));
        public void LoadTitle() => StartCoroutine(LoadWithWipe(SCENE_TITLE));

        // 씬 이동 없는 와이프 전환 — 층 클리어·게임 오버 등에서 사용
        public IEnumerator DoTransition(Action onMidpoint)
        {
            yield return StartCoroutine(AnimateWipe(1f, 0f));
            onMidpoint?.Invoke();
            yield return StartCoroutine(AnimateWipe(0f, 1f));
        }

        private IEnumerator LoadWithWipe(string sceneName)
        {
            yield return StartCoroutine(AnimateWipe(1f, 0f));
            yield return SceneManager.LoadSceneAsync(sceneName);
            yield return StartCoroutine(AnimateWipe(0f, 1f));
        }

        private IEnumerator AnimateWipe(float fromRadius, float toRadius)
        {
            if (_wipeImage == null) yield break;

            _wipeImage.gameObject.SetActive(true);
            SetRadius(fromRadius);

            float t = 0f;
            while (t < WIPE_DURATION)
            {
                t += Time.unscaledDeltaTime;
                SetRadius(Mathf.Lerp(fromRadius, toRadius, Mathf.Clamp01(t / WIPE_DURATION)));
                yield return null;
            }

            SetRadius(toRadius);
            if (toRadius >= 1f) _wipeImage.gameObject.SetActive(false);
        }

        private void SetRadius(float r)
        {
            if (_wipeMat != null) _wipeMat.SetFloat("_Radius", r);
        }
    }
}
