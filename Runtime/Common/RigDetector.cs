using System;

public static class RigDetector
{
    private static string _prefabSuffix = "";
    
    public static string PrefabSuffix()
    {
        if (!string.IsNullOrEmpty(_prefabSuffix)) return _prefabSuffix;
#if UNITY_ANDROID && !UNITY_EDITOR
        if (IsUsingOVR()) _prefabSuffix = "_Meta";
        else _prefabSuffix = "_OpenXR";
#else
        else _prefabSuffix = "_Default";
#endif
        return _prefabSuffix;
    }
    
    private static bool IsUsingOVR()
    {
        return FindType("OVRCameraRig") != null || FindType("OVRManager") != null;
    }

    private static bool IsUsingXRI()
    {
        return FindType("UnityEngine.XR.Interaction.Toolkit.XRInteractionManager") != null ||
               FindType("UnityEngine.XR.Interaction.Toolkit.XRRig") != null;
    }

    private static Type FindType(string typeName)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                var type = assembly.GetType(typeName, false);
                if (type != null)
                    return type;
            }
            catch { }
        }
        return null;
    }
}