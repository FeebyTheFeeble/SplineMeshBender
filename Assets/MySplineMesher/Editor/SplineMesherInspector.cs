using UnityEditor;
using UnityEngine;

namespace SC.Modeling.Splines.Editing
{
    [CustomEditor(typeof(SplineMesher))]
    [CanEditMultipleObjects]
    public class SplineMesherInspector : Editor
    {
        SerializedProperty container, sourceMesh, settings, onRebuilt, startCap, endCap, flipStartCap;

        void OnEnable()
        {
            container = serializedObject.FindProperty("container");
            sourceMesh = serializedObject.FindProperty("sourceMesh");
            settings = serializedObject.FindProperty("settings");
            onRebuilt = serializedObject.FindProperty("onRebuilt");
            startCap = serializedObject.FindProperty("startCap");
            endCap = serializedObject.FindProperty("endCap");
            flipStartCap = serializedObject.FindProperty("flipStartCap");
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        void OnUndoRedo()
        {
            foreach (var t in targets)
            {
                var m = t as SplineMesher;
                if (m != null) m.SetDirty();
            }
        }

        SerializedProperty S(string name)
        {
            return settings.FindPropertyRelative(name);
        }

        void Field(string name)
        {
            EditorGUILayout.PropertyField(S(name));
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // ---- header
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(AssetInfo.Name, UI.HeaderStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Help", EditorStyles.miniButton, GUILayout.Width(50))) HelpWindow.Open();
            EditorGUILayout.EndHorizontal();

            // ---- source
            EditorGUILayout.PropertyField(container);
            EditorGUILayout.PropertyField(sourceMesh);

            if (container.objectReferenceValue == null && !container.hasMultipleDifferentValues)
                UI.Info("Assign a Spline Container (or add one to this GameObject).", MessageType.Warning);
            var src = sourceMesh.objectReferenceValue as Mesh;
            if (sourceMesh.objectReferenceValue == null && !sourceMesh.hasMultipleDifferentValues)
                UI.Info("Assign a Source Mesh to repeat along the spline.", MessageType.Info);
            else if (src != null && !src.isReadable)
                UI.Info("The source mesh is not readable. Enable Read/Write in its import settings.", MessageType.Error);

            // ---- mesh
            if (UI.Foldout("mesh", "Mesh"))
            {
                UI.BeginBox();
                Field("forwardAxis");
                Field("flipForward");
                Field("repeatMode");
                Field("scale");
                Field("roll");
                Field("upMode");
                Field("maxRepeats");
                UI.EndBox();
            }

            if (UI.Foldout("quality", "Quality, UV & Color", false))
            {
                UI.BeginBox();
                Field("sampleSpacing");
                Field("normalMode");
                Field("uvScale");
                Field("uvOffset");
                Field("useVertexColors");
                Field("generateLightmapUVs");
                UI.EndBox();
            }

            if (UI.Foldout("collider", "Collider", false))
            {
                UI.BeginBox();
                Field("colliderMode");
                if (S("colliderMode").enumValueIndex == (int)ColliderMode.Boxes && !S("colliderMode").hasMultipleDifferentValues)
                {
                    Field("colliderSegmentLength");
                    Field("showColliders");
                }
                UI.EndBox();
            }

            if (UI.Foldout("conform", "Ground Conforming", false))
            {
                UI.BeginBox();
                Field("followGround");
                Field("conform");
                bool anyGround = S("followGround").boolValue || S("conform").boolValue;
                using (new EditorGUI.DisabledScope(!anyGround))
                {
                    Field("conformMask");
                    Field("conformRayHeight");
                    Field("conformMaxDistance");
                    Field("conformOffset");
                }
                if (S("followGround").boolValue)
                    UI.Info("Follow Ground snaps the spline path to the terrain. Move a knot or press Rebuild Now after changing the terrain.");
                if (S("conform").boolValue)
                    UI.Info("Tip: with per-vertex Conform, use Normal Mode = Recalculate.");
                UI.EndBox();
            }

            if (UI.Foldout("caps", "End Caps", false))
            {
                UI.BeginBox();
                EditorGUILayout.PropertyField(startCap);
                EditorGUILayout.PropertyField(endCap);
                EditorGUILayout.PropertyField(flipStartCap);
                UI.EndBox();
            }

            if (UI.Foldout("points", "Per-Point Tools"))
            {
                UI.BeginBox();
                if (targets.Length == 1)
                {
                    EditorGUILayout.BeginHorizontal();
                    UI.ToolButton<RollTool>("Roll", "Twist the mesh around the spline per knot.");
                    UI.ToolButton<ScaleTool>("Scale", "Scale the cross-section per knot.");
                    UI.ToolButton<VertexColorTool>("Color", "Paint vertex colors per knot.");
                    UI.ToolButton<ConformingTool>("Conform", "Adjust ground conforming settings in the scene.");
                    EditorGUILayout.EndHorizontal();

                    if (GUILayout.Button("Reset Point Data"))
                    {
                        var m = (SplineMesher)target;
                        Undo.RecordObject(m, "Reset Point Data");
                        m.ResetPointData();
                        EditorUtility.SetDirty(m);
                    }
                }
                else
                {
                    UI.Info("Select a single Spline Mesher to use the per-point tools.");
                }
                UI.EndBox();
            }

            if (UI.Foldout("events", "Events", false))
                EditorGUILayout.PropertyField(onRebuilt);

            // ---- info + actions
            if (targets.Length == 1)
            {
                var m = (SplineMesher)target;
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Spline length", m.TotalLength.ToString("F2") + " m");
                EditorGUILayout.LabelField("Vertices", m.VertexCount.ToString("N0"));
            }

            if (GUILayout.Button("Rebuild Now"))
            {
                foreach (var t in targets)
                {
                    var m = t as SplineMesher;
                    if (m != null) m.Rebuild();
                }
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
