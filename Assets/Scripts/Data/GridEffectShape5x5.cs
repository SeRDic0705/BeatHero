using Sirenix.OdinInspector;
using UnityEngine;

namespace BeatHero.Data
{
    [CreateAssetMenu(menuName = "BeatHero/GridEffectShape/5x5")]
    public class GridEffectShape5x5 : GridEffectShape
    {
        [TableMatrix(HorizontalTitle = "Col", VerticalTitle = "Row", DrawElementMethod = "DrawCell", ResizableColumns = false, SquareCells = true)]
        public CellEffect[,] cells = new CellEffect[5, 5];

#if UNITY_EDITOR
        private static CellEffect DrawCell(Rect rect, CellEffect value)
        {
            return CellEffectDrawer.Draw(rect, value);
        }
#endif
    }
}
