using System;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MinimoMovingPlatformRider))]
[CanEditMultipleObjects]
public sealed class MinimoMovingPlatformRiderEditor : Editor
{
    private bool showDetect = true;
    private bool showAttach = true;
    private bool showBody = true;

    private GUIStyle cardStyle;
    private GUIStyle titleStyle;
    private GUIStyle subtitleStyle;

    public override void OnInspectorGUI()
    {
        EnsureStyles();
        serializedObject.Update();

        EditorGUILayout.BeginVertical(cardStyle);
        EditorGUILayout.LabelField(
            new GUIContent("Platform Rider", "Moving platform attachment and detach behavior."),
            titleStyle);
        EditorGUILayout.LabelField(
            "Runtime behavior is unchanged. This is inspector layout only.",
            subtitleStyle);
        EditorGUILayout.EndVertical();

        DrawGroup(ref showDetect, "Detect", "Platform detection settings.", () =>
        {
            DrawProp("platformDetectionMask", "Mask", "Layers treated as moving platforms.");
            DrawProp("platformProbeDistance", "Probe Dist", "Downward probe distance for platform detection.");
            DrawProp("platformProbeRadiusScale", "Probe Radius", "Probe radius multiplier based on controller radius.");
            DrawProp("requireGrounded", "Need Ground", "Require grounded state before platform attach.");
        });

        DrawGroup(ref showAttach, "Attach", "Contact grace and airborne probe behavior.", () =>
        {
            DrawProp("platformContactLostGraceTime", "Contact Grace", "Grace period before detaching after contact loss.");
            DrawProp("airborneProbeExtraDistance", "Air Probe", "Extra probe distance while airborne.");
        });

        DrawGroup(ref showBody, "Body", "Local rigidbody behavior during attachment.", () =>
        {
            DrawProp("disableRigidbodyGravityWhileAttached", "Disable Grav", "Disable attached rigidbody gravity while on platform.");
        });

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawProp(string propertyName, string label, string tooltip)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null || !MinimoInspectorVisibility.ShouldDrawProperty(property))
        {
            return;
        }

        EditorGUILayout.PropertyField(property, new GUIContent(label, tooltip), true);
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
