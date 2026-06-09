using UnityEngine;
using UnityEngine.UI;

namespace BeatHero.UI
{
    public class BeatBar : MonoBehaviour
    {
        [SerializeField] private Image[] _beatIndicators; // 4 indicators
        [SerializeField] private Color _activeColor   = Color.white;
        [SerializeField] private Color _inactiveColor = new Color(1f, 1f, 1f, 0.2f);

        private void Start() => SetAll(_inactiveColor);

        public void OnBeat(int beat)
        {
            if (_beatIndicators == null || _beatIndicators.Length == 0) return;
            int index = (beat - 1) % _beatIndicators.Length;
            for (int i = 0; i < _beatIndicators.Length; i++)
                _beatIndicators[i].color = i == index ? _activeColor : _inactiveColor;
        }

        private void SetAll(Color c)
        {
            if (_beatIndicators == null) return;
            foreach (var img in _beatIndicators) img.color = c;
        }
    }
}
