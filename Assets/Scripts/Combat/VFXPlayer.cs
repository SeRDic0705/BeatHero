using System.Collections;
using UnityEngine;

namespace BeatHero.Combat
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class VFXPlayer : MonoBehaviour
    {
        private SpriteRenderer _renderer;
        private Coroutine      _routine;

        private void Awake() => _renderer = GetComponent<SpriteRenderer>();

        public void Play(Sprite[] frames, float fps, Vector3 worldPos, System.Action onComplete)
        {
            if (_routine != null) StopCoroutine(_routine);
            transform.position = worldPos;
            _routine = StartCoroutine(PlayRoutine(frames, fps, onComplete));
        }

        private IEnumerator PlayRoutine(Sprite[] frames, float fps, System.Action onComplete)
        {
            float interval = fps > 0f ? 1f / fps : 0.083f;
            foreach (var sprite in frames)
            {
                _renderer.sprite = sprite;
                yield return new WaitForSeconds(interval);
            }
            _renderer.sprite = null;
            _routine = null;
            onComplete?.Invoke();
        }
    }
}
