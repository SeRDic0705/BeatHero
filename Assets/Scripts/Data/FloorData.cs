using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace BeatHero.Data
{
    [CreateAssetMenu(menuName = "BeatHero/FloorData")]
    public class FloorData : SerializedScriptableObject
    {
        [BoxGroup("Floors")]
        [InfoBox("floors[0] = 1층 몬스터, floors[1] = 2층, ... (기획자 작성 예정)")]
        public List<MonsterData> floors = new();

        public MonsterData GetMonster(int floor)
        {
            if (floors == null || floors.Count == 0) return null;
            return floors[Mathf.Clamp(floor - 1, 0, floors.Count - 1)];
        }
    }
}
