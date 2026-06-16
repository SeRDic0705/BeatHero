using System.Collections.Generic;
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

        [BoxGroup("Animation")]
        public RuntimeAnimatorController animator;
        [BoxGroup("Animation")]
        public float idleClipLength = 1f;

        [BoxGroup("Effects")]
        public Dictionary<CellEffect, CellEffectFeedback> effectFeedbacks = new();
        [BoxGroup("Effects")]
        public AudioClip hitSfx;
        [BoxGroup("Effects")]
        public AudioClip deathSfx;
        [BoxGroup("Effects")]
        public GameObject deathVfxPrefab;

        public abstract CombatPhaseData GetCurrentPhase(float hpPercent);
    }
}
