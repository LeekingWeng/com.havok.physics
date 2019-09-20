using UnityEditor;
using UnityEngine;
using Havok.Physics.Authoring;

namespace Havok.Physics.Editor
{
    [CustomPropertyDrawer(typeof(HavokConfigurationAuthoring.VisualDebuggerConfiguation))]
    class VisualDebuggerConfiguationDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
            EditorGUI.GetPropertyHeight(property, label, true);

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            position.height = EditorGUI.GetPropertyHeight(property, label, false);
            EditorGUI.PropertyField(position, property, label);
            if (property.isExpanded)
            {
                ++EditorGUI.indentLevel;

                var childProperty = property.Copy();
                var endProperty = childProperty.GetEndProperty();

                // first property is EnableVisualDebugger
                childProperty.NextVisible(true);
                position.y += position.height + EditorGUIUtility.standardVerticalSpacing;
                position.height = EditorGUI.GetPropertyHeight(childProperty);
                EditorGUI.PropertyField(position, childProperty);

                EditorGUI.BeginDisabledGroup(!childProperty.boolValue);
                while (childProperty.NextVisible(false) && !SerializedProperty.EqualContents(childProperty, endProperty))
                {
                    position.y += position.height + EditorGUIUtility.standardVerticalSpacing;
                    position.height = EditorGUI.GetPropertyHeight(childProperty);
                    EditorGUI.PropertyField(position, childProperty);
                }

                EditorGUI.EndDisabledGroup();

                --EditorGUI.indentLevel;
            }

            EditorGUI.EndProperty();
        }
    }
}
