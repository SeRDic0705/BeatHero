using UnityEngine;

namespace BeatHero.Core
{
    // 판정/오디오 싱크 오프셋 저장소. PlayerPrefs에 ms 단위로 저장, 내부 값은 초 단위로 노출.
    public static class SyncSettings
    {
        private const string PREF_JUDGMENT_MS = "JudgmentOffsetMs";
        private const string PREF_AUDIO_MS    = "AudioOffsetMs";

        public static float JudgmentOffsetSec { get; private set; } =
            PlayerPrefs.GetFloat(PREF_JUDGMENT_MS, 0f) / 1000f;

        public static float AudioOffsetSec { get; private set; } =
            PlayerPrefs.GetFloat(PREF_AUDIO_MS, 0f) / 1000f;

        public static void SetJudgmentOffsetMs(float ms)
        {
            JudgmentOffsetSec = ms / 1000f;
            PlayerPrefs.SetFloat(PREF_JUDGMENT_MS, ms);
        }

        public static void SetAudioOffsetMs(float ms)
        {
            AudioOffsetSec = ms / 1000f;
            PlayerPrefs.SetFloat(PREF_AUDIO_MS, ms);
        }
    }
}
