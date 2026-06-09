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

        private AudioSource _audioSource;
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
        private AudioClip _nextBgm;
        private int _nextBpm;

        private bool _isPaused;
        private double _pauseDspTime;

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
                _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.outputAudioMixerGroup = _bgmMixerGroup;
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

        public void StartSong(AudioClip bgm, int bpm, double firstBeatOffsetSec = 0.0)
        {
            _bpm = bpm;
            _secPerBeat = 60.0 / bpm;
            _firstBeatOffsetSec = firstBeatOffsetSec;
            _lastFiredBeat = 0;
            _switchPending = false;

            // 약간의 여유(0.1초)를 두고 예약해 시작 시각을 확정
            _dspSongStartTime = AudioSettings.dspTime + 0.1;
            _audioSource.clip = bgm;
            _audioSource.loop = true;
            _audioSource.PlayScheduled(_dspSongStartTime);
            _isPlaying = true;
        }

        public void Stop()
        {
            _audioSource.Stop();
            _isPlaying = false;
            _isPaused = false;
            _switchPending = false;
        }

        public void Pause()
        {
            if (!_isPlaying || _isPaused) return;
            _pauseDspTime = AudioSettings.dspTime;
            _audioSource.Pause();
            _isPaused = true;
        }

        public void Resume()
        {
            if (!_isPlaying || !_isPaused) return;
            double pausedDuration = AudioSettings.dspTime - _pauseDspTime;
            _dspSongStartTime += pausedDuration;
            _audioSource.UnPause();
            _isPaused = false;
        }

        // 보스 페이즈 전환: 다음 마디 경계(4박 배수)에서 BGM/BPM 교체
        public void SwitchPhaseAtNextMeasure(AudioClip bgm, int bpm, int beatsPerMeasure = 4)
        {
            int currentBeat = (int)SongPositionInBeats;
            int nextMeasureBeat = ((currentBeat / beatsPerMeasure) + 1) * beatsPerMeasure;
            _switchDspTime = GetBeatDspTime(nextMeasureBeat);
            _nextBgm = bgm;
            _nextBpm = bpm;
            _switchPending = true;

            _audioSource.SetScheduledEndTime(_switchDspTime);
            var nextSource = gameObject.AddComponent<AudioSource>();
            nextSource.clip = bgm;
            nextSource.loop = true;
            nextSource.outputAudioMixerGroup = _bgmMixerGroup;
            nextSource.PlayScheduled(_switchDspTime);
            // 전환 완료 시 ApplyPendingSwitch에서 파라미터 교체
        }

        public double GetBeatDspTime(int beatIndex)
            => _dspSongStartTime + _firstBeatOffsetSec + beatIndex * _secPerBeat;

        private void ApplyPendingSwitch()
        {
            _switchPending = false;
            _bpm = _nextBpm;
            _secPerBeat = 60.0 / _nextBpm;
            _dspSongStartTime = _switchDspTime;
            _firstBeatOffsetSec = 0;
            _lastFiredBeat = 0;

            // 기존 AudioSource 제거 후 새 소스를 주 소스로 교체
            var sources = GetComponents<AudioSource>();
            foreach (var src in sources)
                if (src != _audioSource && src.clip == _nextBgm)
                {
                    Destroy(_audioSource);
                    _audioSource = src;
                    break;
                }
        }
    }
}
