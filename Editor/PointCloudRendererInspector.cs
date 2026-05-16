// Pcx - Point cloud importer & renderer for Unity
using UnityEngine;
using UnityEditor;

namespace Pcx {
    [CanEditMultipleObjects]
    [CustomEditor(typeof(PointCloudRenderer))]
    public class PointCloudRendererInspector : Editor {
        SerializedProperty _customSplatMaterial; // <-- We added this
        SerializedProperty _sourceData;
        SerializedProperty _pointTint;
        SerializedProperty _pointSize;

        void OnEnable() {
            _customSplatMaterial = serializedObject.FindProperty("_customSplatMaterial"); // <-- We added this
            _sourceData = serializedObject.FindProperty("_sourceData");
            _pointTint = serializedObject.FindProperty("_pointTint");
            _pointSize = serializedObject.FindProperty("_pointSize");
        }

        public override void OnInspectorGUI() {
            serializedObject.Update();

            // Draw our new custom material slot at the very top
            EditorGUILayout.LabelField("Stylized Gaussian Rendering", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_customSplatMaterial, new GUIContent("Custom Splat Material"));

            EditorGUILayout.Space();

            // Draw the legacy stuff below it
            EditorGUILayout.LabelField("Legacy Pcx Settings (Ignored if Material assigned)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_sourceData);
            EditorGUILayout.PropertyField(_pointTint);
            EditorGUILayout.PropertyField(_pointSize);

            serializedObject.ApplyModifiedProperties();
        }
    }
}