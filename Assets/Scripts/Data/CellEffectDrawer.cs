#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace BeatHero.Data
{
    internal static class CellEffectDrawer
    {
        private static readonly Color COLOR_EMPTY   = new Color(0.15f, 0.15f, 0.15f);
        private static readonly Color COLOR_DAMAGE  = new Color(0.75f, 0.20f, 0.20f);
        private static readonly Color COLOR_SHIELD  = new Color(0.20f, 0.40f, 0.90f);
        private static readonly Color COLOR_HAZARD  = new Color(0.55f, 0.10f, 0.55f);

        public static CellEffect Draw(Rect rect, CellEffect value)
        {
            Color bg = value == null            ? COLOR_EMPTY  :
                       value is ShieldEffect    ? COLOR_SHIELD :
                       value is PersistentHazardEffect ? COLOR_HAZARD :
                       COLOR_DAMAGE;

            EditorGUI.DrawRect(new Rect(rect.x + 1, rect.y + 1, rect.width - 2, rect.height - 2), bg);

            if (value != null)
            {
                string lbl = value is DamageEffect          ? "DMG" :
                             value is ShieldEffect          ? "SHD" :
                             value is PersistentHazardEffect ? "HAZ" : "?";
                var style = new GUIStyle(EditorStyles.boldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize  = 11,
                    normal    = { textColor = Color.white }
                };
                GUI.Label(rect, lbl, style);
            }

            var e = Event.current;
            if (!rect.Contains(e.mousePosition)) return value;

            if (e.type == EventType.DragUpdated || e.type == EventType.DragPerform)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                if (e.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    foreach (var obj in DragAndDrop.objectReferences)
                        if (obj is CellEffect ce) { e.Use(); return ce; }
                }
                e.Use();
            }
            else if (e.type == EventType.MouseDown && e.button == 1)
            {
                GUI.changed = true;
                e.Use();
                return null;
            }

            return value;
        }
    }
}
#endif
