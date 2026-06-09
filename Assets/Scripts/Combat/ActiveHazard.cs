using BeatHero.Data;
using UnityEngine;

namespace BeatHero.Combat
{
    public class ActiveHazard
    {
        public Vector2Int Position;
        public PersistentHazardEffect Effect;
        public int RemainingResponsePhases;
    }
}
