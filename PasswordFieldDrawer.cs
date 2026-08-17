using _project.Scripts.Core;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace _project.Scripts.Core
{
    /// <summary>
    ///     Masks a string field in the Inspector so its value shows as dots instead of plain text.
    ///     Purely a display convenience — the value is still stored in plain text in the scene/prefab asset.
    /// </summary>
    public class PasswordFieldAttribute : PropertyAttribute
    {
    }
}

#if UNITY_EDITOR
namespace _project.Scripts.Editor
{
    [CustomPropertyDrawer(typeof(PasswordFieldAttribute))]
    public class PasswordFieldDrawer : PropertyDrawer
    {
        private const float ToggleWidth = 46f;
        private bool _revealed;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.LabelField(position, label.text,
                    $"[{nameof(PasswordFieldAttribute)}] only works on string fields.");
                return;
            }

            var fieldRect = new Rect(position.x, position.y, position.width - ToggleWidth - 2f, position.height);
            var toggleRect = new Rect(position.xMax - ToggleWidth, position.y, ToggleWidth, position.height);

            EditorGUI.BeginProperty(position, label, property);

            EditorGUI.BeginChangeCheck();
            var value = _revealed
                ? EditorGUI.TextField(fieldRect, label, property.stringValue)
                : EditorGUI.PasswordField(fieldRect, label, property.stringValue);
            if (EditorGUI.EndChangeCheck())
                property.stringValue = value;

            _revealed = GUI.Toggle(toggleRect, _revealed, _revealed ? "Hide" : "Show", EditorStyles.miniButton);

            EditorGUI.EndProperty();
        }
    }
}
#endif