using Sirenix.OdinInspector;
using UnityEngine;

namespace BeatHero.Data
{
    [CreateAssetMenu(menuName = "BeatHero/PlayerConfig")]
    public class PlayerConfig : ScriptableObject
    {
        [BoxGroup("Stats")]
        public int maxHp = 100;
        [BoxGroup("Stats")]
        public int maxMana = 8;
        [BoxGroup("Stats")]
        public int attackPower = 10;

        // 몬스터 피격음 — 모든 몬스터 공통(플레이어 공격 1회당 재생). 몬스터별 hitSfx 대체.
        [BoxGroup("SFX")]
        public AudioClip hitSfx;
    }
}
