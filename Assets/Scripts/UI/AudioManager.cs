using UnityEngine;

public class AudioManager : MonoBehaviour
{
    [SerializeField] private bool _soundEnabled = true;
    [SerializeField, Range(0, 1)] private float _sfxVolume = 0.5f;

    public bool IsSoundEnabled => _soundEnabled;

    private AudioSource _sfxSource;

    private void Awake()
    {
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
    }

    public void ToggleSound()
    {
        _soundEnabled = !_soundEnabled;
        TiktokAudioBridge.SetEnabled(_soundEnabled);
    }

    public void PlayMove()
    {
        if (!_soundEnabled) return;
        TiktokAudioBridge.Play("move");
    }

    public void PlaySpawn()
    {
        if (!_soundEnabled) return;
        TiktokAudioBridge.Play("spawn");
    }

    public void PlayMerge(int value)
    {
        if (!_soundEnabled) return;
        TiktokAudioBridge.Play("merge");
    }

    public void PlayGameOver()
    {
        if (!_soundEnabled) return;
        TiktokAudioBridge.Play("gameover");
    }

    public void PlayWin()
    {
        if (!_soundEnabled) return;
        TiktokAudioBridge.Play("win");
    }
}
