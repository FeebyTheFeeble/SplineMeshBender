using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

namespace SC.Modeling.Splines.Editing
{
    /// <summary>Scale the mesh cross-section per knot (e.g. widen a road, flare a pipe).</summary>
    [EditorTool("Spline Mesher: Scale", typeof(SplineMesher))]
    public class ScaleTool : PointEditTool
    {
        static GUIContent icon;

        public override GUIContent toolbarIcon
        {
            get
            {
                if (icon == null)
                    icon = new GUIContent(EditorGUIUtility.IconContent("ScaleTool").image, "Spline Mesher: Scale");
                return icon;
            }
        }

        protected override void OnPoint(SplineMesher mesher, int si, int k, PointData data, Vector3 pos, Quaternion rot)
        {
            float size = HandleUtility.GetHandleSize(pos) * 0.15f;
            Vector3 handlePos = pos + rot * Vector3.up * HandleUtility.GetHandleSize(pos) * 0.6f;

            Handles.color = new Color(1f, 0.75f, 0.2f, 1f);
            Handles.DrawLine(pos, handlePos);

            EditorGUI.BeginChangeCheck();
            float scaled = Handles.ScaleValueHandle(data.scale, handlePos, rot, size, Handles.CubeHandleCap, 0f);
            if (EditorGUI.EndChangeCheck())
            {
                BeginEdit(mesher, "Scale Spline Point");
                data.scale = Mathf.Max(0.01f, scaled);
                EndEdit(mesher);
            }

            Handles.Label(handlePos + rot * Vector3.up * size * 2f, "x" + data.scale.ToString("F2"));
        }

        protected override void OnOverlay(SplineMesher mesher)
        {
            GUILayout.BeginArea(new Rect(12, 12, 210, 44), EditorStyles.helpBox);
            GUILayout.Label("Scale: drag the cubes at the knots", EditorStyles.miniLabel);
            if (GUILayout.Button("Reset all scales", EditorStyles.miniButton))
            {
                BeginEdit(mesher, "Reset Scales");
                var splines = mesher.Container.Splines;
                for (int si = 0; si < splines.Count; si++)
                    for (int k = 0; k < splines[si].Count; k++)
                        mesher.GetPointData(si, k).scale = 1f;
                EndEdit(mesher);
            }
            GUILayout.EndArea();
        }
    }
}
