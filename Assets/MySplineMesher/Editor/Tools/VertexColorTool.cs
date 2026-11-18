using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

namespace SC.Modeling.Splines.Editing
{
    /// <summary>Paint per-knot vertex colors. Click a knot to apply the paint color.</summary>
    [EditorTool("Spline Mesher: Vertex Color", typeof(SplineMesher))]
    public class VertexColorTool : PointEditTool
    {
        static GUIContent icon;
        static Color paintColor = Color.red;

        public override GUIContent toolbarIcon
        {
            get
            {
                if (icon == null)
                    icon = new GUIContent(EditorGUIUtility.IconContent("ColorPicker.CycleSlider").image, "Spline Mesher: Vertex Color");
                return icon;
            }
        }

        protected override void OnPoint(SplineMesher mesher, int si, int k, PointData data, Vector3 pos, Quaternion rot)
        {
            float size = HandleUtility.GetHandleSize(pos);
            Handles.color = data.color;
            if (Handles.Button(pos + rot * Vector3.up * size * 0.3f, rot, size * 0.12f, size * 0.16f, Handles.SphereHandleCap))
            {
                BeginEdit(mesher, "Paint Spline Point");
                data.color = paintColor;
                EndEdit(mesher);

                if (!mesher.Settings.useVertexColors)
                {
                    mesher.Settings.useVertexColors = true; // painting is pointless otherwise
                    EndEdit(mesher);
                }
            }
        }

        protected override void OnOverlay(SplineMesher mesher)
        {
            GUILayout.BeginArea(new Rect(12, 12, 230, 86), EditorStyles.helpBox);
            GUILayout.Label("Click a knot sphere to paint it", EditorStyles.miniLabel);
            paintColor = EditorGUILayout.ColorField(GUIContent.none, paintColor, true, true, false);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Fill all", EditorStyles.miniButton)) Fill(mesher, paintColor, "Fill Vertex Colors");
            if (GUILayout.Button("Clear (white)", EditorStyles.miniButton)) Fill(mesher, Color.white, "Clear Vertex Colors");
            EditorGUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        static void Fill(SplineMesher mesher, Color c, string undoName)
        {
            BeginEdit(mesher, undoName);
            var splines = mesher.Container.Splines;
            for (int si = 0; si < splines.Count; si++)
                for (int k = 0; k < splines[si].Count; k++)
                    mesher.GetPointData(si, k).color = c;
            EndEdit(mesher);
        }
    }
}
