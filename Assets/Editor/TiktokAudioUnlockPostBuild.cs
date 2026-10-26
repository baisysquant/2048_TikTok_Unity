using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public class TiktokAudioUnlockPostBuild : IPostprocessBuildWithReport
{
    public int callbackOrder => 0;

    private const string Anchor =
        "window.addEventListener(\"mousedown\",_userEventCallback);window.addEventListener(\"touchstart\",_userEventCallback);Module.deinitializers.push(function(){window.removeEventListener(\"mousedown\",_userEventCallback);window.removeEventListener(\"touchstart\",_userEventCallback)})";

    private const string Patch =
        "window.addEventListener(\"mousedown\",_userEventCallback);window.addEventListener(\"touchstart\",_userEventCallback);if(typeof tt!==\"undefined\"&&typeof tt.onTouchStart===\"function\"){tt.onTouchStart(function(){try{if(WEBAudio.audioContext&&WEBAudio.audioContext.state!==\"running\"&&WEBAudio.audioContext.state!==\"closed\"){WEBAudio.audioContext.resume().catch(function(e){console.warn(\"Could not resume audio context from tt touch. \"+e)})}}catch(e){}})}Module.deinitializers.push(function(){window.removeEventListener(\"mousedown\",_userEventCallback);window.removeEventListener(\"touchstart\",_userEventCallback)})";

    public void OnPostprocessBuild(BuildReport report)
    {
        string root = Path.GetDirectoryName(report.summary.outputPath);
        PatchFramework(root);
        CopyAudioFiles(root);
    }

    private void PatchFramework(string root)
    {
        string frameworkPath = FindFramework(root, 3);
        if (frameworkPath == null)
        {
            Debug.LogWarning("[AudioUnlock] webgl.framework.js not found under " + root);
            return;
        }

        string content = File.ReadAllText(frameworkPath);
        if (content.Contains("tt.onTouchStart"))
        {
            Debug.Log("[AudioUnlock] Already patched: " + frameworkPath);
            return;
        }

        if (!content.Contains(Anchor))
        {
            Debug.LogWarning("[AudioUnlock] Anchor not found in " + frameworkPath + ". Audio unlock NOT applied.");
            return;
        }

        File.WriteAllText(frameworkPath, content.Replace(Anchor, Patch));
        Debug.Log("[AudioUnlock] Applied tt.onTouchStart audio unlock to " + frameworkPath);
    }

    private void CopyAudioFiles(string root)
    {
        string srcDir = Path.Combine(Application.dataPath, "StreamingAssets", "audio");
        if (!Directory.Exists(srcDir))
        {
            Debug.LogWarning("[AudioUnlock] StreamingAssets/audio not found: " + srcDir);
            return;
        }

        string destDir = FindAudioDestDir(root);
        if (destDir == null)
        {
            Debug.LogWarning("[AudioUnlock] Could not locate build output dir for audio files");
            return;
        }

        Directory.CreateDirectory(destDir);
        foreach (var file in Directory.GetFiles(srcDir))
        {
            string dest = Path.Combine(destDir, Path.GetFileName(file));
            File.Copy(file, dest, true);
            Debug.Log("[AudioUnlock] Copied " + Path.GetFileName(file) + " -> " + dest);
        }
    }

    private static string FindAudioDestDir(string root)
    {
        // 构建输出根目录（可能直接是 tt-minigame 根，也可能多一层）
        for (int depth = 0; depth <= 3 && root != null; depth++)
        {
            string candidate = Path.Combine(root, "audio");
            string probe = Path.Combine(root, "game.js");
            if (File.Exists(probe))
            {
                return candidate;
            }
            root = Path.GetDirectoryName(root);
        }
        return null;
    }

    private static string FindFramework(string startDir, int maxDepth)
    {
        for (int depth = 0; depth <= maxDepth && startDir != null; depth++)
        {
            string candidate = Path.Combine(startDir, "webgl.framework.js");
            if (File.Exists(candidate)) return candidate;
            startDir = Path.GetDirectoryName(startDir);
        }
        return null;
    }
}
