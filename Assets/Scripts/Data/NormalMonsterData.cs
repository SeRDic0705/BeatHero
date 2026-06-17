using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace BeatHero.Data
{
    [CreateAssetMenu(menuName = "BeatHero/Monster/Normal")]
    public class NormalMonsterData : MonsterData
    {
        [BoxGroup("Combat")]
        public float bpm;
        [BoxGroup("Combat")]
        public AudioClip bgm;
        [BoxGroup("Combat"), PropertyOrder(2)] // isRandom(PropertyOrder 1) 바로 아래
        public List<PatternData> patterns;

        public override CombatPhaseData GetCurrentPhase(float hpPercent)
            => new CombatPhaseData { bpm = bpm, bgm = bgm, patterns = patterns };
    }
}
