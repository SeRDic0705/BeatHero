using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace BeatHero.Data
{
    public abstract class GridEffectShape : SerializedScriptableObject
    {
        public abstract IEnumerable<CellEffect> AllCells();
        public abstract IEnumerable<(Vector2Int pos, CellEffect effect)> AllCellsWithPosition();
    }
}
