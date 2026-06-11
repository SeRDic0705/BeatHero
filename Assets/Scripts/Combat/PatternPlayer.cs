using BeatHero.Data;
using UnityEngine;

namespace BeatHero.Combat
{
    // 런타임 패턴 재생 인덱스 관리. PatternData SO는 읽기 전용으로만 사용.
    public class PatternPlayer : MonoBehaviour
    {
        public const int UNITS_PER_BEAT  = 12;
        private const int TOTAL_UNITS     = 48; // 4박

        private PatternData _pattern;
        private int _currentIndex;

        public PatternData CurrentPattern => _pattern;

        public BeatUnit Current => (_pattern != null && _currentIndex < _pattern.beatUnits.Count)
            ? _pattern.beatUnits[_currentIndex]
            : null;

        public bool IsFinished => _pattern == null || _currentIndex >= _pattern.beatUnits.Count;

        public void SetPattern(PatternData pattern)
        {
            _pattern = pattern;
            _currentIndex = 0;
        }

        public void Advance() => _currentIndex++;

        public void Reset() => _currentIndex = 0;

        // unitPosition(0~47)에 해당하는 BeatUnit 반환
        public BeatUnit GetUnitAtPosition(int unitPosition)
        {
            if (_pattern == null) return null;
            int accumulated = 0;
            foreach (var bu in _pattern.beatUnits)
            {
                accumulated += (int)bu.noteLength;
                if (unitPosition < accumulated) return bu;
            }
            return null;
        }

        // 박자 인덱스(0~3)를 단위 위치로 변환
        public static int BeatToUnitPosition(int beatIndex) => beatIndex * UNITS_PER_BEAT;
    }
}
