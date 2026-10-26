using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class SceneSetup
{
    private static bool _attempted;

    static SceneSetup()
    {
        EditorApplication.delayCall += TryCreateScene;
    }

    private static void TryCreateScene()
    {
        if (_attempted) return;
        _attempted = true;

        if (!string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
            return;

        string[] results = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" });
        if (results.Length > 0)
        {
            string scenePath = AssetDatabase.GUIDToAssetPath(results[0]);
            EditorSceneManager.OpenScene(scenePath);
            Debug.Log($"[SceneSetup] Opened existing scene: {scenePath}");
            return;
        }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camGO = new GameObject("MainCamera", typeof(Camera), typeof(AudioListener));
        camGO.tag = "MainCamera";
        var cam = camGO.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.96f, 0.94f, 0.90f);
        cam.orthographic = true;

        var lightGO = new GameObject("Directional Light", typeof(Light));
        var light = lightGO.GetComponent<Light>();
        light.type = LightType.Directional;

        if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            AssetDatabase.CreateFolder("Assets", "Scenes");

        string path = "Assets/Scenes/Main.unity";
        EditorSceneManager.SaveScene(scene, path);
        EditorSceneManager.OpenScene(path);

        Debug.Log($"[SceneSetup] Created default scene at: {path}");
    }
}