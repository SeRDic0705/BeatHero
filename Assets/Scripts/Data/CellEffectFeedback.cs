using UnityEngine;

namespace BeatHero.Data
{
    [CreateAssetMenu(menuName = "BeatHero/CellEffectFeedback")]
    public class CellEffectFeedback : ScriptableObject
    {
        public AudioClip activateSfx;
        public Sprite[]  vfxFrames;
        public float     vfxFps = 24f;
    }
}
