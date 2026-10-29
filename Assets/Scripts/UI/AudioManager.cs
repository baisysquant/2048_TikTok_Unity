using UnityEngine;

public class AudioManager : MonoBehaviour
{
    private const string SoundSaveKey = "fk2048_sound_on";   // 与微信版一致的开关持久化键

    [SerializeField] private bool _soundEnabled = true;
    [SerializeField, Range(0, 1)] private float _sfxVolume = 0.5f;

    public bool IsSoundEnabled => _soundEnabled;

    private AudioSource _sfxSource;

    private void Awake()
    {
        // 读取持久化声音开关（对齐微信版：默认开启）
        try
        {
            string saved = TikTokBridge.GetString(SoundSaveKey, "");
            if (saved == "0") _soundEnabled = false;
            else if (saved == "1") _soundEnabled = true;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[AudioManager] Load sound setting failed: " + e.Message);
        }

        if (TiktokAudioBridge.IsTikTokRuntime)
        {
            TiktokAudioBridge.Initialize();
            TiktokAudioBridge.SetVolume(_sfxVolume);
        }
        else
        {
            _sfxSource = gameObject.AddComponent<AudioSource>();
            _sfxSource.loop = false;
            _sfxSource.volume = _sfxVolume;
            TiktokAudioBridge.InitEditorSource(_sfxSource);
        }
        TiktokAudioBridge.SetEnabled(_soundEnabled);
    }

    public void ToggleSound()
    {
        _soundEnabled = !_soundEnabled;
        TiktokAudioBridge.SetEnabled(_soundEnabled);
        try
        {
            TikTokBridge.SetString(SoundSaveKey, _soundEnabled ? "1" : "0");
            TikTokBridge.Save();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[AudioManager] Save sound setting failed: " + e.Message);
        }
    }

    private void Play(string name)
    {
        if (!_soundEnabled) return;
        TiktokAudioBridge.Play(name);
    }

    public void PlayMove()      { Play("move"); }
    public void PlaySpawn()     { Play("spawn"); }
    public void PlayMerge(int value) { Play("merge"); }
    public void PlayGameOver()  { Play("gameover"); }
    public void PlayWin()       { Play("win"); }

    // 对齐微信版 WebAudio 音效事件：合成512触发/入池/奖励/回撤/点击
    public void PlaySpecial()   { Play("special"); }
    public void PlayBank()      { Play("bank"); }
    public void PlayReward()    { Play("reward"); }
    public void PlayUndo()      { Play("undo"); }
    public void PlayClick()     { Play("click"); }
}
