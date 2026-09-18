#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

internal static class MinimoInspectorLocalization
{
    private static readonly Dictionary<string, string> LabelOverrides = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["gameplayPreset"] = "Gameplay Preset",
        ["applyPresetOnStart"] = "Apply Preset On Start",
        ["controlProfile"] = "Control Profile",
        ["applyControlProfileOnStart"] = "Apply Control Profile On Start",
        ["dashMotionBlurVolume"] = "Dash Motion Blur Volume",
        ["baseCameraFov"] = "Base Camera FOV",
        ["maxBoostCameraFov"] = "Max Boost Camera FOV",
        ["dashFovFloor01"] = "Dash FOV Floor (0-1)",
        ["dashFovWeight"] = "Dash FOV Weight",
        ["dashFovBoostSharpness"] = "Dash FOV Boost Sharpness",
        ["dashFovRecoverSharpness"] = "Dash FOV Recover Sharpness",
        ["fovBoostSharpness"] = "FOV Boost Sharpness",
        ["fovRecoverSharpness"] = "FOV Recover Sharpness",
        ["airDashFovWeight"] = "Air Dash FOV Weight",
        ["customClimbMask"] = "Custom Climb Mask",
        ["debugState"] = "Runtime State",
        ["animStateParam"] = "Animation State (Int) Parameter",
        ["gamepadMoveXAxis"] = "Gamepad Move X Axis",
        ["gamepadMoveYAxis"] = "Gamepad Move Y Axis",
        ["gamepadLookXAxis"] = "Gamepad Look X Axis",
        ["gamepadLookYAxis"] = "Gamepad Look Y Axis",
        ["gamepadSprintAxis"] = "Gamepad Sprint Axis",
        ["crouchWalkSpeedMultiplier"] = "Crouch Walk Speed Multiplier"
    };

    private static readonly Dictionary<string, string> TokenOverrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["fov"] = "FOV",
        ["hud"] = "HUD",
        ["ui"] = "UI",
        ["fx"] = "FX",
        ["x"] = "X",
        ["y"] = "Y",
        ["z"] = "Z"
    };

    private static readonly Regex TokenRegex = new Regex(@"[A-Z]?[a-z]+|[A-Z]+(?![a-z])|\d+", RegexOptions.Compiled);

    public static GUIContent BuildContent(SerializedProperty property, string fallbackLabel = null, string fallbackTooltip = null)
    {
        string label = string.IsNullOrWhiteSpace(fallbackLabel)
            ? GetEnglishLabel(property.name, property.displayName)
            : fallbackLabel;

        string tooltip = string.IsNullOrWhiteSpace(fallbackTooltip)
            ? BuildDefaultTooltip(property, label)
            : fallbackTooltip;

        return new GUIContent(label, tooltip);
    }

    private static string GetEnglishLabel(string propertyName, string defaultLabel)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return defaultLabel;
        }

        if (LabelOverrides.TryGetValue(propertyName, out string explicitLabel))
        {
            return explicitLabel;
        }

        MatchCollection matches = TokenRegex.Matches(propertyName);
        if (matches.Count == 0)
        {
            return defaultLabel;
        }

        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < matches.Count; i++)
        {
            string token = matches[i].Value;
            if (string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(NormalizeToken(token));
        }

        string result = builder.ToString();
        return string.IsNullOrWhiteSpace(result) ? defaultLabel : result;
    }

    private static string NormalizeToken(string token)
    {
        if (TokenOverrides.TryGetValue(token, out string mapped))
        {
            return mapped;
        }

        if (token.Length <= 1)
        {
            return token.ToUpperInvariant();
        }

        string lower = token.ToLowerInvariant();
        return char.ToUpperInvariant(lower[0]) + lower[1..];
    }

    private static string BuildDefaultTooltip(SerializedProperty property, string label)
    {
        if (property == null)
        {
            return $"Configure {label}.";
        }

        return property.propertyType switch
        {
            SerializedPropertyType.Boolean => $"Enable or disable {label.ToLowerInvariant()}.",
            SerializedPropertyType.ObjectReference => $"Assign a reference for {label.ToLowerInvariant()}.",
            SerializedPropertyType.Enum => $"Select a value for {label.ToLowerInvariant()}.",
            SerializedPropertyType.String => $"Set the text value used by {label.ToLowerInvariant()}.",
            SerializedPropertyType.Color => $"Choose the color used by {label.ToLowerInvariant()}.",
            _ => $"Adjust {label.ToLowerInvariant()}."
        };
    }
}

internal static class MinimoInspectorVisibility
{
    private const BindingFlags FieldFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    public static bool ShouldDrawProperty(SerializedProperty property)
    {
        if (property == null || property.serializedObject == null || property.serializedObject.targetObject == null)
        {
            return false;
        }

        FieldInfo fieldInfo = property.serializedObject.targetObject.GetType().GetField(property.name, FieldFlags);
        if (fieldInfo == null)
        {
            return true;
        }

        ShowIfAttribute[] showIfAttributes = fieldInfo.GetCustomAttributes(typeof(ShowIfAttribute), false) as ShowIfAttribute[];
        if (showIfAttributes != null)
        {
            for (int i = 0; i < showIfAttributes.Length; i++)
            {
                SerializedProperty conditionProperty = property.serializedObject.FindProperty(showIfAttributes[i].ConditionFieldName);
                if (conditionProperty != null
                    && conditionProperty.propertyType == SerializedPropertyType.Boolean
                    && !conditionProperty.boolValue)
                {
                    return false;
                }
            }
        }

        HideIfAttribute[] hideIfAttributes = fieldInfo.GetCustomAttributes(typeof(HideIfAttribute), false) as HideIfAttribute[];
        if (hideIfAttributes != null)
        {
            for (int i = 0; i < hideIfAttributes.Length; i++)
            {
                SerializedProperty conditionProperty = property.serializedObject.FindProperty(hideIfAttributes[i].ConditionFieldName);
                if (conditionProperty != null
                    && conditionProperty.propertyType == SerializedPropertyType.Boolean
                    && conditionProperty.boolValue)
                {
                    return false;
                }
            }
        }

        return true;
    }
}

[CustomEditor(typeof(MinimoCharacterBootstrap))]
[CanEditMultipleObjects]
public class MinimoCharacterBootstrapEditor : Editor
{
    private bool showCore = true;
    private bool showSize = true;
    private bool showVisual = true;
    private bool showCamera = true;
    private bool showPreset = true;

    private GUIStyle cardStyle;
    private GUIStyle titleStyle;
    private GUIStyle subtitleStyle;

    public override void OnInspectorGUI()
    {
        EnsureStyles();
        serializedObject.Update();

        EditorGUILayout.BeginVertical(cardStyle);
        EditorGUILayout.LabelField(
            new GUIContent("Bootstrap", "Scene character setup bridge for controller, visual root, and camera offsets."),
            titleStyle);
        EditorGUILayout.LabelField(
            "Automatic character creation is disabled. Assign an existing scene character for setup.",
            subtitleStyle);
        EditorGUILayout.EndVertical();

        DrawGroup(ref showCore, "Core", "Scene binding and editor automation.", () =>
        {
            DrawProperty("sceneCharacter", "Scene", "Scene character object used for setup.");
            DrawProperty("capsuleCharacterName", "Search", "Fallback scene object name used when Scene is empty.");
            DrawProperty("capsuleVisualScale", "Scale", "Base scale applied during setup.");
            DrawProperty("autoConfigureInEditor", "Auto", "Auto-configure required components in editor.");
            DrawProperty("liveUpdateInEditor", "Live", "Apply setup updates immediately when values change.");
        });

        DrawGroup(ref showSize, "Size", "CharacterController and ground snap settings.", () =>
        {
            DrawProperty("characterControllerHeight", "Height", "CharacterController height.");
            DrawProperty("characterControllerRadius", "Radius", "CharacterController radius.");
            DrawProperty("snapCharacterToGround", "Snap", "Snap character to nearest ground during setup.");
            DrawProperty("groundSnapDistance", "Distance", "Raycast distance used by ground snapping.");
        });

        DrawGroup(ref showVisual, "Visual", "Visual root placement and mesh behavior.", () =>
        {
            DrawProperty("characterVisualPrefab", "Prefab", "Visual prefab attached under character root.");
            DrawProperty("characterVisualRootName", "Root", "Name of visual root child.");
            DrawProperty("characterVisualLocalPosition", "Pos", "Visual root local position.");
            DrawProperty("characterVisualLocalEuler", "Rot", "Visual root local Euler rotation.");
            DrawProperty("characterVisualLocalScale", "Scale", "Visual root local scale.");
            DrawProperty("fitVisualToCharacterController", "Fit", "Fit visual height to CharacterController.");
            DrawProperty("hidePrimitiveRendererWhenVisualPresent", "Hide Mesh", "Hide primitive renderer when visual exists.");
            DrawProperty("ragdoll", "Ragdoll", "Keep child colliders active for ragdoll usage.");
        });

        DrawGroup(ref showCamera, "Camera", "Startup camera offsets.", () =>
        {
            DrawProperty("autoSetupCamera", "Auto", "Apply camera offset and rotation automatically.");
            DrawProperty("cameraStartPosition", "Offset", "Initial camera offset from character.");
            DrawProperty("cameraStartEuler", "Euler", "Initial camera Euler rotation.");
        });

        DrawGroup(ref showPreset, "Preset", "Optional TPS preset assignment.", () =>
        {
            DrawProperty("defaultGameplayPreset", "TPS", "Preset asset used by bootstrap setup.");
        });

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawProperty(string propertyName, string label, string tooltip)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
        {
            return;
        }

        EditorGUILayout.PropertyField(property, MinimoInspectorLocalization.BuildContent(property, label, tooltip), true);
    }

    private void DrawGroup(ref bool expanded, string title, string description, Action drawBody)
    {
        EditorGUILayout.BeginVertical(cardStyle);
        expanded = EditorGUILayout.Foldout(expanded, new GUIContent(title, description), true);
        if (expanded)
        {
            if (!string.IsNullOrWhiteSpace(description))
            {
                EditorGUILayout.LabelField(description, subtitleStyle);
                EditorGUILayout.Space(2f);
            }

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

[CustomEditor(typeof(MinimoTpsController))]
[CanEditMultipleObjects]
public class MinimoTpsControllerEditor : Editor
{
    private const string FoldoutSessionPrefix = "Minimo.TpsControllerEditor.";

    private sealed class FieldDefinition
    {
        public FieldDefinition(string propertyName, bool readOnly = false, string labelOverride = null, string tooltipOverride = null)
        {
            PropertyName = propertyName;
            ReadOnly = readOnly;
            LabelOverride = labelOverride;
            TooltipOverride = tooltipOverride;
        }

        public string PropertyName { get; }
        public bool ReadOnly { get; }
        public string LabelOverride { get; }
        public string TooltipOverride { get; }
    }

    private sealed class GroupDefinition
    {
        public GroupDefinition(string title, string description, FieldDefinition[] fields, string toggleProperty = null, bool hideBodyWhenDisabled = false, string visibleWhenProperty = null)
        {
            Title = title;
            Description = description;
            Fields = fields ?? Array.Empty<FieldDefinition>();
            ToggleProperty = toggleProperty;
            HideBodyWhenDisabled = hideBodyWhenDisabled;
            VisibleWhenProperty = visibleWhenProperty;
        }

        public string Title { get; }
        public string Description { get; }
        public FieldDefinition[] Fields { get; }
        public string ToggleProperty { get; }
        public bool HideBodyWhenDisabled { get; }
        public string VisibleWhenProperty { get; }
    }

    private sealed class SectionDefinition
    {
        public SectionDefinition(string id, string title, string description, bool defaultExpanded, GroupDefinition[] groups)
        {
            Id = id;
            Title = title;
            Description = description;
            DefaultExpanded = defaultExpanded;
            Groups = groups ?? Array.Empty<GroupDefinition>();
        }

        public string Id { get; }
        public string Title { get; }
        public string Description { get; }
        public bool DefaultExpanded { get; }
        public GroupDefinition[] Groups { get; }
    }

    private sealed class TabDefinition
    {
        public TabDefinition(string id, string label, string description, SectionDefinition[] sections)
        {
            Id = id;
            Label = label;
            Description = description;
            Sections = sections ?? Array.Empty<SectionDefinition>();
        }

        public string Id { get; }
        public string Label { get; }
        public string Description { get; }
        public SectionDefinition[] Sections { get; }
    }

    private int activeTab;

    private static readonly TabDefinition[] InspectorTabs = BuildTabs();
    private static readonly string[] TabLabels = Array.ConvertAll(InspectorTabs, tab => tab.Label);

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawOverview();
        EditorGUILayout.Space(8f);

        activeTab = DrawTabGrid(activeTab);
        TabDefinition currentTab = InspectorTabs[Mathf.Clamp(activeTab, 0, InspectorTabs.Length - 1)];

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField(currentTab.Label, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(currentTab.Description, EditorStyles.wordWrappedMiniLabel);
        }

        DrawSectionToolbar(currentTab);
        DrawConfiguredTab(currentTab);

        serializedObject.ApplyModifiedProperties();
    }

    private int DrawTabGrid(int currentIndex)
    {
        int safeIndex = Mathf.Clamp(currentIndex, 0, InspectorTabs.Length - 1);
        const int tabsPerRow = 3;
        int totalTabs = InspectorTabs.Length;
        int rowCount = Mathf.CeilToInt(totalTabs / (float)tabsPerRow);
        Color defaultColor = GUI.backgroundColor;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Inspector Navigation", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField(
                "Core tabs are arranged in compact rows. Detailed tuning values remain in compact cards and optional advanced foldouts.",
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(4f);

            for (int row = 0; row < rowCount; row++)
            {
                int rowStart = row * tabsPerRow;
                int rowEnd = Mathf.Min(rowStart + tabsPerRow, totalTabs);
                int rowTabCount = rowEnd - rowStart;

                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int col = 0; col < rowTabCount; col++)
                    {
                        int tabIndex = rowStart + col;

                        bool selected = tabIndex == safeIndex;
                        GUI.backgroundColor = selected ? new Color(0.37f, 0.62f, 0.95f, 1f) : defaultColor;
                        GUIStyle buttonStyle;
                        if (rowTabCount <= 1)
                        {
                            buttonStyle = EditorStyles.miniButton;
                        }
                        else if (col == 0)
                        {
                            buttonStyle = EditorStyles.miniButtonLeft;
                        }
                        else if (col == rowTabCount - 1)
                        {
                            buttonStyle = EditorStyles.miniButtonRight;
                        }
                        else
                        {
                            buttonStyle = EditorStyles.miniButtonMid;
                        }

                        if (GUILayout.Button(TabLabels[tabIndex], buttonStyle, GUILayout.Height(24f), GUILayout.ExpandWidth(true)))
                        {
                            safeIndex = tabIndex;
                        }
                    }
                }

                EditorGUILayout.Space(2f);
            }
        }

        GUI.backgroundColor = defaultColor;
        return safeIndex;
    }

    private void DrawOverview()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Minimo TPS Controller", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Streamlined layout with Setup, Actions, Camera, Feel, Parameters, and Particles tabs. Core controls stay visible while detailed tuning remains compact.",
                EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.Space(6f);

            if (serializedObject.isEditingMultipleObjects)
            {
                EditorGUILayout.LabelField("Multi-object editing is active. Shared values are being edited together.", EditorStyles.miniLabel);
                return;
            }

            DrawSummaryLine("Preset", GetObjectReferenceName("gameplayPreset", "None"));
            DrawSummaryLine("Profile", GetEnumDisplayName("controlProfile", "-"));
            DrawSummaryLine("Animator", GetToggleStateLabel("useAnimatorParameters"));
            DrawSummaryLine("Feedback", GetToggleStateLabel("enableFeedback"));
            DrawSummaryLine("Gamepad", GetToggleStateLabel("enableGamepadInput"));
        }
    }

    private void DrawSummaryLine(string label, string value)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(label, GUILayout.Width(82f));
            EditorGUILayout.SelectableLabel(value, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
        }
    }

    private string GetObjectReferenceName(string propertyName, string fallback)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null || property.propertyType != SerializedPropertyType.ObjectReference)
        {
            return fallback;
        }

        return property.objectReferenceValue != null ? property.objectReferenceValue.name : fallback;
    }

    private string GetEnumDisplayName(string propertyName, string fallback)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null || property.propertyType != SerializedPropertyType.Enum || property.enumDisplayNames == null || property.enumDisplayNames.Length == 0)
        {
            return fallback;
        }

        int index = Mathf.Clamp(property.enumValueIndex, 0, property.enumDisplayNames.Length - 1);
        return property.enumDisplayNames[index];
    }

    private string GetToggleStateLabel(string propertyName)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null || property.propertyType != SerializedPropertyType.Boolean)
        {
            return "-";
        }

        if (property.hasMultipleDifferentValues)
        {
            return "Mixed";
        }

        return property.boolValue ? "On" : "Off";
    }

    private void DrawSectionToolbar(TabDefinition currentTab)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Expand All Sections"))
            {
                SetAllSectionsExpanded(currentTab, true);
            }

            if (GUILayout.Button("Collapse All Sections"))
            {
                SetAllSectionsExpanded(currentTab, false);
            }
        }

        EditorGUILayout.Space(3f);
    }

    private void SetAllSectionsExpanded(TabDefinition tab, bool expanded)
    {
        for (int i = 0; i < tab.Sections.Length; i++)
        {
            SessionState.SetBool(GetSectionStateKey(tab.Id, tab.Sections[i].Id), expanded);
        }
    }

    private void DrawConfiguredTab(TabDefinition tab)
    {
        for (int i = 0; i < tab.Sections.Length; i++)
        {
            DrawConfiguredSection(tab, tab.Sections[i]);
        }
    }

    private void DrawConfiguredSection(TabDefinition tab, SectionDefinition section)
    {
        if (!ShouldDrawSection(section))
        {
            return;
        }

        bool expanded = SessionState.GetBool(GetSectionStateKey(tab.Id, section.Id), section.DefaultExpanded);
        expanded = EditorGUILayout.Foldout(expanded, new GUIContent(section.Title, section.Description), true);
        SessionState.SetBool(GetSectionStateKey(tab.Id, section.Id), expanded);

        if (expanded)
        {
            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField(section.Description, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(4f);

            for (int i = 0; i < section.Groups.Length; i++)
            {
                DrawConfiguredGroup(tab, section, i, section.Groups[i]);
            }
        }

        EditorGUILayout.Space(4f);
    }

    private void DrawConfiguredGroup(TabDefinition tab, SectionDefinition section, int groupIndex, GroupDefinition group)
    {
        if (!ShouldDrawGroup(group))
        {
            return;
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            string stateKey = GetGroupStateKey(tab.Id, section.Id, groupIndex);
            bool expanded = SessionState.GetBool(stateKey, true);
            string headerTitle = BuildGroupHeaderTitle(group);
            expanded = EditorGUILayout.Foldout(
                expanded,
                new GUIContent(headerTitle, group.Description),
                true,
                EditorStyles.foldout);
            SessionState.SetBool(stateKey, expanded);

            if (expanded)
            {
                if (!string.IsNullOrWhiteSpace(group.Description))
                {
                    EditorGUILayout.LabelField(group.Description, EditorStyles.wordWrappedMiniLabel);
                    EditorGUILayout.Space(2f);
                }

                SerializedProperty toggleProperty = string.IsNullOrWhiteSpace(group.ToggleProperty)
                    ? null
                    : serializedObject.FindProperty(group.ToggleProperty);

                if (toggleProperty != null)
                {
                    EditorGUILayout.PropertyField(toggleProperty, MinimoInspectorLocalization.BuildContent(toggleProperty), true);

                    bool enabled = toggleProperty.hasMultipleDifferentValues || toggleProperty.boolValue;
                    if (!enabled && group.HideBodyWhenDisabled)
                    {
                        EditorGUILayout.HelpBox("This feature is currently disabled. Enable it to edit these settings.", MessageType.None);
                    }
                    else
                    {
                        using (new EditorGUI.DisabledScope(!enabled))
                        {
                            DrawConfiguredFields(group.Fields);
                        }
                    }
                }
                else
                {
                    DrawConfiguredFields(group.Fields);
                }
            }
        }

        EditorGUILayout.Space(4f);
    }

    private void DrawConfiguredFields(IReadOnlyList<FieldDefinition> fields)
    {
        EditorGUI.indentLevel++;
        for (int i = 0; i < fields.Count; i++)
        {
            DrawConfiguredField(fields[i]);
        }

        EditorGUI.indentLevel--;
    }

    private void DrawConfiguredField(FieldDefinition field)
    {
        SerializedProperty property = serializedObject.FindProperty(field.PropertyName);
        if (property == null || !MinimoInspectorVisibility.ShouldDrawProperty(property))
        {
            return;
        }

        using (new EditorGUI.DisabledScope(field.ReadOnly))
        {
            EditorGUILayout.PropertyField(
                property,
                MinimoInspectorLocalization.BuildContent(property, field.LabelOverride, field.TooltipOverride),
                true);
        }
    }

    private bool ShouldDrawSection(SectionDefinition section)
    {
        for (int i = 0; i < section.Groups.Length; i++)
        {
            if (ShouldDrawGroup(section.Groups[i]))
            {
                return true;
            }
        }

        return false;
    }

    private bool ShouldDrawGroup(GroupDefinition group)
    {
        if (!EvaluateGroupVisibility(group.VisibleWhenProperty))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(group.ToggleProperty))
        {
            return serializedObject.FindProperty(group.ToggleProperty) != null;
        }

        for (int i = 0; i < group.Fields.Length; i++)
        {
            SerializedProperty property = serializedObject.FindProperty(group.Fields[i].PropertyName);
            if (property != null && MinimoInspectorVisibility.ShouldDrawProperty(property))
            {
                return true;
            }
        }

        return false;
    }

    private bool EvaluateGroupVisibility(string visibleWhenProperty)
    {
        if (string.IsNullOrWhiteSpace(visibleWhenProperty))
        {
            return true;
        }

        SerializedProperty condition = serializedObject.FindProperty(visibleWhenProperty);
        if (condition == null || condition.propertyType != SerializedPropertyType.Boolean)
        {
            return true;
        }

        return condition.hasMultipleDifferentValues || condition.boolValue;
    }

    private string GetSectionStateKey(string tabId, string sectionId)
    {
        return $"{FoldoutSessionPrefix}{target.GetType().Name}.{tabId}.{sectionId}";
    }

    private string GetGroupStateKey(string tabId, string sectionId, int groupIndex)
    {
        return $"{FoldoutSessionPrefix}{target.GetType().Name}.{tabId}.{sectionId}.group{groupIndex}";
    }

    private string BuildGroupHeaderTitle(GroupDefinition group)
    {
        if (string.IsNullOrWhiteSpace(group.ToggleProperty))
        {
            return group.Title;
        }

        return $"{group.Title} [{GetToggleStateLabel(group.ToggleProperty)}]";
    }

    private static TabDefinition[] BuildTabs()
    {
        return new[]
        {
            CreateSetupTab(),
            CreateActionsTab(),
            CreateCameraTab(),
            CreateFeelTab(),
            CreateAnimationDebugTab(),
            CreateParticlesTab()
        };
    }

    private static TabDefinition CreateSetupTab()
    {
        return new TabDefinition(
            "setup",
            "Setup",
            "General setup, control profile, gamepad input, HUD overlays, and cursor behavior.",
            new[]
            {
                new SectionDefinition(
                    "configuration",
                    "Core Configuration",
                    "Configure startup behavior and default gameplay profile choices.",
                    true,
                    new[]
                    {
                        new GroupDefinition(
                            "Gameplay Preset",
                            "Apply a single preset asset to quickly configure the controller.",
                            Fields("gameplayPreset", "applyPresetOnStart")),
                        new GroupDefinition(
                            "Control Profile",
                            "Choose an input profile and apply it automatically at startup.",
                            Fields("controlProfile", "applyControlProfileOnStart"))
                    }),
                new SectionDefinition(
                    "input",
                    "Input And HUD",
                    "Manage gamepad bindings, overlay panels, and cursor locking in one place.",
                    true,
                    new[]
                    {
                        new GroupDefinition(
                            "Gamepad Action Buttons",
                            "Map gamepad buttons used for jump, dash, crouch, and other actions.",
                            Fields(
                                "gamepadJumpButton",
                                "gamepadSprintButton",
                                "gamepadDashButton",
                                "gamepadGroundPoundButton",
                                "gamepadDiveButton",
                                "gamepadRollButton",
                                "gamepadCrouchButton",
                                "gamepadKillButton"),
                            "enableGamepadInput",
                            true),
                        new GroupDefinition(
                            "Gamepad Sticks And Trigger",
                            "Configure movement/look axes, dead zones, and analog trigger sprint input.",
                            Fields(
                                "gamepadMoveXAxis",
                                "gamepadMoveYAxis",
                                "gamepadMoveDeadZone",
                                "gamepadLookXAxis",
                                "gamepadLookYAxis",
                                "gamepadLookSensitivity",
                                "gamepadLookDeadZone",
                                "useGamepadLeftTriggerForSprint",
                                "gamepadSprintAxis",
                                "gamepadSprintDeadZone"),
                            "enableGamepadInput",
                            true),
                        new GroupDefinition(
                            "Overlay Panels",
                            "Control the in-game controls panel and movement state HUD overlays.",
                            Fields(
                                "showControlsOverlay",
                                "controlsOverlayToggleKey",
                                "showStateHud",
                                "stateHudToggleKey",
                                "showExtendedStateHud")),
                        new GroupDefinition(
                            "Cursor",
                            "Configure cursor lock mode and temporary unlock behavior.",
                            Fields("hideAndLockCursor", "unlockCursorWithEscape"))
                    })
            });
    }

    private static TabDefinition CreateMovementTab()
    {
        return new TabDefinition(
            "movement",
            "Movement",
            "Tune locomotion, jumping, ground interaction, and edge behavior.",
            new[]
            {
                new SectionDefinition(
                    "locomotion",
                    "Locomotion",
                    "Core movement behavior while grounded and airborne.",
                    true,
                    new[]
                    {
                        new GroupDefinition(
                            "Base Movement",
                            "Top speed, acceleration, turning response, and input shaping.",
                            Fields(
                                "moveSpeed",
                                "groundAcceleration",
                                "turnAcceleration",
                                "groundDeceleration",
                                "airAcceleration",
                                "airControl",
                                "reverseDirectionAccelerationMultiplier",
                                "rotationSharpness",
                                "airRotationMultiplier",
                                "snappyReverseTurnMultiplier",
                                "reversePivotThreshold",
                                "pivotBrakeDeceleration",
                                "movementInputDeadZone",
                                "facingSpeedThreshold",
                                "crouchWalkSpeedMultiplier")),
                        new GroupDefinition(
                            "Sprint",
                            "Enable sprint and tune its speed multiplier.",
                            Fields("sprintKey", "sprintSpeedMultiplier"),
                            "enableSprint",
                            true),
                        new GroupDefinition(
                            "Run Stop Slide",
                            "Short momentum slide when releasing movement at high speed.",
                            Fields("runStopSlideMinSpeed", "runStopSlideDuration", "runStopSlideDecelerationMultiplier"),
                            "enableRunStopSlide",
                            true)
                    }),
                new SectionDefinition(
                    "jumping",
                    "Jump And Air",
                    "Jump rise/fall tuning, coyote time, wall interactions, and ledge movement.",
                    true,
                    new[]
                    {
                        new GroupDefinition(
                            "Jump Core",
                            "Configure jump height, hold behavior, and minimum jump cut behavior.",
                            Fields(
                                "jumpHeight",
                                "gravity",
                                "jumpHoldTime",
                                "jumpHoldForce",
                                "minimumJumpHeight",
                                "jumpCutMultiplier",
                                "jumpCeilingCheckDistance",
                                "jumpHeadroomMask",
                                "groundedVerticalForce")),
                        new GroupDefinition(
                            "Fall Tuning",
                            "Configure gravity multipliers, apex feel, and input forgiveness windows.",
                            Fields(
                                "fallGravityMultiplier",
                                "fastFallGravityMultiplier",
                                "fallAnimationVerticalSpeedThreshold",
                                "highFallMinHeightForFallAnim",
                                "highFallDoubleJumpHeightTolerance",
                                "highFallMinAirTimeForRoll",
                                "apexVerticalSpeedThreshold",
                                "apexGravityMultiplier",
                                "terminalVelocity",
                                "coyoteTime",
                                "jumpBufferTime",
                                "jumpLandingCooldownDuration")),
                        new GroupDefinition(
                            "Air Jump",
                            "Enable extra jumps in air and tune the air jump height scaling.",
                            Fields("maxAirJumps", "airJumpHeightMultiplier"),
                            "enableDoubleJump",
                            true),
                        new GroupDefinition(
                            "Wall Jump",
                            "Configure wall jump detection and launch behavior.",
                            Fields("wallJumpProbeDistance", "wallJumpBackwardSpeed", "wallJumpMaxSurfaceUpDot", "wallJumpCameraTurnSpeed"),
                            "enableWallJump",
                            true),
                        new GroupDefinition(
                            "Wall Hang And Ledge",
                            "Configure wall hang slide, ledge detection, shimmy, and climb alignment.",
                            Fields(
                                "wallHangContactDistance",
                                "wallHangFacingAngleTolerance",
                                "wallHangReleaseInputAngle",
                                "wallHangSlideSpeed",
                                "wallHangSlideEaseInDuration",
                                "wallHangSlideEaseInStartAccelerationScale",
                                "wallHangHorizontalDamping",
                                "wallHangStickSpeed",
                                "wallHangReleaseGroundDistance",
                                "enableLedgeHang",
                                "customClimbMask",
                                "leftHandLedgeTransform",
                                "rightHandLedgeTransform",
                                "leftHandLedgeProbeRadius",
                                "rightHandLedgeProbeRadius",
                                "ledgeHangSnapHandsMidpointToEdge",
                                "ledgeHandContactPadding",
                                "ledgeGrabAssistDistance",
                                "ledgeTopProbeUpDistance",
                                "ledgeTopProbeForwardInset",
                                "ledgeTopProbeDownDistance",
                                "ledgeTopSurfaceMinUpDot",
                                "ledgeTopMaxHorizontalGap",
                                "ledgeHangMinEdgeHeight",
                                "ledgeHangMaxEdgeHeight",
                                "ledgeHangClimbForwardInputThreshold",
                                "ledgeHangLateralMoveSpeed",
                                "ledgeHangLateralInputThreshold",
                                "ledgeHangCornerStopDistance",
                                "ledgeHangSideAnimationThreshold",
                                "ledgeHangHoldBeforeClimbDuration",
                                "ledgeHangEntryInputLockDuration",
                                "ledgeHangReleaseBackInputThreshold",
                                "ledgeHangClimbDuration",
                                "ledgeHangClimbUpDistance",
                                "ledgeHangClimbForwardDistance",
                                "ledgeClimbStepForwardBonus",
                                "ledgeHangAnchorOutwardOffset",
                                "ledgeHangAnchorVerticalOffset",
                                "ledgeHangHandMidpointVerticalBias",
                                "ledgeHangVerticalCalibrationOffset",
                                "ledgeHangUseAnchorOffsets",
                                "ledgeHangWallProximityOffset",
                                "ledgeHangHandSurfaceTargetOffset",
                                "ledgeHangDualHandSurfaceTolerance",
                                "ledgeHangDualHandMaxExtraPush",
                                "ledgeHangAnchorAlignSpeed",
                                "ledgeHangAnchorAlignTolerance",
                                "ledgeHangContactLostGraceTime",
                                "wallPostClimbDetachLockTime",
                                "ledgeHangRegrabLockTime",
                                "ledgeHangCornerTransitionAssistDistance",
                                "ledgeHangCornerProbeLateralOffset"),
                            "enableWallHang",
                            true)
                    }),
                new SectionDefinition(
                    "ground",
                    "Ground Interaction",
                    "Ground detection, slope behavior, moving platforms, and edge balance.",
                    true,
                    new[]
                    {
                        new GroupDefinition(
                            "Ground Probe",
                            "Ground contact probing, mask setup, and upward probe ignore threshold.",
                            Fields(
                                "groundMask",
                                "groundProbeOffset",
                                "groundProbeDistance",
                                "groundProbeRadius",
                                "ignoreGroundProbeUpwardSpeed")),
                        new GroupDefinition(
                            "Moving Platform",
                            "Follow moving platform translation and rotation while staying stable.",
                            Fields(
                                "enableMovingPlatformCompensation",
                                "movingPlatformRotationInfluence",
                                "movingPlatformMaxDeltaPerFrame",
                                "disablePlatformCompensationWhenParented",
                                "suppressGravityWhenParentedToPlatform")),
                        new GroupDefinition(
                            "Slope Assist",
                            "Configure uphill/downhill speed effects and anti-slide stabilization.",
                            Fields(
                                "slopeSpeedInfluence",
                                "uphillSpeedPenalty",
                                "downhillSpeedBoost",
                                "slopeAntiSlideDeceleration",
                                "slopeAlignmentSharpness")),
                        new GroupDefinition(
                            "Step Up",
                            "Detect and step over small obstacles smoothly.",
                            Fields("maxStepHeight", "stepCheckDistance", "stepForwardOffset", "stepSurfaceProbeHeight"),
                            "enableStepUp",
                            true),
                        new GroupDefinition(
                            "Teetering",
                            "Edge balancing behavior, safe pullback, and fall/recover thresholds.",
                            Fields(
                                "teeterMaxEntrySpeed",
                                "teeterProbeForwardDistance",
                                "teeterEdgeEarlyDetectDistance",
                                "teeterProbeDepth",
                                "teeterSnapBackDistance",
                                "teeterHoldBackSpeed",
                                "teeterFallInputThreshold",
                                "teeterRecoverInputThreshold",
                                "teeterLateralExitInputThreshold",
                                "teeterLockForwardWalk",
                                "teeterFacingEdgeAlignmentThreshold",
                                "teeterOpenSpaceRadius",
                                "teeterOpenSpaceForwardOffset",
                                "teeterOpenSpaceVerticalOffset",
                                "teeterOpenSpaceUpperVerticalOffset",
                                "teeterReentryLockDuration",
                                "teeterAudioCooldown",
                                "teeterNoInputEntrySpeedMultiplier"),
                            "enableTeetering",
                            true)
                    })
            });
    }

    private static TabDefinition CreateActionsTab()
    {
        return new TabDefinition(
            "actions",
            "Actions",
            "Merged movement and action controls for quick gameplay setup.",
            new[]
            {
                new SectionDefinition(
                    "movement_core",
                    "Movement Core",
                    "Essential locomotion and jump values. Deep tuning values are intentionally hidden from this compact tab.",
                    true,
                    new[]
                    {
                        new GroupDefinition(
                            "Base Movement",
                            "Core speed and acceleration used most frequently during feel iteration.",
                            Fields(
                                "moveSpeed",
                                "groundAcceleration",
                                "groundDeceleration",
                                "airAcceleration",
                                "airControl",
                                "rotationSharpness",
                                "sprintSpeedMultiplier",
                                "crouchWalkSpeedMultiplier")),
                        new GroupDefinition(
                            "Jump And Air",
                            "Primary jump, gravity, and air-jump values.",
                            Fields(
                                "jumpHeight",
                                "gravity",
                                "fallGravityMultiplier",
                                "fastFallGravityMultiplier",
                                "coyoteTime",
                                "jumpBufferTime",
                                "enableDoubleJump",
                                "maxAirJumps",
                                "airJumpHeightMultiplier",
                                "enableWallHang",
                                "enableLedgeHang",
                                "customClimbMask",
                                "leftHandLedgeTransform",
                                "rightHandLedgeTransform",
                                "leftHandLedgeProbeRadius",
                                "rightHandLedgeProbeRadius",
                                "ledgeHangSnapHandsMidpointToEdge",
                                "ledgeHangVerticalCalibrationOffset")),
                        new GroupDefinition(
                            "Ground And Slope",
                            "Basic ground probe, slope feel, and step-up controls.",
                            Fields(
                                "groundMask",
                                "groundProbeDistance",
                                "slopeSpeedInfluence",
                                "uphillSpeedPenalty",
                                "downhillSpeedBoost",
                                "enableStepUp",
                                "maxStepHeight",
                                "stepCheckDistance"))
                    }),
                new SectionDefinition(
                    "action_cards",
                    "Action Cards",
                    "Primary action toggles and headline values. Detailed micro-tuning remains hidden for a cleaner inspector.",
                    true,
                    new[]
                    {
                        new GroupDefinition(
                            "Dash",
                            "Quick burst movement.",
                            Fields("dashKey", "dashSpeed", "dashDuration", "dashCooldown"),
                            "enableDash",
                            true),
                        new GroupDefinition(
                            "Ground Pound",
                            "Downward slam with impact force.",
                            Fields("groundPoundKey", "groundPoundSpeed", "groundPoundAcceleration"),
                            "enableGroundPound",
                            true),
                        new GroupDefinition(
                            "Crouch And Slide",
                            "Crouch mode and slide behavior.",
                            Fields("crouchKey", "crouchToggleMode", "crouchHeight", "slideInitialBoost", "slideDuration", "slideDeceleration"),
                            "enableCrouchSlide",
                            true),
                        new GroupDefinition(
                            "Dive And Roll",
                            "Aerial dive plus landing roll continuation.",
                            Fields("diveKey", "diveForwardSpeed", "diveDownwardSpeed", "diveDuration", "rollKey", "rollDuration"),
                            "enableDiveRoll",
                            true),
                        new GroupDefinition(
                            "Slope Slide",
                            "Downhill momentum slide setup.",
                            Fields("slopeSlideMinAngle", "slopeSlideDownhillAcceleration", "slopeSlideMaxSpeed"),
                            "enableSlopeSlide",
                            true),
                        new GroupDefinition(
                            "Traversal Toggles",
                            "Enable advanced traversal systems without exposing full detailed tuning here.",
                            Fields("enableWallJump", "enableWallHang", "enableLedgeHang", "enableTeetering"))
                    })
            });
    }

    private static TabDefinition CreateCameraTab()
    {
        return new TabDefinition(
            "camera",
            "Camera",
            "Camera rig, look response, collision, and FOV behavior.",
            new[]
            {
                new SectionDefinition(
                    "camera_core",
                    "Camera Rig",
                    "Main camera setup used during normal gameplay.",
                    true,
                    new[]
                    {
                        new GroupDefinition(
                            "Rig",
                            "Follow distance, zoom range, shoulder offset, and pitch limits.",
                            Fields("followDistance", "minZoomDistance", "maxZoomDistance", "zoomLevelCount", "zoomSharpness", "shoulderOffset", "pivotHeight", "cameraPositionSmooth", "minPitch", "maxPitch")),
                        new GroupDefinition(
                            "Look",
                            "Mouse and gamepad look sensitivity.",
                            Fields("mouseXSensitivity", "mouseYSensitivity", "invertY", "gamepadLookSensitivity", "gamepadLookDeadZone")),
                        new GroupDefinition(
                            "Camera Collision",
                            "Prevent camera clipping by tuning probe radius and collision offsets.",
                            Fields("collisionProbeRadius", "collisionBuffer", "cameraCollisionMinDistance", "cameraCollisionMask")),
                        new GroupDefinition(
                            "Camera Assist",
                            "Auto-yaw and look-ahead support that follows movement direction.",
                            Fields("cameraAssistYawWeight", "cameraAssistYawSharpness", "cameraLookAheadDistance", "cameraLookAheadSharpness"),
                            "enableCameraAssist",
                            true),
                        new GroupDefinition(
                            "Speed FOV",
                            "Speed-based field of view expansion and blend behavior.",
                            Fields("baseCameraFov", "maxBoostCameraFov", "fovBoostSharpness", "fovRecoverSharpness", "speedFovWeight", "airDashFovWeight", "slopeSlideFovWeight", "fovSpeedReference", "enableWindOverlay"),
                            "enableSpeedFov",
                            true),
                        new GroupDefinition(
                            "Camera Motion FX",
                            "Bank, sway, and shake response from movement and landings.",
                            Fields("maxCameraBankAngle", "cameraBankSharpness", "cameraShakeMaxAmplitude", "cameraShakeFrequency", "cameraShakeDamping", "landingCameraShakeMultiplier"),
                            "enableCameraMotionFx",
                            true),
                        new GroupDefinition(
                            "Roll Stabilization",
                            "Action-camera style stabilization during roll without losing character follow.",
                            Fields(
                                "rollCameraPivotStabilizationDeadZone",
                                "rollCameraPivotStabilizationSharpness",
                                "rollCameraPivotStabilizationMaxLag",
                                "rollCameraPivotReleaseDuration",
                                "rollCameraPivotReleaseSharpness"))
                    })
            });
    }

    private static TabDefinition CreateFeelTab()
    {
        return new TabDefinition(
            "feel",
            "Feel",
            "Character visual response, squash/stretch, and quick feedback shaping.",
            new[]
            {
                new SectionDefinition(
                    "visual_feel",
                    "Visual Feel",
                    "Stylized body response controls kept in a concise format.",
                    true,
                    new[]
                    {
                        new GroupDefinition(
                            "Character Feel",
                            "Enable character feel and tune the primary visual response values.",
                            Fields(
                                "visualRoot",
                                "visualResponse",
                                "maxSideLean",
                                "maxForwardLean",
                                "moveStretch",
                                "accelerationStretch",
                                "brakeSquash",
                                "jumpStretch",
                                "fallShapeAmount",
                                "landSquash",
                                "groundedVisualSink",
                                "crouchVisualSinkBonus"),
                            "enableCharacterFeel",
                            true),
                        new GroupDefinition(
                            "Jump Blob",
                            "Jump pulse and vertical blob response.",
                            Fields(
                                "jumpStretchDecaySpeed",
                                "jumpBlobHorizontalCompression",
                                "jumpBlobVerticalStretch",
                                "jumpBlobVerticalVelocityInfluence"),
                            "enableJumpBlobEffect",
                            true,
                            "enableCharacterFeel"),
                        new GroupDefinition(
                            "Dash Blob",
                            "Dash pulse compression/stretch values.",
                            Fields(
                                "dashBlobPulse",
                                "dashBlobHorizontalCompression",
                                "dashBlobVerticalCompression",
                                "dashBlobForwardStretch",
                                "dashBlobDecaySpeed"),
                            "enableDashBlobEffect",
                            true,
                            "enableCharacterFeel")
                    }),
                new SectionDefinition(
                    "feedback_quick",
                    "Feedback Quick",
                    "Commonly adjusted sound and camera kick values.",
                    false,
                    new[]
                    {
                        new GroupDefinition(
                            "Audio And Camera Kick",
                            "Quick-access feedback controls. Full clip routing stays under Parameters tab.",
                            Fields(
                                "enableFeedback",
                                "feedbackVolume",
                                "enableFootsteps",
                                "footstepBaseInterval",
                                "jumpCameraKick",
                                "landCameraKick",
                                "dashCameraKick",
                                "cameraKickRecoverSpeed"))
                    })
            });
    }

    private static TabDefinition CreateAnimationDebugTab()
    {
        return new TabDefinition(
            "animation",
            "Parameters",
            "Animation parameters, audio feedback, and debug display settings.",
            new[]
            {
                new SectionDefinition(
                    "animator",
                    "Animator",
                    "Animator driver values and animation parameter names.",
                    true,
                    new[]
                    {
                        new GroupDefinition(
                            "Animator Driver",
                            "Animator assignment plus runtime playback and speed blending controls.",
                            Fields(
                                "animatorOverride",
                                "enableJumpMirrorAlternation",
                                "enableDashMirrorAlternation",
                                "animSpeedSmoothness",
                                "stopAnimationHoldTime",
                                "stopArmAnimatorSpeedThreshold",
                                "stopTriggerAnimatorSpeedThreshold",
                                "stopSprintPrimeMinPlanarSpeed",
                                "stopSprintReleaseTriggerSpeed01",
                                "holdStopBoolUntilAnimationEnds",
                                "stopAnimationStateTag",
                                "stopAnimationEndNormalizedTime",
                                "stopAnimationLayerIndex",
                                "animDashSpeedValue",
                                "animatorNormalPlaybackSpeed",
                                "animatorDashPlaybackSpeed",
                                "animatorPlaybackSpeedLinearRise",
                                "animatorPlaybackSpeedLinearFall",
                                "animSpeedLinearRise",
                                "animSpeedLinearFall",
                                "animSpeedLinearRateScale",
                                "animMoveSpeedLinearRise",
                                "animMoveSpeedLinearFall",
                                "animVerticalSpeedLinearRate",
                                "planarStopSnapSpeed",
                                "animatorSpeedZeroEpsilon"),
                            "useAnimatorParameters",
                            true),
                        new GroupDefinition(
                            "Animation Parameter Names",
                            "Keep all animator parameter and trigger names in one place.",
                            Fields(
                                "animSpeedParam",
                                "animMoveSpeedParam",
                                "animVerticalSpeedParam",
                                "animGroundedParam",
                                "animStateParam",
                                "animFallParam",
                                "animLandParam",
                                "animStopParam",
                                "animHangParam",
                                "animHangLeftParam",
                                "animHangRightParam",
                                "animClimbParam",
                                "animClimbJumpParam",
                                "animCrouchParam",
                                "animSlideParam",
                                "animDashParam",
                                "animGroundPoundParam",
                                "animHardFallParam",
                                "animHardLandParam",
                                "animDiveParam",
                                "animRollParam",
                                "animTeeterParam",
                                "animJumpTrigger",
                                "animFrontflipTrigger",
                                "animBackflipTrigger",
                                "animJumpMirrorParam",
                                "animDashMirrorParam"),
                            null,
                            false,
                            "useAnimatorParameters")
                    }),
                new SectionDefinition(
                    "feedback",
                    "Feedback",
                    "Audio clips and camera kick behavior.",
                    true,
                    new[]
                    {
                        new GroupDefinition(
                            "Feedback Core",
                            "Main feedback source, audio clip references, and camera kick values.",
                            Fields("audioSourceOverride", "feedbackVolume", "jumpClip", "landClip", "dashClip", "groundPoundStartClip", "groundPoundLandClip", "slideClip", "teeterClip", "jumpCameraKick", "landCameraKick", "dashCameraKick", "cameraKickRecoverSpeed"),
                            "enableFeedback",
                            true),
                        new GroupDefinition(
                            "Footsteps",
                            "Footstep clip and interval settings while moving on ground.",
                            Fields("footstepClip", "footstepMinSpeed", "footstepBaseInterval"),
                            "enableFootsteps",
                            true,
                            "enableFeedback")
                    }),
                new SectionDefinition(
                    "debug",
                    "Debug",
                    "Debug overlay, gizmo rendering, labels, and runtime state.",
                    false,
                    new[]
                    {
                        new GroupDefinition(
                            "Debug Overlay",
                            "On-screen debug panel visibility and toggle key.",
                            Fields("showDebugOverlay", "debugToggleKey")),
                        new GroupDefinition(
                            "Debug Gizmos",
                            "Scene gizmos for probes, actions, and predicted trajectories.",
                            Fields("debugGizmoToggleKey", "drawGroundProbeGizmo", "drawSlopeGizmo", "drawStepGizmo", "drawTeeterGizmo", "drawTeeterEventGizmo", "drawDashGizmo", "drawDiveGizmo", "drawSlideGizmo", "drawActionDirectionGizmo", "drawJumpHeadroomGizmo", "drawLandingGizmo", "actionGizmoHistoryDuration", "drawCameraPivotGizmo"),
                            "showDebugGizmos",
                            true),
                        new GroupDefinition(
                            "Hover Label",
                            "Show hovered gizmo names in game view for quick identification.",
                            Fields("showGizmoHoverLabel", "gizmoHoverPixelRadius", "gizmoHoverLabelTextColor", "gizmoHoverLabelBackgroundColor"),
                            "showDebugGizmos",
                            true),
                        new GroupDefinition(
                            "Gizmo Colors",
                            "Color palette for ground, action, and impact visualization gizmos.",
                            Fields(
                                "groundProbeGizmoColor",
                                "slopeNormalGizmoColor",
                                "slopeDownhillGizmoColor",
                                "stepClearGizmoColor",
                                "stepBlockedGizmoColor",
                                "stepSurfaceGizmoColor",
                                "stepCandidateGizmoColor",
                                "teeterGroundedGizmoColor",
                                "teeterOpenEdgeGizmoColor",
                                "teeterDirectionGizmoColor",
                                "teeterEventGizmoColor",
                                "teeterSnapBackGizmoColor",
                                "dashGizmoColor",
                                "diveGizmoColor",
                                "rollGizmoColor",
                                "slideGizmoColor",
                                "slopeSlideGizmoColor",
                                "activeSlidePredictionGizmoColor",
                                "jumpHeadroomBlockedGizmoColor",
                                "jumpHeadroomClearGizmoColor",
                                "landingGizmoColor",
                                "cameraPivotGizmoColor"),
                            "showDebugGizmos",
                            true),
                        new GroupDefinition(
                            "Runtime State",
                            "Read-only state information shown during play mode.",
                            new[] { new FieldDefinition("debugState", true) })
                    })
            });
    }

    private static TabDefinition CreateParticlesTab()
    {
        return new TabDefinition(
            "particles",
            "Particles",
            "Configure walking and landing particle prefab spawning.",
            new[]
            {
                new SectionDefinition(
                    "particle_setup",
                    "Particle Setup",
                    "Spawn position and prefab references for walking and landing particles.",
                    true,
                    new[]
                    {
                        new GroupDefinition(
                            "Particle References",
                            "Walking particle is created once while grounded if missing. Landing particle is created on jump landings.",
                            new[]
                            {
                                new FieldDefinition("particleLocalPosition", false, "Position"),
                                new FieldDefinition("walkingParticlePrefab", false, "Walking Particle"),
                                new FieldDefinition("landingParticlePrefab", false, "Landing Particle")
                            })
                    })
            });
    }

    private static FieldDefinition[] Fields(params string[] propertyNames)
    {
        FieldDefinition[] fields = new FieldDefinition[propertyNames.Length];
        for (int i = 0; i < propertyNames.Length; i++)
        {
            fields[i] = new FieldDefinition(propertyNames[i]);
        }

        return fields;
    }
}

[CustomEditor(typeof(MinimoTpsPreset))]
[CanEditMultipleObjects]
public class MinimoTpsPresetEditor : Editor
{
    private const string FoldoutSessionPrefix = "Minimo.TpsPresetEditor.";

    private bool showMovement = true;
    private bool showJumpAndFall = true;
    private bool showAdvanced = true;
    private bool showCamera = true;

    private sealed class PresetGroupDefinition
    {
        public PresetGroupDefinition(string title, string description, params string[] fields)
        {
            Title = title;
            Description = description;
            Fields = fields ?? Array.Empty<string>();
        }

        public string Title { get; }
        public string Description { get; }
        public string[] Fields { get; }
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawOverview();
        EditorGUILayout.Space(4f);
        DrawSectionToolbar();
        EditorGUILayout.Space(2f);

        DrawSection(ref showMovement, "movement", "Movement", "Core locomotion preset values.", new[]
        {
            new PresetGroupDefinition(
                "Base Movement",
                "Ground movement speed, acceleration, and deceleration values.",
                "moveSpeed",
                "groundAcceleration",
                "turnAcceleration",
                "groundDeceleration",
                "airAcceleration",
                "airControl",
                "crouchWalkSpeedMultiplier"),
            new PresetGroupDefinition(
                "Turning And Responsiveness",
                "Multipliers that shape turning response and direction changes.",
                "reverseDirectionAccelerationMultiplier",
                "rotationSharpness",
                "snappyReverseTurnMultiplier",
                "reversePivotThreshold",
                "pivotBrakeDeceleration")
        });

        DrawSection(ref showJumpAndFall, "jump", "Jump And Fall", "Jump and gravity-related preset values.", new[]
        {
            new PresetGroupDefinition(
                "Jump Core",
                "Rise, hold, and minimum jump behavior.",
                "jumpHeight",
                "gravity",
                "jumpHoldTime",
                "jumpHoldForce",
                "minimumJumpHeight",
                "jumpCutMultiplier",
                "jumpCeilingCheckDistance",
                "groundedVerticalForce"),
            new PresetGroupDefinition(
                "Fall",
                "Fall and apex behavior with air-time forgiveness windows.",
                "fallGravityMultiplier",
                "fastFallGravityMultiplier",
                "fallAnimationVerticalSpeedThreshold",
                "apexVerticalSpeedThreshold",
                "apexGravityMultiplier",
                "terminalVelocity",
                "coyoteTime",
                "jumpBufferTime",
                "enableDoubleJump",
                "maxAirJumps",
                "airJumpHeightMultiplier"),
            new PresetGroupDefinition(
                "Jump Blob Feel",
                "Controls jump-time blob/squash-stretch pulse behavior.",
                "enableCharacterFeel",
                "enableJumpBlobEffect",
                "jumpStretch",
                "jumpStretchDecaySpeed",
                "jumpBlobHorizontalCompression",
                "jumpBlobVerticalStretch",
                "jumpBlobVerticalVelocityInfluence"),
            new PresetGroupDefinition(
                "Slope And Step",
                "Core slope speed influence and step-up helper values.",
                "slopeSpeedInfluence",
                "uphillSpeedPenalty",
                "downhillSpeedBoost",
                "slopeAntiSlideDeceleration",
                "slopeAlignmentSharpness",
                "maxStepHeight",
                "stepCheckDistance"),
            new PresetGroupDefinition(
                "Ledge Hand Transforms",
                "Optional hand transform references and probe radii for ledge hang detection.",
                "leftHandLedgeTransform",
                "rightHandLedgeTransform",
                "leftHandLedgeProbeRadius",
                "rightHandLedgeProbeRadius")
        });

        DrawSection(ref showAdvanced, "advanced", "Advanced Actions", "Dash, ground pound, and sliding preset values.", new[]
        {
            new PresetGroupDefinition(
                "Dash And Ground Pound",
                "Dash and heavy downward slam tuning values.",
                "dashSpeed",
                "dashDuration",
                "dashCooldown",
                "groundPoundSpeed",
                "groundPoundAcceleration"),
            new PresetGroupDefinition(
                "Crouch And Slide",
                "Crouch and sliding behavior.",
                "crouchHeight",
                "crouchWalkSpeedMultiplier",
                "slideDuration",
                "slideInitialBoost",
                "slideDeceleration"),
            new PresetGroupDefinition(
                "Dive And Roll",
                "Aerial dive behavior and roll momentum transfer.",
                "diveForwardSpeed",
                "diveForwardBoost",
                "diveDownwardSpeed",
                "diveGravityMultiplier",
                "diveDuration",
                "rollBaseSpeed",
                "rollMomentumConversion",
                "rollDuration",
                "rollDurationMultiplier",
                "rollLandingResumeDurationRatio",
                "rollLandingResumeMaxDuration",
                "rollDeceleration"),
            new PresetGroupDefinition(
                "Slope Slide",
                "Slope slide and momentum carry-over values.",
                "slopeSlideMinAngle",
                "slopeSlideEnterMinSpeed",
                "slopeSlideDownhillAcceleration",
                "slopeSlideFriction",
                "slopeSlideMaxSpeed",
                "slopeMomentumCarryDuration",
                "slopeMomentumRetention",
                "slopeMomentumDecay",
                "slopeJumpSpeedMultiplier")
        });

        DrawSection(ref showCamera, "camera", "Camera", "Follow, zoom, and mouse sensitivity preset values.", new[]
        {
            new PresetGroupDefinition(
                "Rig And Input",
                "Follow distance, zoom values, and mouse sensitivity.",
                "followDistance",
                "minZoomDistance",
                "maxZoomDistance",
                "mouseXSensitivity",
                "mouseYSensitivity"),
            new PresetGroupDefinition(
                "FOV And Landing Feel",
                "Speed-based FOV and landing shake contribution.",
                "baseCameraFov",
                "maxBoostCameraFov",
                "speedFovWeight",
                "airDashFovWeight",
                "slopeSlideFovWeight",
                "fovSpeedReference",
                "landingCameraShakeMultiplier")
        });

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawOverview()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Minimo TPS Preset", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Preset inspector is grouped into clean cards so movement, jump, action, and camera values can be tuned faster.",
                EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.Space(6f);
            DrawSummaryLine("Jump", GetFloatValue("jumpHeight").ToString("F2"));
            DrawSummaryLine("Dash", GetFloatValue("dashSpeed").ToString("F2"));
            DrawSummaryLine("Camera", GetFloatValue("followDistance").ToString("F2"));
            DrawSummaryLine("FOV", GetFloatValue("baseCameraFov").ToString("F1"));
        }
    }

    private void DrawSectionToolbar()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Expand All Sections"))
            {
                showMovement = true;
                showJumpAndFall = true;
                showAdvanced = true;
                showCamera = true;
            }

            if (GUILayout.Button("Collapse All Sections"))
            {
                showMovement = false;
                showJumpAndFall = false;
                showAdvanced = false;
                showCamera = false;
            }
        }
    }

    private void DrawSummaryLine(string label, string value)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(label, GUILayout.Width(82f));
            EditorGUILayout.SelectableLabel(value, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
        }
    }

    private float GetFloatValue(string propertyName)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
        {
            return 0f;
        }

        return property.propertyType switch
        {
            SerializedPropertyType.Float => property.floatValue,
            SerializedPropertyType.Integer => property.intValue,
            _ => 0f
        };
    }

    private void DrawSection(ref bool expanded, string sectionId, string title, string tooltip, IReadOnlyList<PresetGroupDefinition> groups)
    {
        expanded = EditorGUILayout.Foldout(expanded, new GUIContent(title, tooltip), true);
        if (expanded)
        {
            EditorGUILayout.LabelField(tooltip, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(4f);

            for (int i = 0; i < groups.Count; i++)
            {
                DrawGroup(sectionId, i, groups[i]);
            }
        }

        EditorGUILayout.Space(3f);
    }

    private void DrawGroup(string sectionId, int groupIndex, PresetGroupDefinition group)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            string stateKey = $"{FoldoutSessionPrefix}{target.GetType().Name}.{sectionId}.group{groupIndex}";
            bool expanded = SessionState.GetBool(stateKey, true);
            expanded = EditorGUILayout.Foldout(expanded, new GUIContent(group.Title, group.Description), true);
            SessionState.SetBool(stateKey, expanded);

            if (!expanded)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(group.Description))
            {
                EditorGUILayout.LabelField(group.Description, EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.Space(2f);
            }

            EditorGUI.indentLevel++;
            for (int i = 0; i < group.Fields.Length; i++)
            {
                DrawPresetField(group.Fields[i]);
            }

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(4f);
    }

    private void DrawPresetField(string propertyName)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null || !MinimoInspectorVisibility.ShouldDrawProperty(property))
        {
            return;
        }

        EditorGUILayout.PropertyField(property, MinimoInspectorLocalization.BuildContent(property), true);
    }
}
#endif

