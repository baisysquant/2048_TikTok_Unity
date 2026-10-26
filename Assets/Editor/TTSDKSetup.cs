#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

[InitializeOnLoad]
public static class TTSDKSetup
{
    static TTSDKSetup()
    {
        EditorApplication.delayCall += EnsureTTSDKDefine;
    }

    private static void EnsureTTSDKDefine()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Plugins/com.tiktok.minigame"))
            return;

        var defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.WebGL);
        if (!defines.Contains("TTSDK"))
        {
            string newDefines = string.IsNullOrEmpty(defines) ? "TTSDK" : defines + ";TTSDK";
            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.WebGL, newDefines);
            Debug.Log("[TTSDKSetup] Auto-enabled TTSDK define for WebGL");
        }
    }

    [MenuItem("Tools/TikTok/Enable TTSDK Define (All Platforms)")]
    public static void EnableTTSDKDefine()
    {
        foreach (var target in new[]
        {
            NamedBuildTarget.Standalone,
            NamedBuildTarget.WebGL,
            NamedBuildTarget.Android,
            NamedBuildTarget.iOS
        })
        {
            var defines = PlayerSettings.GetScriptingDefineSymbols(target);
            if (!defines.Contains("TTSDK"))
            {
                string newDefines = string.IsNullOrEmpty(defines) ? "TTSDK" : defines + ";TTSDK";
                PlayerSettings.SetScriptingDefineSymbols(target, newDefines);
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[TTSDKSetup] TTSDK define symbol set for Standalone/WebGL/Android/iOS");
    }
}
#endif
