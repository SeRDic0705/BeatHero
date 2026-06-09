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
        public int maxMana = 5;
        [BoxGroup("Stats")]
        public int attackPower = 10;
    }
}
