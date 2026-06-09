using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BeatHero.Core
{
    // Title↔Game 씬 전환 + 페이드 연출
    public class SceneLoader : MonoBehaviour
    {
        public static SceneLoader Instance { get; private set; }

        private const string SCENE_TITLE = "TitleScene";
        private const string SCENE_GAME  = "GameScene";
        private const float FADE_DURATION = 0.4f;

        [SerializeField] private CanvasGroup _fadeCanvas;

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

        public void LoadGame() => StartCoroutine(LoadWithFade(SCENE_GAME));
        public void LoadTitle() => StartCoroutine(LoadWithFade(SCENE_TITLE));

        private IEnumerator LoadWithFade(string sceneName)
        {
            yield return Fade(0f, 1f);
            yield return SceneManager.LoadSceneAsync(sceneName);
            yield return Fade(1f, 0f);
        }

        private IEnumerator Fade(float from, float to)
        {
            if (_fadeCanvas == null) yield break;
            _fadeCanvas.gameObject.SetActive(true);
            float t = 0f;
            while (t < FADE_DURATION)
            {
                t += Time.unscaledDeltaTime;
                _fadeCanvas.alpha = Mathf.Lerp(from, to, t / FADE_DURATION);
                yield return null;
            }
            _fadeCanvas.alpha = to;
            if (to <= 0f) _fadeCanvas.gameObject.SetActive(false);
        }
    }
}
