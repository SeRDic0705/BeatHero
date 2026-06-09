using BeatHero.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BeatHero.UI
{
    public class BeatBar : MonoBehaviour
    {
        [SerializeField] private RectTransform _cursor;
        [SerializeField] private Image[]       _beatMarkers;  // 4개, 박자 위치에 고정
        [SerializeField] private Color _activeColor   = Color.white;
        [SerializeField] private Color _inactiveColor = new Color(1f, 1f, 1f, 0.25f);
        [SerializeField] private float _flashDuration = 0.12f;

        private Conductor _conductor;
        private float[]   _flashTimers;

        private void Awake()
        {
            _flashTimers = new float[4];
            if (_beatMarkers != null)
                foreach (var m in _beatMarkers)
                    if (m) m.color = _inactiveColor;
        }

        // CombatHUD.BindConductor에서 호출
        public void Bind(Conductor conductor) => _conductor = conductor;

        private void Update()
        {
            MoveCursor();
            DecayMarkers();
        }

        private void MoveCursor()
        {
            if (_cursor == null || _conductor == null) return;
            float barWidth = ((RectTransform)transform).rect.width;
            float t = (float)(_conductor.SongPositionInBeats % 4) / 4f; // 0..1 per measure
            _cursor.anchoredPosition = new Vector2(t * barWidth, 0f);
        }

        private void DecayMarkers()
        {
            if (_beatMarkers == null) return;
            for (int i = 0; i < _beatMarkers.Length; i++)
            {
                if (_flashTimers[i] <= 0f) continue;
                _flashTimers[i] -= Time.deltaTime;
                float t = Mathf.Clamp01(_flashTimers[i] / _flashDuration);
                if (_beatMarkers[i]) _beatMarkers[i].color = Color.Lerp(_inactiveColor, _activeColor, t);
            }
        }

        public void OnBeat(int beat)
        {
            if (_beatMarkers == null || _beatMarkers.Length == 0) return;
            int i = (beat - 1) % _beatMarkers.Length;
            _flashTimers[i] = _flashDuration;
            if (_beatMarkers[i]) _beatMarkers[i].color = _activeColor;
        }
    }
}
