using System;
using UnityEngine;

namespace BeatHero.Data
{
    // 몬스터 전투 중 말풍선 대사 한 줄.
    // useInSequential: 순차 출력(전투 시작 후 n번째 CallPhase = n번 순차 대사) 대상.
    // useInRandom:     순차 소진 후 랜덤 출력 후보에 포함.
    [Serializable]
    public class MonsterDialogueLine
    {
        [TextArea] public string text;
        public bool useInSequential = true;
        public bool useInRandom     = false;
    }
}
