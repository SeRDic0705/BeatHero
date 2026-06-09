using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;

namespace BeatHero.Data
{
    [CreateAssetMenu(menuName = "BeatHero/PatternData")]
    public class PatternData : SerializedScriptableObject
    {
        private const int BEATS_PER_PATTERN = 48;

        [ValidateInput(nameof(ValidateLength), "총 음표 길이가 48단위(4박)와 맞지 않습니다.")]
        public List<BeatUnit> beatUnits = new();

        [Range(0.1f, 5f)]
        public float damageMultiplier = 1f;

        private bool ValidateLength()
            => beatUnits != null && beatUnits.Sum(b => (int)b.noteLength) == BEATS_PER_PATTERN;
    }
}
