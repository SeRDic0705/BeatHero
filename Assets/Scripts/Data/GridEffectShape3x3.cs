using Sirenix.OdinInspector;
using UnityEngine;

namespace BeatHero.Data
{
    [CreateAssetMenu(menuName = "BeatHero/GridEffectShape/3x3")]
    public class GridEffectShape3x3 : GridEffectShape
    {
        [TableMatrix(HorizontalTitle = "Col", VerticalTitle = "Row", DrawElementMethod = "DrawCell", ResizableColumns = false, SquareCells = true)]
        public CellEffect[,] cells = new CellEffect[3, 3];

#if UNITY_EDITOR
        private static CellEffect DrawCell(Rect rect, CellEffect value)
        {
            return CellEffectDrawer.Draw(rect, value);
        }
#endif
    }
}
