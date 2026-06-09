using UnityEngine;

namespace BeatHero.Data
{
    // 지속형 장애물: N ResponsePhase 동안 해당 칸을 이동 불가 구역으로 지정
    [CreateAssetMenu(menuName = "BeatHero/CellEffect/PersistentHazard")]
    public class PersistentHazardEffect : CellEffect
    {
        public int durationResponsePhases;
    }
}
