using System;
using UnityEngine;
using UnityEngine.Audio;

namespace BeatHero.Core
{
    public class Conductor : MonoBehaviour
    {
        [SerializeField] private AudioMixerGroup _bgmMixerGroup;
        public double SongPositionSec => _songPositionSec;
        public double SongPositionInBeats => _songPositionSec / _secPerBeat;
        public int Bpm => _bpm;
        public double SecPerBeat => _secPerBeat;

        public event Action<int> OnBeat;
        public event Action<double, int> OnSongScheduled; // (dspSongStartTime, bpm)
        public event Action OnPaused;
        public event Action<double> OnResumed; // (pausedDurationSec)

        // 마커 어프로치 리드타임 = 1마디(4박). secPerBeat*BEATS_PER_MEASURE = 240/BPM 초.
        private const int BEATS_PER_MEASURE = 4;

        // 핑퐁 AudioSource: 전환 시 AddComponent/Destroy 없이 레퍼런스 스왑만 수행
        private AudioSource _activeSource;   // 현재 재생 중인 BGM
        private AudioSource _standbySource;  // 다음 BGM 대기 (게임 시작 시 사전 할당)

        private double _floorStartDsp;
        private double _dspSongStartTime;
        private double _firstBeatOffsetSec;
        private double _songPositionSec;
        private int _bpm;
        private double _secPerBeat;
        private int _lastFiredBeat;
        private bool _isPlaying;

        // 예약 중인 페이즈 전환 데이터
        private bool _switchPending;
        private double _switchDspTime;
        private int _nextBpm;

        private bool _isPaused;
        private double _pauseDspTime;

        private void Awake()
        {
            _activeSource = GetComponent<AudioSource>();
            if (_activeSource == null)
                _activeSource = gameObject.AddComponent<AudioSource>();
            _activeSource.outputAudioMixerGroup = _bgmMixerGroup;

            // 대기 소스 — 게임 시작 시 1회만 생성. 전환마다 AddComponent 없음.
            _standbySource = gameObject.AddComponent<AudioSource>();
            _standbySource.outputAudioMixerGroup = _bgmMixerGroup;
            _standbySource.volume = 0f;
        }

        private void Update()
        {
            if (!_isPlaying || _isPaused) return;

            // 페이즈 전환 시각 도달 체크
            if (_switchPending && AudioSettings.dspTime >= _switchDspTime)
                ApplyPendingSwitch();

            _songPositionSec = AudioSettings.dspTime - _dspSongStartTime - _firstBeatOffsetSec;
            if (_songPositionSec < 0) return;

            int beat = (int)(SongPositionInBeats);
            while (_lastFiredBeat < beat)
            {
                _lastFiredBeat++;
                OnBeat?.Invoke(_lastFiredBeat);
            }
        }

        // 1단계: 층 데이터 세팅 — clip/bpm 준비 + 오디오 데이터 프리로드(예약 재생 레이턴시 제거). 클럭 미시작.
        public void PrepareSong(AudioClip bgm, int bpm, double firstBeatOffsetSec = 0.0)
        {
            _bpm = bpm;
            _secPerBeat = 60.0 / bpm;
            _firstBeatOffsetSec = firstBeatOffsetSec;
            _lastFiredBeat = -1;
            _switchPending = false;
            _isPlaying = false;
            _isPaused = false;

            _activeSource.clip   = bgm;
            _activeSource.loop   = true;
            _activeSource.volume = 1f;
            _activeSource.pitch  = 1f;

            // 대기 소스도 초기화 (이전 층 잔여 데이터 제거)
            _standbySource.Stop();
            _standbySource.clip   = null;
            _standbySource.volume = 0f;
            _standbySource.pitch  = 1f;

            if (bgm != null && bgm.loadState != AudioDataLoadState.Loaded)
                bgm.LoadAudioData();
        }

        // 2단계: 층 시작 — 현재 dspTime을 시작시간으로 저장. 1마디(리드) 뒤에 BGM 시작 = beat0 = CallPhase 시작.
        public void StartFloor()
        {
            _floorStartDsp = AudioSettings.dspTime;
            double leadSec = _secPerBeat * BEATS_PER_MEASURE; // 240/BPM
            _dspSongStartTime = _floorStartDsp + leadSec;
            _lastFiredBeat = -1;
            _activeSource.PlayScheduled(_dspSongStartTime);
            _isPlaying = true;
            OnSongScheduled?.Invoke(_dspSongStartTime, _bpm);
        }

        public void Stop()
        {
            _activeSource.Stop();
            _standbySource.Stop();
            _isPlaying = false;
            _isPaused = false;
            _switchPending = false;
        }

        public void Pause()
        {
            if (!_isPlaying || _isPaused) return;
            _pauseDspTime = AudioSettings.dspTime;
            _activeSource.Pause();
            _isPaused = true;
            OnPaused?.Invoke();
        }

        public void Resume()
        {
            if (!_isPlaying || !_isPaused) return;
            double pausedDuration = AudioSettings.dspTime - _pauseDspTime;
            _floorStartDsp    += pausedDuration;
            _dspSongStartTime += pausedDuration;
            _activeSource.UnPause();
            _isPaused = false;
            OnResumed?.Invoke(pausedDuration);
        }

        // 보스 페이즈 전환: 지정된 dspTime에 BGM/BPM 교체.
        // 클럭 파라미터(BPM·dspSongStartTime)는 즉시 갱신 → 다음 프레이즈가 올바른 BPM으로 시작됨.
        public void SwitchPhaseAt(double dspTime, AudioClip bgm, int bpm)
        {
            _switchDspTime = dspTime;
            _nextBpm       = bpm;
            _switchPending = true;

            _activeSource.SetScheduledEndTime(dspTime);

            _standbySource.clip   = bgm;
            _standbySource.loop   = true;
            _standbySource.volume = 1f;
            _standbySource.pitch  = 1f;
            _standbySource.PlayScheduled(dspTime);

            // 클럭 파라미터 즉시 교체 — 프레임 순서 경쟁 없이 새 BPM으로 전환
            _bpm                = bpm;
            _secPerBeat         = 60.0 / bpm;
            _dspSongStartTime   = dspTime;
            _firstBeatOffsetSec = 0;
            _lastFiredBeat      = 0;

            OnSongScheduled?.Invoke(dspTime, bpm);
        }

        // 전환 완충 마디용: 구 BGM 페이드아웃 + 신 BGM을 transitionBeats박 뒤(CallPhase 시작)에 예약.
        // 클럭 파라미터는 dspTime(전환 마디 시작) 기준으로 즉시 교체 → BeatBar가 신 BPM을 바로 표시.
        public void SwitchPhaseWithTransition(double dspTime, AudioClip bgm, int bpm, int transitionBeats = 4, bool pitchSweep = false)
        {
            // pitch 즉시 점프: clock 업데이트 전에 oldBpm 캡처
            if (pitchSweep && _bpm > 0)
                _activeSource.pitch = (float)bpm / _bpm;

            double newSecPerBeat = 60.0 / bpm;
            double bgmStartDsp   = dspTime + transitionBeats * newSecPerBeat;

            // 구 BGM: CallPhase 시작 직전 정지 + 전환 마디 전체에 걸쳐 페이드아웃
            _activeSource.SetScheduledEndTime(bgmStartDsp);
            StartCoroutine(FadeOutSource(_activeSource, (float)(bgmStartDsp - AudioSettings.dspTime)));

            // 신 BGM: CallPhase 시작 시각에 예약 (대기 소스 재사용 — AddComponent 없음)
            _switchDspTime = bgmStartDsp;
            _nextBpm       = bpm;
            _switchPending = true;

            _standbySource.clip   = bgm;
            _standbySource.loop   = true;
            _standbySource.volume = 1f;
            _standbySource.pitch  = 1f;
            _standbySource.PlayScheduled(bgmStartDsp);

            // 클럭 파라미터 즉시 교체 — 전환 마디부터 신 BPM 기준
            _bpm                = bpm;
            _secPerBeat         = newSecPerBeat;
            _dspSongStartTime   = dspTime;
            _firstBeatOffsetSec = 0;
            _lastFiredBeat      = 0;

            OnSongScheduled?.Invoke(dspTime, bpm);
        }

        private System.Collections.IEnumerator FadeOutSource(AudioSource src, float duration)
        {
            if (src == null || duration <= 0f) yield break;
            float startVolume = src.volume;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += UnityEngine.Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // Equal-power fade: cos 곡선으로 청각적으로 균일한 페이드
                try { src.volume = startVolume * Mathf.Cos(t * Mathf.PI * 0.5f); }
                catch { yield break; }
                yield return null;
            }
            try { src.volume = 0f; }
            catch { }
        }

        // 구형 API — 다음 마디 경계 자동 계산.
        public void SwitchPhaseAtNextMeasure(AudioClip bgm, int bpm, int beatsPerMeasure = 4)
        {
            int currentBeat     = (int)SongPositionInBeats;
            int nextMeasureBeat = ((currentBeat / beatsPerMeasure) + 1) * beatsPerMeasure;
            SwitchPhaseAt(GetBeatDspTime(nextMeasureBeat), bgm, bpm);
        }

        public double GetBeatDspTime(int beatIndex)
            => _dspSongStartTime + _firstBeatOffsetSec + beatIndex * _secPerBeat;

        // 레퍼런스 스왑 — Destroy/AddComponent 없이 핑퐁 교체
        private void ApplyPendingSwitch()
        {
            _switchPending = false;
            _activeSource.Stop();
            (_activeSource, _standbySource) = (_standbySource, _activeSource);
            // 구 소스 초기화 → 다음 전환에 재사용
            _standbySource.clip   = null;
            _standbySource.volume = 0f;
            _standbySource.pitch  = 1f;
        }
    }
}
