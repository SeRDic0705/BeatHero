using System.Collections.Generic;
using BeatHero.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BeatHero.UI
{
    public class BeatBar : MonoBehaviour
    {
        [SerializeField] private RectTransform _cursor;
        [SerializeField] private RectTransform _leftSpawn;
        [SerializeField] private RectTransform _rightSpawn;
        [SerializeField] private RectTransform _markerPrefab;
        [SerializeField] private Color _markerColor        = Color.white;
        [SerializeField] private float _markerWidth        = 4f;
        [SerializeField] private float _cursorPulseScale   = 1.4f;
        [SerializeField] private float _cursorPulseDuration = 0.1f;

        private Conductor _conductor;
        private float _markerSpeed;
        private float _spawnInterval;
        private float _spawnTimer;
        private bool  _isRunning;
        private Vector3 _cursorBaseScale;
        private float _cursorPulseTimer;

        private readonly List<(RectTransform rt, float dir)> _activeMarkers = new();
        private readonly Queue<RectTransform> _pool = new();

        private void Awake()
        {
            if (_cursor != null)
                _cursorBaseScale = _cursor.localScale;
        }

        private void OnDestroy() => UnsubscribeAll();

        public void Bind(Conductor conductor)
        {
            UnsubscribeAll();
            _conductor = conductor;
            if (_conductor == null) return;
            _conductor.OnSongScheduled += OnSongScheduled;
            _conductor.OnBeat          += OnBeat;
        }

        private void UnsubscribeAll()
        {
            if (_conductor == null) return;
            _conductor.OnSongScheduled -= OnSongScheduled;
            _conductor.OnBeat          -= OnBeat;
        }

        private void OnSongScheduled(double dspStartTime, int bpm)
        {
            ReturnAllMarkers();

            float secPerBeat = 60f / bpm;

            float dist = (_rightSpawn != null && _cursor != null)
                ? Mathf.Abs(_rightSpawn.position.x - _cursor.position.x)
                : 500f;

            // BPM 계산이 아닌 실제 DSP 잔여시간으로 마커 속도 결정 — dspTime 오프셋 오차 방지
            float leadTime = (float)(dspStartTime - AudioSettings.dspTime);
            if (leadTime <= 0f) leadTime = secPerBeat * 4f;

            _markerSpeed   = dist / leadTime;
            _spawnInterval = secPerBeat;
            _spawnTimer    = 0f;
            _isRunning     = true;

            SpawnPair();
        }

        private void Update()
        {
            if (!_isRunning) return;
            MoveMarkers();
            _spawnTimer += Time.deltaTime;
            if (_spawnTimer >= _spawnInterval)
            {
                _spawnTimer -= _spawnInterval;
                SpawnPair();
            }
            UpdateCursorPulse();
        }

        private void MoveMarkers()
        {
            float delta   = _markerSpeed * Time.deltaTime;
            float cursorX = _cursor != null ? _cursor.position.x : 0f;

            for (int i = _activeMarkers.Count - 1; i >= 0; i--)
            {
                var (rt, dir) = _activeMarkers[i];
                if (rt == null) { _activeMarkers.RemoveAt(i); continue; }

                rt.position += new Vector3(dir * delta, 0f, 0f);

                bool passed = dir > 0f ? rt.position.x >= cursorX
                                       : rt.position.x <= cursorX;
                if (!passed) continue;

                ReturnMarker(rt);
                _activeMarkers.RemoveAt(i);
            }
        }

        private void SpawnPair()
        {
            if (_leftSpawn == null || _rightSpawn == null) return;
            SpawnMarker(_leftSpawn.position, +1f);
            SpawnMarker(_rightSpawn.position, -1f);
        }

        private void SpawnMarker(Vector3 worldPos, float dir)
        {
            var rt = GetMarker();
            rt.position = worldPos;
            _activeMarkers.Add((rt, dir));
        }

        private RectTransform GetMarker()
        {
            if (_pool.Count > 0)
            {
                var pooled = _pool.Dequeue();
                pooled.gameObject.SetActive(true);
                return pooled;
            }

            RectTransform rt;
            if (_markerPrefab != null)
            {
                rt = Instantiate(_markerPrefab, transform);
            }
            else
            {
                var go = new GameObject("BeatMarker", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(transform, false);
                rt = go.GetComponent<RectTransform>();
                var img = go.GetComponent<Image>();
                img.color    = _markerColor;
                rt.sizeDelta  = new Vector2(_markerWidth, 0f);
                rt.anchorMin  = new Vector2(0f, 0f);
                rt.anchorMax  = new Vector2(0f, 1f);
            }
            return rt;
        }

        private void ReturnMarker(RectTransform rt)
        {
            rt.gameObject.SetActive(false);
            _pool.Enqueue(rt);
        }

        private void ReturnAllMarkers()
        {
            foreach (var (rt, _) in _activeMarkers)
                if (rt != null) ReturnMarker(rt);
            _activeMarkers.Clear();
        }

        private void UpdateCursorPulse()
        {
            if (_cursor == null || _cursorPulseTimer <= 0f) return;
            _cursorPulseTimer -= Time.deltaTime;
            float t = Mathf.Clamp01(_cursorPulseTimer / _cursorPulseDuration);
            _cursor.localScale = _cursorBaseScale * Mathf.Lerp(1f, _cursorPulseScale, t);
        }

        public void OnBeat(int beat)
        {
            _cursorPulseTimer = _cursorPulseDuration;
        }
    }
}
