using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

namespace SC.Modeling.Splines.Editing
{
    /// <summary>Twist the mesh around the spline: drag the disc at each knot.</summary>
    [EditorTool("Spline Mesher: Roll", typeof(SplineMesher))]
    public class RollTool : PointEditTool
    {
        static GUIContent icon;

        public override GUIContent toolbarIcon
        {
            get
            {
                if (icon == null)
                    icon = new GUIContent(EditorGUIUtility.IconContent("RotateTool").image, "Spline Mesher: Roll");
                return icon;
            }
        }

        protected override void OnPoint(SplineMesher mesher, int si, int k, PointData data, Vector3 pos, Quaternion rot)
        {
            float size = HandleUtility.GetHandleSize(pos) * 0.8f;
            Vector3 axis = rot * Vector3.forward;

            Handles.color = new Color(0.3f, 0.8f, 1f, 1f);
            EditorGUI.BeginChangeCheck();
            Quaternion rolled = Handles.Disc(rot, pos, axis, size, false, 1f);
            if (EditorGUI.EndChangeCheck())
            {
                float delta = Vector3.SignedAngle(rot * Vector3.up, rolled * Vector3.up, axis);
                BeginEdit(mesher, "Roll Spline Point");
                data.roll += delta;
                EndEdit(mesher);
            }

            Handles.Label(pos + rot * Vector3.up * size * 0.25f, data.roll.ToString("F1") + "\u00B0");
        }

        protected override void OnOverlay(SplineMesher mesher)
        {
            GUILayout.BeginArea(new Rect(12, 12, 210, 44), EditorStyles.helpBox);
            GUILayout.Label("Roll: drag the discs at the knots", EditorStyles.miniLabel);
            if (GUILayout.Button("Zero all rolls", EditorStyles.miniButton))
            {
                BeginEdit(mesher, "Reset Rolls");
                var splines = mesher.Container.Splines;
                for (int si = 0; si < splines.Count; si++)
                    for (int k = 0; k < splines[si].Count; k++)
                        mesher.GetPointData(si, k).roll = 0f;
                EndEdit(mesher);
            }
            GUILayout.EndArea();
        }
    }
}
