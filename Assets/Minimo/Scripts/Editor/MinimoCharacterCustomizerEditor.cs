using System;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MinimoCharacterCustomizer), true)]
[CanEditMultipleObjects]
public sealed class MinimoCharacterCustomizerEditor : Editor
{
    private bool showCore = true;
    private bool showSource = true;
    private bool showVariants = true;

    private GUIStyle cardStyle;
    private GUIStyle titleStyle;
    private GUIStyle subtitleStyle;

    public override void OnInspectorGUI()
    {
        EnsureStyles();
        serializedObject.Update();

        EditorGUILayout.BeginVertical(cardStyle);
        EditorGUILayout.LabelField(
            new GUIContent("Customizer", "Character slot/variant setup settings."),
            titleStyle);
        EditorGUILayout.LabelField(
            "Inspector view is simplified. Runtime behavior is unchanged.",
            subtitleStyle);
        EditorGUILayout.EndVertical();

        DrawGroup(ref showCore, "Core", "Renderer binding and startup behavior.", () =>
        {
            DrawProp("autoBindExistingRenderers", "Auto Bind", "Bind slot renderers from scene hierarchy automatically.");
            DrawProp("normalizeChildNames", "Fix Names", "Normalize child names to expected slot names.");
            DrawProp("applyOnStart", "Apply Start", "Apply current selections automatically on Start.");
            DrawProp("defaultVariantForAllParts", "Default Set", "Default variant index for all parts.");
            DrawProp("keepAssignedMaterialsOnApply", "Keep Mats", "Keep existing materials when applying meshes.");
        });

        DrawGroup(ref showSource, "Source", "Variant source mode settings.", () =>
        {
            DrawProp("useCustomizeFolderSource", "Use Custom", "Use Customize folder source instead of manual list.");
            DrawProp("customizeRootFolder", "Root Path", "Project folder path used for Customize source scan.");
            DrawProp("autoLoadCustomizeFolderInEditor", "Auto Load", "Refresh Customize source data automatically in editor.");
        });

        DrawGroup(ref showVariants, "Variants", "Manual list used when Use Custom is disabled.", () =>
        {
            SerializedProperty useCustom = serializedObject.FindProperty("useCustomizeFolderSource");
            bool customEnabled = useCustom != null && useCustom.boolValue && !useCustom.hasMultipleDifferentValues;
            if (customEnabled)
            {
                EditorGUILayout.HelpBox(
                    "Use Custom is enabled. Manual Variants list is ignored until this mode is disabled.",
                    MessageType.Info);
            }

            using (new EditorGUI.DisabledScope(customEnabled))
            {
                DrawProp("variants", "List", "Manual variant definitions.", true);
            }
        });

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawProp(string propertyName, string label, string tooltip, bool includeChildren = false)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null || !MinimoInspectorVisibility.ShouldDrawProperty(property))
        {
            return;
        }

        EditorGUILayout.PropertyField(property, new GUIContent(label, tooltip), includeChildren);
    }

    private void DrawGroup(ref bool expanded, string title, string description, Action drawBody)
    {
        EditorGUILayout.BeginVertical(cardStyle);
        expanded = EditorGUILayout.Foldout(expanded, new GUIContent(title, description), true);
        if (expanded)
        {
            EditorGUILayout.LabelField(description, subtitleStyle);
            EditorGUILayout.Space(2f);
            drawBody?.Invoke();
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(2f);
    }

    private void EnsureStyles()
    {
        if (cardStyle == null)
        {
            cardStyle = new GUIStyle("HelpBox")
            {
                padding = new RectOffset(12, 12, 10, 10),
                margin = new RectOffset(0, 0, 4, 4)
            };
        }

        if (titleStyle == null)
        {
            titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12
            };
        }

        if (subtitleStyle == null)
        {
            subtitleStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                wordWrap = true
            };
        }
    }
}
