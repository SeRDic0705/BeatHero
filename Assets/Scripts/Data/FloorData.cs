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
        //[ListDrawerSettings(OnBeginListElementGUI = nameof(DrawFloorLabel))]
        public List<MonsterData> floors = new();

#if UNITY_EDITOR
        private void DrawFloorLabel(int index)
        {
            UnityEditor.EditorGUILayout.LabelField($"{index + 1}층", UnityEditor.EditorStyles.boldLabel);
        }
#endif

        public MonsterData GetMonster(int floor)
        {
            if (floors == null || floors.Count == 0) return null;
            return floors[Mathf.Clamp(floor - 1, 0, floors.Count - 1)];
        }
    }
}
