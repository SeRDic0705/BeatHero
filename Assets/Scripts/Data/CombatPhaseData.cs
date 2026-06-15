using System;
using System.Collections.Generic;
using UnityEngine;

namespace BeatHero.Data
{
    [Serializable]
    public class CombatPhaseData
    {
        public float bpm;
        public AudioClip bgm;
        public List<PatternData> patterns;
    }
}
