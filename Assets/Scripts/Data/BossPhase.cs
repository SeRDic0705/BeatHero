using System;

namespace BeatHero.Data
{
    [Serializable]
    public class BossPhase : CombatPhaseData
    {
        // HP%가 이 값 이하로 떨어지면 이 페이즈로 전환. 예: 1.0 = 1페이즈(시작), 0.5 = 2페이즈
        public float hpThreshold;
    }
}
