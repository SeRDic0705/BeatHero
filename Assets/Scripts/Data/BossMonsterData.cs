using System.Collections.Generic;
using UnityEngine;

namespace BeatHero.Data
{
    [CreateAssetMenu(menuName = "BeatHero/Monster/Boss")]
    public class BossMonsterData : MonsterData
    {
        // hpThreshold 내림차순 정렬 (1.0 → 0.5 → ...)
        public List<BossPhase> phases;

        public override CombatPhaseData GetCurrentPhase(float hpPercent)
        {
            for (int i = phases.Count - 1; i >= 0; i--)
                if (hpPercent <= phases[i].hpThreshold)
                    return phases[i];
            return phases[0];
        }
    }
}
