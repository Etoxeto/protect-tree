using ProtectTree.Runtime.Board;
using UnityEditor;
using UnityEngine;

namespace ProtectTree.Editor
{
    [CustomEditor(typeof(BoardVisualDefinition))]
    public sealed class BoardVisualDefinitionEditor : UnityEditor.Editor
    {
        private const float CellButtonWidth = 46f;
        private Vector2 _gridScroll;
        private int _selectedX;
        private int _selectedY;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawPropertiesExcluding(
                serializedObject,
                "m_Script",
                "visualGridWidth",
                "visualGridHeight",
                "cellOverrides");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Per-Cell Visual Overrides", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "This grid changes Unity visuals only. It does not modify Lua terrain, zones, routes, height, deployment, or combat rules.",
                MessageType.Info);

            SerializedProperty width = serializedObject.FindProperty("visualGridWidth");
            SerializedProperty height = serializedObject.FindProperty("visualGridHeight");
            EditorGUILayout.PropertyField(width, new GUIContent("Grid Width"));
            EditorGUILayout.PropertyField(height, new GUIContent("Grid Height"));
            width.intValue = Mathf.Max(1, width.intValue);
            height.intValue = Mathf.Max(1, height.intValue);

            _selectedX = Mathf.Clamp(_selectedX, 0, width.intValue - 1);
            _selectedY = Mathf.Clamp(_selectedY, 0, height.intValue - 1);

            DrawGrid(width.intValue, height.intValue);
            DrawSelectedCell();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawGrid(int width, int height)
        {
            SerializedProperty overrides = serializedObject.FindProperty("cellOverrides");
            _gridScroll = EditorGUILayout.BeginScrollView(
                _gridScroll,
                GUILayout.Height(Mathf.Min(260f, height * 25f + 22f)));

            for (int y = height - 1; y >= 0; y--)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label($"y={y}", GUILayout.Width(32f));

                for (int x = 0; x < width; x++)
                {
                    bool selected = x == _selectedX && y == _selectedY;
                    bool hasOverride = FindOverrideIndex(overrides, x, y) >= 0;
                    Color previousColor = GUI.backgroundColor;
                    GUI.backgroundColor = selected
                        ? new Color(0.2f, 0.85f, 1f)
                        : hasOverride ? new Color(0.45f, 0.9f, 0.45f) : previousColor;

                    if (GUILayout.Button($"{x},{y}", GUILayout.Width(CellButtonWidth)))
                    {
                        _selectedX = x;
                        _selectedY = y;
                    }

                    GUI.backgroundColor = previousColor;
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawSelectedCell()
        {
            SerializedProperty overrides = serializedObject.FindProperty("cellOverrides");
            int index = FindOverrideIndex(overrides, _selectedX, _selectedY);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                $"Selected Cell ({_selectedX}, {_selectedY})",
                EditorStyles.boldLabel);

            if (index < 0)
            {
                if (GUILayout.Button("Create Top Material Override"))
                {
                    index = overrides.arraySize;
                    overrides.InsertArrayElementAtIndex(index);
                    InitializeOverride(overrides.GetArrayElementAtIndex(index));
                }

                return;
            }

            SerializedProperty entry = overrides.GetArrayElementAtIndex(index);
            DrawSurfaceOverride(entry, "Top", "overrideTop", "topMaterial", "fallbackTopColor");
            DrawSurfaceOverride(
                entry,
                "Front Side",
                "overrideFrontSide",
                "frontSideMaterial",
                "fallbackFrontSideColor");
            DrawSurfaceOverride(
                entry,
                "Left Side",
                "overrideLeftSide",
                "leftSideMaterial",
                "fallbackLeftSideColor");
            DrawSurfaceOverride(
                entry,
                "Right Side",
                "overrideRightSide",
                "rightSideMaterial",
                "fallbackRightSideColor");

            EditorGUILayout.Space();
            if (GUILayout.Button("Remove Cell Override"))
            {
                overrides.DeleteArrayElementAtIndex(index);
            }
        }

        private static void DrawSurfaceOverride(
            SerializedProperty entry,
            string label,
            string enabledName,
            string materialName,
            string fallbackColorName)
        {
            SerializedProperty enabled = entry.FindPropertyRelative(enabledName);
            EditorGUILayout.PropertyField(enabled, new GUIContent($"Override {label}"));
            if (!enabled.boolValue)
            {
                return;
            }

            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(
                entry.FindPropertyRelative(materialName),
                new GUIContent($"{label} Material"));
            EditorGUILayout.PropertyField(
                entry.FindPropertyRelative(fallbackColorName),
                new GUIContent($"{label} Fallback Color"));
            EditorGUI.indentLevel--;
        }

        private void InitializeOverride(SerializedProperty entry)
        {
            entry.FindPropertyRelative("gridX").intValue = _selectedX;
            entry.FindPropertyRelative("gridY").intValue = _selectedY;
            entry.FindPropertyRelative("overrideTop").boolValue = true;
            entry.FindPropertyRelative("topMaterial").objectReferenceValue = null;
            entry.FindPropertyRelative("fallbackTopColor").colorValue = Color.white;

            entry.FindPropertyRelative("overrideFrontSide").boolValue = false;
            entry.FindPropertyRelative("frontSideMaterial").objectReferenceValue = null;
            entry.FindPropertyRelative("fallbackFrontSideColor").colorValue =
                new Color(0.45f, 0.35f, 0.18f, 1f);
            entry.FindPropertyRelative("overrideLeftSide").boolValue = false;
            entry.FindPropertyRelative("leftSideMaterial").objectReferenceValue = null;
            entry.FindPropertyRelative("fallbackLeftSideColor").colorValue =
                new Color(0.38f, 0.3f, 0.16f, 1f);
            entry.FindPropertyRelative("overrideRightSide").boolValue = false;
            entry.FindPropertyRelative("rightSideMaterial").objectReferenceValue = null;
            entry.FindPropertyRelative("fallbackRightSideColor").colorValue =
                new Color(0.5f, 0.4f, 0.22f, 1f);
        }

        private static int FindOverrideIndex(
            SerializedProperty overrides,
            int gridX,
            int gridY)
        {
            for (int index = 0; index < overrides.arraySize; index++)
            {
                SerializedProperty entry = overrides.GetArrayElementAtIndex(index);
                if (entry.FindPropertyRelative("gridX").intValue == gridX
                    && entry.FindPropertyRelative("gridY").intValue == gridY)
                {
                    return index;
                }
            }

            return -1;
        }
    }
}
