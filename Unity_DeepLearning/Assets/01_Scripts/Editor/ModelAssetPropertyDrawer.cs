using Unity.InferenceEngine;
using UnityEditor;
using UnityEngine;

namespace DeepLearning.Editor
{
    [CustomPropertyDrawer(typeof(ModelAsset), true)]
    public sealed class ModelAssetPropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            if (property.propertyType != SerializedPropertyType.ObjectReference)
            {
                EditorGUI.LabelField(position, label.text, "ModelAsset 참조에만 사용할 수 있습니다.");
                EditorGUI.EndProperty();
                return;
            }

            EditorGUI.BeginChangeCheck();
            ModelAsset selectedModel = EditorGUI.ObjectField(
                position,
                label,
                property.objectReferenceValue,
                typeof(ModelAsset),
                false) as ModelAsset;

            if (EditorGUI.EndChangeCheck())
            {
                property.objectReferenceValue = selectedModel;
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }
    }
}
