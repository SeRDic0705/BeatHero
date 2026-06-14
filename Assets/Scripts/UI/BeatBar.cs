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

        // 리드 = 1마디. 마커는 출발 후 LOOKAHEAD_BEATS박 뒤(=커서 도달) BGM 비트와 일치.
        private const int LOOKAHEAD_BEATS = 4;

        // 한 마커: 도달 시각(arrivalDsp)에 커서(toX)에 닿도록 fromX→toX 보간. 위치는 dspTime으로 산출.
        private class Marker
        {
            public RectTransform rt;
            public double arrivalDsp;
            public double lead;   // 스폰 시점의 lead 값 — BPM 변경 후에도 올바른 속도로 이동
            public float fromX;
            public float toX;
            public float y;
        }

        private Conductor _conductor;
        private double _lead;          // 240/BPM (초)
        private double _interval;      // 60/BPM  (초, 1박)
        private double _nextArrivalDsp; // 다음 스폰할 마커가 커서에 도달하는 시각
        private bool   _isRunning;
        private bool   _paused;
        private Vector3 _cursorBaseScale;
        private float _cursorPulseTimer;

        private readonly List<Marker> _activeMarkers = new();
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
            _conductor.OnPaused        += OnPaused;
            _conductor.OnResumed       += OnResumed;
        }

        private void UnsubscribeAll()
        {
            if (_conductor == null) return;
            _conductor.OnSongScheduled -= OnSongScheduled;
            _conductor.OnBeat          -= OnBeat;
            _conductor.OnPaused        -= OnPaused;
            _conductor.OnResumed       -= OnResumed;
        }

        // 층 시작 또는 보스 페이즈 전환 시 호출.
        // 층 시작: 마커 전부 초기화 후 새 BPM으로 시작.
        // 페이즈 전환: 비행 중인 마커는 자기 lead로 자연 소멸 — 갑작스러운 리셋 없음.
        private void OnSongScheduled(double dspSongStart, int bpm)
        {
            bool isSwitch = _isRunning;
            if (!isSwitch)
            {
                ReturnAllMarkers();
                Canvas.ForceUpdateCanvases();
            }

            double secPerBeat = 60.0 / bpm;
            _interval = secPerBeat;
            _lead     = secPerBeat * LOOKAHEAD_BEATS;

            _nextArrivalDsp = dspSongStart;
            _paused    = false;
            _isRunning = true;
        }

        private void Update()
        {
            if (!_isRunning || _paused) return;

            double now = AudioSettings.dspTime;

            // departure(arrival-lead)에 도달한 마커 스폰
            while (now >= _nextArrivalDsp - _lead)
            {
                SpawnPair(_nextArrivalDsp, now);
                _nextArrivalDsp += _interval;
            }

            MoveMarkers(now);
            UpdateCursorPulse();
        }

        private void MoveMarkers(double now)
        {
            for (int i = _activeMarkers.Count - 1; i >= 0; i--)
            {
                var m = _activeMarkers[i];
                if (m.rt == null) { _activeMarkers.RemoveAt(i); continue; }

                if (PositionMarker(m, now))
                {
                    ReturnMarker(m.rt);
                    _activeMarkers.RemoveAt(i);
                }
            }
        }

        // 위치 갱신. 커서 도달(frac>=1) 시 true 반환.
        // m.lead: 스폰 시점의 lead — BPM 전환 후에도 기존 마커가 올바른 속도로 이동
        private bool PositionMarker(Marker m, double now)
        {
            float frac = (float)(1.0 - (m.arrivalDsp - now) / m.lead);
            if (frac >= 1f) return true;
            if (frac < 0f) frac = 0f;
            float x = m.fromX + (m.toX - m.fromX) * frac;
            m.rt.position = new Vector3(x, m.y, m.rt.position.z);
            return false;
        }

        private void SpawnPair(double arrivalDsp, double now)
        {
            if (_leftSpawn == null || _rightSpawn == null || _cursor == null) return;
            float cursorX = _cursor.position.x;
            SpawnMarker(arrivalDsp, _leftSpawn.position.x,  cursorX, _leftSpawn.position.y,  now);
            SpawnMarker(arrivalDsp, _rightSpawn.position.x, cursorX, _rightSpawn.position.y, now);
        }

        private void SpawnMarker(double arrivalDsp, float fromX, float toX, float y, double now)
        {
            var m = new Marker { rt = GetMarker(), arrivalDsp = arrivalDsp, lead = _lead, fromX = fromX, toX = toX, y = y };
            // 이미 커서를 지난(또는 도달한) 마커라면 추가하지 않고 즉시 반환
            if (PositionMarker(m, now)) { ReturnMarker(m.rt); return; }
            _activeMarkers.Add(m);
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
            if (rt == null) return;
            rt.gameObject.SetActive(false);
            _pool.Enqueue(rt);
        }

        private void ReturnAllMarkers()
        {
            foreach (var m in _activeMarkers)
                if (m.rt != null) ReturnMarker(m.rt);
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

        // 일시정지: 모션/스폰 정지. dspTime은 계속 흐르므로 Resume에서 그만큼 보정.
        private void OnPaused() => _paused = true;

        private void OnResumed(double pausedDurationSec)
        {
            foreach (var m in _activeMarkers)
                m.arrivalDsp += pausedDurationSec;
            _nextArrivalDsp += pausedDurationSec;
            _paused = false;
        }
    }
}
