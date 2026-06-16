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

        // 모든 몬스터는 MonsterBaseAnimator 상태머신을 공유하고 클립만 교체한다.
        // MonsterView가 런타임에 AnimatorOverrideController를 생성해 적용한다.
        [BoxGroup("Animation")]
        public AnimationClip idleClip;
        [BoxGroup("Animation")]
        public AnimationClip hurtClip;
        [BoxGroup("Animation")]
        public AnimationClip deathClip;

        [BoxGroup("Effects")]
        public Dictionary<CellEffect, CellEffectFeedback> effectFeedbacks = new();
        [BoxGroup("Effects")]
        public AudioClip hitSfx;
        [BoxGroup("Effects")]
        public AudioClip deathSfx;

        public abstract CombatPhaseData GetCurrentPhase(float hpPercent);
    }
}
