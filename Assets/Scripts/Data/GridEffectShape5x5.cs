using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace BeatHero.Data
{
    [CreateAssetMenu(menuName = "BeatHero/GridEffectShape/5x5")]
    public class GridEffectShape5x5 : GridEffectShape
    {
        [TableMatrix(HorizontalTitle = "Col", VerticalTitle = "Row", DrawElementMethod = "DrawCell", ResizableColumns = false, SquareCells = true)]
        public CellEffect[,] cells = new CellEffect[5, 5];

        public override IEnumerable<CellEffect> AllCells()
        {
            for (int x = 0; x < 5; x++)
                for (int y = 0; y < 5; y++)
                    yield return cells[x, y];
        }

        public override IEnumerable<(Vector2Int pos, CellEffect effect)> AllCellsWithPosition()
        {
            for (int x = 0; x < 5; x++)
                for (int y = 0; y < 5; y++)
                    yield return (new Vector2Int(x, y), cells[x, y]);
        }

#if UNITY_EDITOR
        private static CellEffect DrawCell(Rect rect, CellEffect value)
        {
            return CellEffectDrawer.Draw(rect, value);
        }
#endif
    }
}
