using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

namespace SC.Modeling.Splines.Editing
{
    /// <summary>Visualises and adjusts the downward ground-conforming rays.</summary>
    [EditorTool("Spline Mesher: Conform", typeof(SplineMesher))]
    public class ConformingTool : PointEditTool
    {
        static GUIContent icon;

        public override GUIContent toolbarIcon
        {
            get
            {
                if (icon == null)
                    icon = new GUIContent(EditorGUIUtility.IconContent("MoveTool").image, "Spline Mesher: Conform");
                return icon;
            }
        }

        protected override void OnPoint(SplineMesher mesher, int si, int k, PointData data, Vector3 pos, Quaternion rot)
        {
            Settings s = mesher.Settings;
            Vector3 top = pos + Vector3.up * s.conformRayHeight;
            Vector3 bottom = pos + Vector3.down * s.conformMaxDistance;

            Handles.color = (s.conform || s.followGround) ? new Color(0.4f, 1f, 0.4f, 0.9f) : new Color(1f, 1f, 1f, 0.35f);
            Handles.DrawDottedLine(top, bottom, 4f);

            // drag the cone to change how high the rays start
            float size = HandleUtility.GetHandleSize(top) * 0.12f;
            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.Slider(top, Vector3.up, size, Handles.ConeHandleCap, 0f);
            if (EditorGUI.EndChangeCheck())
            {
                BeginEdit(mesher, "Change Conform Ray Height");
                s.conformRayHeight = Mathf.Max(0.1f, moved.y - pos.y);
                EndEdit(mesher);
            }

            if (Physics.Raycast(top, Vector3.down, out RaycastHit hit, s.conformRayHeight + s.conformMaxDistance, s.conformMask, QueryTriggerInteraction.Ignore))
            {
                Handles.color = Color.yellow;
                Handles.DrawWireDisc(hit.point, hit.normal, HandleUtility.GetHandleSize(hit.point) * 0.1f);
            }
        }

        protected override void OnOverlay(SplineMesher mesher)
        {
            Settings s = mesher.Settings;
            GUILayout.BeginArea(new Rect(12, 12, 230, 130), EditorStyles.helpBox);

            EditorGUI.BeginChangeCheck();
            bool follow = EditorGUILayout.ToggleLeft("Follow ground (path)", s.followGround);
            bool conform = EditorGUILayout.ToggleLeft("Conform vertices", s.conform);
            float offset = EditorGUILayout.FloatField("Offset", s.conformOffset);
            float maxDist = EditorGUILayout.FloatField("Max distance", s.conformMaxDistance);
            if (EditorGUI.EndChangeCheck())
            {
                BeginEdit(mesher, "Change Conform Settings");
                s.followGround = follow;
                s.conform = conform;
                s.conformOffset = offset;
                s.conformMaxDistance = Mathf.Max(0f, maxDist);
                EndEdit(mesher);
            }

            GUILayout.Label("Drag the cones to set ray start height", EditorStyles.miniLabel);
            GUILayout.EndArea();
        }
    }
}
