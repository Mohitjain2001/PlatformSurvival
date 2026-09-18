using System;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MinimoCustomizationUIController), true)]
[CanEditMultipleObjects]
public sealed class MinimoCustomizationUIControllerEditor : Editor
{
    private bool showCore = true;
    private bool showSaved = true;
    private bool showBuy = true;
    private bool showWorld = true;
    private bool showView = true;
    private bool showColors = true;
    private bool showMainPalette;
    private bool showSkinPalette;
    private bool showHairPalette;
    private bool showClothesPalette;
    private bool showEyesPalette;
    private bool showBrowsPalette;
    private bool showMakeupPalette;

    private GUIStyle cardStyle;
    private GUIStyle titleStyle;
    private GUIStyle subtitleStyle;

    public override void OnInspectorGUI()
    {
        EnsureStyles();
        serializedObject.Update();

        EditorGUILayout.BeginVertical(cardStyle);
        EditorGUILayout.LabelField(
            new GUIContent("Customizer", "Main customization UI settings."),
            titleStyle);
        EditorGUILayout.LabelField(
            "Only user-facing controls are shown here. Internal scene references are hidden.",
            subtitleStyle);
        EditorGUILayout.EndVertical();

        DrawGroup(ref showCore, "Core", "General UI behavior settings.", () =>
        {
            DrawProp("metarigObject", "Metarig", "GameObject kept active on the customization screen and before entering Playground.");
            DrawProp("preferBodySelectionUi", "Body", "Prefer body selection flow for part browsing.");
            DrawProp("showPanelsAsTabs", "Tabs", "Display main panels as tab-like views.");
            DrawProp("enableCustomColorPicker", "Picker", "Enable advanced custom color picker.");
            DrawProp("livePreviewCustomColor", "Live", "Apply custom color changes while dragging picker controls.");
            DrawProp("wrapSelection", "Wrap", "Loop previous/next navigation at list ends.");
            DrawProp("autoRotationSpeedDegreesPerSecond", "Spin", "Character auto-rotation speed in degrees per second.");
            DrawProp("persistAcrossSessions", "Persist", "Keep selected parts and colors across sessions.");
        });

        DrawGroup(ref showBuy, "Buy", "Category purchase panel options.", () =>
        {
            DrawProp("showBuyCategoryPanel", "Buy Category Panel", "Show the buy panel for categories without available content for the selected region.");
        });

        DrawGroup(ref showSaved, "Saved", "Saved characters panel options.", () =>
        {
            DrawProp("savedPanelShowsReadyCharactersOnly", "Filter", "Show only ready/complete entries in saved panel.");
            DrawProp("readyCharacterCardHeight", "Height", "Visual height of each saved character card.");
            DrawProp("showReadyCharacterSideArrows", "Arrows", "Show side arrows for saved character navigation.");
            DrawProp("readyCharacterArrowDistance", "Gap", "Distance between center and side arrows.");
            DrawProp("readyCharacterArrowSize", "Size", "Size of saved panel side arrow buttons.");
        });

        DrawGroup(ref showWorld, "World", "World-space selection and focus.", () =>
        {
            DrawProp("focusCameraToSelectedRegion", "Focus", "Focus camera toward selected body region.");
            DrawProp("cameraFocusLerpSpeed", "Lerp", "Camera focus interpolation speed.");
            DrawProp("enableWorldPartHoverSelection", "Hover", "Allow selecting parts directly from the 3D model.");
            DrawProp("worldPartHoverScaleMultiplier", "Scale", "Scale multiplier applied while hovering a part.");
            DrawProp("worldPartHoverScaleLerpSpeed", "Speed", "Smoothing speed for hover scale transitions.");
            DrawProp("worldPartClickMaxDragPixels", "Drag", "Maximum drag distance still treated as click.");
            DrawProp("ignoreWorldPartHoverWhenPointerOnUI", "Ignore UI", "Disable world hover while pointer is over UI.");
        });

        DrawGroup(ref showView, "View", "Variant preview rendering settings.", () =>
        {
            DrawProp("showVariantPreviewInHud", "HUD", "Show live variant previews in UI buttons.");
            DrawProp("variantPreviewAngles", "Angles", "Default model/camera angles used for previews.");
            DrawProp("variantPreviewBackground", "BG", "Background color used by preview rendering.");
            DrawProp("variantPreviewGlobalMeshSize", "Mesh", "Global mesh size multiplier for preview framing.");
        });

        DrawGroup(ref showColors, "Colors", "Color palettes used by swatches and randomization.", () =>
        {
            DrawPalette("Main", "General-purpose palette.", "paletteColors", ref showMainPalette);
            DrawPalette("Skin", "Skin tone palette.", "skinTonePalette", ref showSkinPalette);
            DrawPalette("Hair", "Hair tone palette.", "hairTonePalette", ref showHairPalette);
            DrawPalette("Clothes", "Garment color palette.", "garmentTonePalette", ref showClothesPalette);
            DrawPalette("Eyes", "Eye/iris palette.", "eyesTonePalette", ref showEyesPalette);
            DrawPalette("Brows", "Eyebrow tone palette.", "eyebrowsTonePalette", ref showBrowsPalette);
            DrawPalette("Makeup", "Makeup accent palette.", "makeupTonePalette", ref showMakeupPalette);
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

    private void DrawPalette(string label, string tooltip, string propertyName, ref bool expanded)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null || !property.isArray)
        {
            return;
        }

        string foldoutLabel = $"{label} ({property.arraySize})";
        expanded = EditorGUILayout.Foldout(expanded, new GUIContent(foldoutLabel, tooltip), true);
        if (!expanded)
        {
            return;
        }

        EditorGUILayout.PropertyField(property, new GUIContent("List", tooltip), true);
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
