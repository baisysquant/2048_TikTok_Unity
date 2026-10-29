using System.Runtime.InteropServices;
using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using UnityEngine.Scripting;
#endif

public static class TiktokAudioBridge
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [Preserve]
    [DllImport("__Internal")]
    private static extern void TTWebAudioInit();

    [Preserve]
    [DllImport("__Internal")]
    private static extern void TTWebAudioPlay(string name);

    [Preserve]
    [DllImport("__Internal")]
    private static extern void TTWebAudioSetEnabled(int enabled);

    [Preserve]
    [DllImport("__Internal")]
    private static extern void TTWebAudioUnlock();

    [Preserve]
    [DllImport("__Internal")]
    private static extern void TTWebAudioSetVolume(float volume);
#else
    private static void TTWebAudioInit() { }
    private static void TTWebAudioPlay(string name) { }
    private static void TTWebAudioSetEnabled(int enabled) { }
    private static void TTWebAudioUnlock() { }
    private static void TTWebAudioSetVolume(float volume) { }
#endif

    private static bool _initialized;
    private static AudioSource _editorSource;
    private static AudioClip _editorMove;
    private static AudioClip _editorSpawn;
    private static AudioClip _editorMerge;
    private static AudioClip _editorGameOver;
    private static AudioClip _editorWin;
    private static AudioClip _editorSpecial;
    private static AudioClip _editorBank;
    private static AudioClip _editorReward;
    private static AudioClip _editorUndo;
    private static AudioClip _editorClick;

    public static bool IsTikTokRuntime
    {
        get
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return true;
#else
            return false;
#endif
        }
    }

    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;
        TTWebAudioInit();
    }

    public static void SetVolume(float volume)
    {
        TTWebAudioSetVolume(volume);
    }

    public static void UnlockOnTouch()
    {
        TTWebAudioUnlock();
    }

    public static void SetEnabled(bool enabled)
    {
        TTWebAudioSetEnabled(enabled ? 1 : 0);
    }

    public static void Play(string name)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        TTWebAudioPlay(name);
#else
        if (_editorSource == null || _editorSource.gameObject == null) return;
        var clip = GetEditorClip(name);
        if (clip != null) _editorSource.PlayOneShot(clip);
#endif
    }

    public static void InitEditorSource(AudioSource source)
    {
        _editorSource = source;
        _editorMove = Resources.Load<AudioClip>("Audio/move");
        _editorSpawn = Resources.Load<AudioClip>("Audio/spawn");
        _editorMerge = Resources.Load<AudioClip>("Audio/merge");
        _editorGameOver = Resources.Load<AudioClip>("Audio/gameover");
        _editorWin = Resources.Load<AudioClip>("Audio/win");
        _editorSpecial = Resources.Load<AudioClip>("Audio/special");
        _editorBank = Resources.Load<AudioClip>("Audio/bank");
        _editorReward = Resources.Load<AudioClip>("Audio/reward");
        _editorUndo = Resources.Load<AudioClip>("Audio/undo");
        _editorClick = Resources.Load<AudioClip>("Audio/click");
    }

    private static AudioClip GetEditorClip(string name)
    {
        switch (name)
        {
            case "move": return _editorMove;
            case "spawn": return _editorSpawn;
            case "merge": return _editorMerge;
            case "gameover": return _editorGameOver;
            case "win": return _editorWin;
            case "special": return _editorSpecial;
            case "bank": return _editorBank;
            case "reward": return _editorReward;
            case "undo": return _editorUndo;
            case "click": return _editorClick;
            default: return null;
        }
    }
}
