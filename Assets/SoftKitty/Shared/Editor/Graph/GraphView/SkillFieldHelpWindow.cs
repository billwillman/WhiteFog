using System;
using UnityEditor;
using UnityEngine;

namespace SoftKitty
{
    internal sealed class SkillFieldHelpWindow : EditorWindow
    {
        private static SkillFieldHelpWindow current;
        [SerializeField] private string fieldTitle;
        [SerializeField] private string explanation;
        private Vector2 scroll;

        internal static void Open(string title, string text)
        {
            bool create = !current;
            var window = create ? CreateInstance<SkillFieldHelpWindow>() : current;
            window.fieldTitle = title;
            window.explanation = text;
            window.scroll = Vector2.zero;
            if (create)
            {
                window.titleContent = new GUIContent("技能设置说明");
                window.minSize = new Vector2(320, 140);
                var center = focusedWindow ? focusedWindow.position.center : new Vector2(400, 300);
                window.position = new Rect(center.x - 210, center.y - 90, 420, 180);
                window.ShowUtility();
            }
            else window.Focus();
            window.Repaint();
        }

        private void OnEnable() => current = this;

        private void OnGUI()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.inspectorDefaultMargins))
            using (var view = new EditorGUILayout.ScrollViewScope(scroll))
            {
                scroll = view.scrollPosition;
                EditorGUILayout.Space();
                EditorGUILayout.LabelField(fieldTitle, EditorStyles.boldLabel);
                EditorGUILayout.Space();
                EditorGUILayout.LabelField(explanation, EditorStyles.wordWrappedLabel);
            }
        }
    }

    internal static class SkillFieldHelp
    {
        private const float ButtonWidth = 22f;
        private static readonly GUIContent Icon = new GUIContent("?", "查看此设置的中文说明");
        private static readonly GUILayoutOption Width = GUILayout.Width(ButtonWidth);

        internal static void Button(string title, string text)
        {
            var rect = GUILayoutUtility.GetRect(ButtonWidth, EditorGUIUtility.singleLineHeight, Width);
            Button(rect, title, text);
        }

        internal static void Button(Rect rect, string title, string text)
        {
            bool enabled = GUI.enabled;
            bool changed = GUI.changed;
            GUI.enabled = true;
            bool open = GUI.Button(rect, Icon, EditorStyles.miniButton);
            GUI.enabled = enabled;
            GUI.changed = changed;
            if (open) EditorApplication.delayCall += () => SkillFieldHelpWindow.Open(title, text);
        }

        internal static int Popup(string title, int value, string[] options, string help)
        {
            using (new Row(title, help)) return EditorGUILayout.Popup(title, value, options);
        }

        internal static int IntPopup(string title, int value, string[] options, int[] values, string help)
        {
            using (new Row(title, help)) return EditorGUILayout.IntPopup(title, value, options, values);
        }

        internal static UnityEngine.Object ObjectField(string title, UnityEngine.Object value, Type type, bool sceneObjects, string help)
        {
            using (new Row(title, help)) return EditorGUILayout.ObjectField(title, value, type, sceneObjects);
        }

        internal static bool Toggle(string title, bool value, string help)
        {
            using (new Row(title, help)) return EditorGUILayout.Toggle(title, value);
        }

        internal static bool ToggleLeft(string title, bool value, string help)
        {
            using (new Row(title, help)) return EditorGUILayout.ToggleLeft(title, value);
        }

        internal static string TextField(string title, string value, string help)
        {
            using (new Row(title, help)) return EditorGUILayout.TextField(title, value);
        }

        internal static string DelayedTextField(string title, string value, string help)
        {
            using (new Row(title, help)) return EditorGUILayout.DelayedTextField(title, value);
        }

        internal static float FloatField(string title, float value, string help)
        {
            using (new Row(title, help)) return EditorGUILayout.FloatField(title, value);
        }

        internal static float DelayedFloatField(string title, float value, string help)
        {
            using (new Row(title, help)) return EditorGUILayout.DelayedFloatField(title, value);
        }

        internal static int DelayedIntField(string title, int value, string help)
        {
            using (new Row(title, help)) return EditorGUILayout.DelayedIntField(title, value);
        }

        internal static Vector3 Vector3Field(string title, Vector3 value, string help)
        {
            using (new Row(title, help)) return EditorGUILayout.Vector3Field(title, value);
        }

        internal static float Slider(string title, float value, float min, float max, string help)
        {
            using (new Row(title, help)) return EditorGUILayout.Slider(title, value, min, max);
        }

        internal static int MaskField(string title, int value, string[] options, string help)
        {
            using (new Row(title, help)) return EditorGUILayout.MaskField(title, value, options);
        }

        internal readonly struct Row : IDisposable
        {
            private readonly string title;
            private readonly string text;
            internal Row(string title, string text)
            {
                this.title = title;
                this.text = text;
                EditorGUILayout.BeginHorizontal();
            }
            public void Dispose()
            {
                Button(title, text);
                EditorGUILayout.EndHorizontal();
            }
        }
    }
}
