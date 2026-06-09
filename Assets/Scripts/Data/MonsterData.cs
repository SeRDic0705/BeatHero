using Sirenix.OdinInspector;
using UnityEngine;

namespace BeatHero.Data
{
    public abstract class MonsterData : SerializedScriptableObject
    {
        [BoxGroup("Stats")]
        public int maxHp;
        [BoxGroup("Stats")]
        public int attackPower;
        [BoxGroup("Stats")]
        public GridType gridType;

        [BoxGroup("Presentation")]
        public Sprite sprite;
        [BoxGroup("Presentation")]
        public RuntimeAnimatorController animator;
        [BoxGroup("Presentation")]
        public AudioClip hitSfx;
        [BoxGroup("Presentation")]
        public AudioClip deathSfx;
        [BoxGroup("Presentation")]
        public GameObject deathVfxPrefab;

        public abstract CombatPhaseData GetCurrentPhase(float hpPercent);
    }
}
