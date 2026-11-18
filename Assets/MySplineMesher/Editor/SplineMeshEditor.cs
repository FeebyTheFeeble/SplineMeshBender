using Unity.Mathematics;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Splines;

namespace SC.Modeling.Splines.Editing
{
    /// <summary>Menu commands and shared helpers for creating and editing Spline Meshers.</summary>
    public static class SplineMeshEditor
    {
        [MenuItem("GameObject/Spline Mesher/Spline Mesher", false, 10)]
        static void CreateMenu(MenuCommand cmd)
        {
            var m = CreateSplineMesher("Spline Mesher", null);
            if (cmd.context is GameObject parent)
                GameObjectUtility.SetParentAndAlign(m.gameObject, parent);
        }

        [MenuItem("CONTEXT/SplineContainer/Add Spline Mesher")]
        static void AddToContainer(MenuCommand cmd)
        {
            var container = (SplineContainer)cmd.context;
            var mesher = Undo.AddComponent<SplineMesher>(container.gameObject);
            mesher.Container = container;
            ApplyDefaultMaterial(mesher);
        }

        [MenuItem("CONTEXT/SplineContainer/Add Spline Mesher", true)]
        static bool AddToContainerValidate(MenuCommand cmd)
        {
            var container = cmd.context as SplineContainer;
            return container != null && container.GetComponent<SplineMesher>() == null;
        }

        /// <summary>Creates a GameObject with a spline container (a gentle S-curve) and a SplineMesher.</summary>
        public static SplineMesher CreateSplineMesher(string name, Mesh source)
        {
            var go = new GameObject(name, typeof(SplineContainer));
            Vector3 pos = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : Vector3.zero;
            go.transform.position = pos;

            var container = go.GetComponent<SplineContainer>();
            if (container.Splines.Count == 0) container.AddSpline(new Spline());
            Spline spline = container.Splines[0];
            if (spline.Count == 0)
            {
                spline.Add(new BezierKnot(new float3(-10f, 0f, 0f)), TangentMode.AutoSmooth);
                spline.Add(new BezierKnot(new float3(0f, 0f, 4f)), TangentMode.AutoSmooth);
                spline.Add(new BezierKnot(new float3(10f, 0f, -2f)), TangentMode.AutoSmooth);
            }

            var mesher = go.AddComponent<SplineMesher>();
            mesher.Container = container;
            mesher.SourceMesh = source;
            ApplyDefaultMaterial(mesher);

            Undo.RegisterCreatedObjectUndo(go, "Create Spline Mesher");
            Selection.activeGameObject = go;
            return mesher;
        }

        public static void ApplyDefaultMaterial(SplineMesher mesher)
        {
            var r = mesher.GetComponent<MeshRenderer>();
            if (r == null || (r.sharedMaterial != null && r.sharedMaterial.shader != null && r.sharedMaterial.shader.name != "Hidden/InternalErrorShader")) return;
            Material mat = null;
            var rp = GraphicsSettings.currentRenderPipeline;
            if (rp != null) mat = rp.defaultMaterial;
            if (mat == null) mat = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Material.mat");
            if (mat != null) r.sharedMaterial = mat;
        }
    }

    /// <summary>
    /// Base class for the scene tools that edit per-knot data. Draws nothing itself;
    /// derived tools implement <see cref="OnPoint"/> once per knot.
    /// </summary>
    public abstract class PointEditTool : EditorTool
    {
        protected SplineMesher Mesher { get { return target as SplineMesher; } }

        public override void OnToolGUI(EditorWindow window)
        {
            if (!(window is SceneView)) return;
            SplineMesher m = Mesher;
            if (m == null || m.Container == null) return;

            m.EnsurePointData();
            var splines = m.Container.Splines;
            for (int si = 0; si < splines.Count; si++)
            {
                int knots = splines[si].Count;
                for (int k = 0; k < knots; k++)
                {
                    if (!m.TryGetKnotFrame(si, k, out Vector3 pos, out Quaternion rot)) continue;
                    PointData pd = m.GetPointData(si, k);
                    if (pd == null) continue;
                    OnPoint(m, si, k, pd, pos, rot);
                }
            }

            Handles.BeginGUI();
            OnOverlay(m);
            Handles.EndGUI();
        }

        protected abstract void OnPoint(SplineMesher mesher, int splineIndex, int knotIndex, PointData data, Vector3 worldPos, Quaternion worldRot);

        protected virtual void OnOverlay(SplineMesher mesher) { }

        /// <summary>Call before changing data so the change can be undone.</summary>
        protected static void BeginEdit(SplineMesher m, string label)
        {
            Undo.RecordObject(m, label);
        }

        /// <summary>Call after changing data.</summary>
        protected static void EndEdit(SplineMesher m)
        {
            EditorUtility.SetDirty(m);
            m.SetDirty();
        }
    }
}
