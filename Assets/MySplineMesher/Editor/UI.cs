using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

namespace SC.Modeling.Splines.Editing
{
    /// <summary>Small IMGUI helpers shared by the inspector, help window and tools.</summary>
    public static class UI
    {
        static GUIStyle headerStyle;
        static GUIStyle linkStyle;

        public static GUIStyle HeaderStyle
        {
            get
            {
                if (headerStyle == null)
                    headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };
                return headerStyle;
            }
        }

        static GUIStyle LinkStyle
        {
            get
            {
                if (linkStyle == null)
                {
                    linkStyle = new GUIStyle(EditorStyles.label) { stretchWidth = false };
                    linkStyle.normal.textColor = new Color(0.35f, 0.65f, 1f);
                    linkStyle.hover.textColor = new Color(0.55f, 0.8f, 1f);
                }
                return linkStyle;
            }
        }

        /// <summary>Foldout whose open/closed state is remembered for the editor session.</summary>
        public static bool Foldout(string key, string title, bool defaultOpen = true)
        {
            string k = "SC.SplineMesher.Foldout." + key;
            bool open = SessionState.GetBool(k, defaultOpen);
            bool now = EditorGUILayout.BeginFoldoutHeaderGroup(open, title);
            EditorGUILayout.EndFoldoutHeaderGroup();
            if (now != open) SessionState.SetBool(k, now);
            return now;
        }

        public static void BeginBox()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        }

        public static void EndBox()
        {
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }

        /// <summary>A toggle button that activates / deactivates the given scene tool.</summary>
        public static void ToolButton<T>(string label, string tooltip) where T : EditorTool
        {
            bool active = ToolManager.activeToolType == typeof(T);
            EditorGUI.BeginChangeCheck();
            bool now = GUILayout.Toggle(active, new GUIContent(label, tooltip), EditorStyles.miniButton, GUILayout.Height(22));
            if (EditorGUI.EndChangeCheck())
            {
                if (now) ToolManager.SetActiveTool<T>();
                else ToolManager.RestorePreviousPersistentTool();
            }
        }

        /// <summary>Clickable text link that opens a URL.</summary>
        public static void Link(string label, string url)
        {
            Rect r = GUILayoutUtility.GetRect(new GUIContent(label), LinkStyle);
            EditorGUIUtility.AddCursorRect(r, MouseCursor.Link);
            if (GUI.Button(r, label, LinkStyle)) Application.OpenURL(url);
        }

        public static void Info(string message, MessageType type = MessageType.Info)
        {
            EditorGUILayout.HelpBox(message, type);
        }
    }
}
