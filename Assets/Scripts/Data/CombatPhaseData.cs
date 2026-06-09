using System;
using System.Collections.Generic;
using UnityEngine;

namespace BeatHero.Data
{
    [Serializable]
    public class CombatPhaseData
    {
        public int bpm;
        public AudioClip bgm;
        public List<PatternData> patterns;
    }
}
