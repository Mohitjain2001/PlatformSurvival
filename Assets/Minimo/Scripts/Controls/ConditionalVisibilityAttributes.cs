using System;
using UnityEngine;

[AttributeUsage(AttributeTargets.Field)]
public sealed class ShowIfAttribute : PropertyAttribute
{
    public string ConditionFieldName { get; }

    public ShowIfAttribute(string conditionFieldName)
    {
        ConditionFieldName = conditionFieldName;
    }
}

[AttributeUsage(AttributeTargets.Field)]
public sealed class HideIfAttribute : PropertyAttribute
{
    public string ConditionFieldName { get; }

    public HideIfAttribute(string conditionFieldName)
    {
        ConditionFieldName = conditionFieldName;
    }
}

internal static class MinimoUnityCompatibility
{
    public static T FindFirstObjectByType<T>() where T : UnityEngine.Object
    {
#if UNITY_2022_2_OR_NEWER
        return UnityEngine.Object.FindFirstObjectByType<T>();
#else
#pragma warning disable 0618
        return UnityEngine.Object.FindObjectOfType<T>();
#pragma warning restore 0618
#endif
    }

    public static T FindFirstObjectByType<T>(bool includeInactive) where T : UnityEngine.Object
    {
#if UNITY_2022_2_OR_NEWER
        return UnityEngine.Object.FindFirstObjectByType<T>(
            includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude);
#else
#pragma warning disable 0618
        return UnityEngine.Object.FindObjectOfType<T>(includeInactive);
#pragma warning restore 0618
#endif
    }

    public static T[] FindObjectsByType<T>(bool includeInactive) where T : UnityEngine.Object
    {
#if UNITY_2022_2_OR_NEWER
        return UnityEngine.Object.FindObjectsByType<T>(
            includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
#else
#pragma warning disable 0618
        return UnityEngine.Object.FindObjectsOfType<T>(includeInactive);
#pragma warning restore 0618
#endif
    }
}
