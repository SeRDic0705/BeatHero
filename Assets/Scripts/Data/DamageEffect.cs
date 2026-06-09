using UnityEngine;

namespace BeatHero.Data
{
    // 판정 시 해당 칸에 있으면 데미지 (일회성)
    // 실제 피해량: MonsterData.attackPower × PatternData.damageMultiplier
    [CreateAssetMenu(menuName = "BeatHero/CellEffect/Damage")]
    public class DamageEffect : CellEffect { }
}
