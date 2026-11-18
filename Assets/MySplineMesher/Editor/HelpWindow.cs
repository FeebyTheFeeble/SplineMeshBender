using UnityEditor;
using UnityEngine;

namespace SC.Modeling.Splines.Editing
{
    public class HelpWindow : EditorWindow
    {
        Vector2 scroll;

        [MenuItem("Tools/Spline Mesher/Help")]
        public static void Open()
        {
            var w = GetWindow<HelpWindow>(true, AssetInfo.Name + " Help");
            w.minSize = new Vector2(380, 420);
            w.Show();
        }

        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            GUILayout.Space(6);
            GUILayout.Label(AssetInfo.Name + "  v" + AssetInfo.Version, UI.HeaderStyle);
            GUILayout.Space(4);

            UI.BeginBox();
            GUILayout.Label("Quick start", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "1. GameObject > Spline Mesher > Spline Mesher (or right-click a Spline Container > Add Spline Mesher).\n" +
                "2. Assign a Source Mesh. It needs Read/Write enabled in its import settings.\n" +
                "3. Edit the spline with Unity's Spline tools. The mesh follows live.\n" +
                "4. Use the Roll, Scale, Vertex Color and Conform tools (inspector buttons) for per-point control.",
                EditorStyles.wordWrappedLabel);
            UI.EndBox();

            UI.BeginBox();
            GUILayout.Label("Tips", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "- The source mesh needs enough edge loops along its length to bend smoothly.\n" +
                "- Pick the Forward Axis that matches your mesh's length direction.\n" +
                "- Negative X scale mirrors the mesh (guardrails on both sides of a road).\n" +
                "- With Ground Conforming use the Recalculate normal mode and keep your own colliders on another layer than the conform mask.\n" +
                "- Per-point data is matched to knots by index.",
                EditorStyles.wordWrappedLabel);
            UI.EndBox();

            UI.BeginBox();
            GUILayout.Label("Links", EditorStyles.boldLabel);
            UI.Link("Documentation", AssetInfo.DocumentationUrl);
            UI.Link("Support", AssetInfo.SupportUrl);
            UI.Link("Leave a review", AssetInfo.ReviewUrl);
            UI.EndBox();

            EditorGUILayout.EndScrollView();
        }
    }
}
