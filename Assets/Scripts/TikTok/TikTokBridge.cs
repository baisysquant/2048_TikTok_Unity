using UnityEngine;
#if TTSDK
using TTSDK;
#endif

public static class TikTokBridge
{
    private static bool _isInitialized;
    private static System.Action _onInitialized;
    private static readonly System.Collections.Generic.HashSet<string> _pendingKeys =
        new System.Collections.Generic.HashSet<string>();

    private static string _rewardedAdUnitId = "";
    private static string _interstitialAdUnitId = "";

    private static void LoadAdConfig()
    {
        var config = Resources.Load<TextAsset>("AdConfig");
        if (config == null)
        {
            Debug.LogWarning("[TikTokBridge] AdConfig.json not found in Resources");
            return;
        }

        try
        {
            var json = JsonUtility.FromJson<AdConfigData>(config.text);
            _rewardedAdUnitId = json.rewardedAdUnitId ?? "";
            _interstitialAdUnitId = json.interstitialAdUnitId ?? "";
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[TikTokBridge] Failed to parse AdConfig.json: {e.Message}");
        }
    }

    [System.Serializable]
    private class AdConfigData
    {
        public string rewardedAdUnitId;
        public string interstitialAdUnitId;
    }

    public static void Initialize(System.Action onInitialized = null)
    {
        if (_isInitialized)
        {
            onInitialized?.Invoke();
            return;
        }

        _onInitialized = onInitialized;
        LoadAdConfig();

#if TTSDK
        TT.InitSDK((code, env) =>
        {
            if (code == 0)
            {
                _isInitialized = true;
                Debug.Log($"[TikTokBridge] SDK initialized. AppId: {env?.GameAppId}, Host: {env?.m_HostEnum}");
                MigratePendingValues();
                _onInitialized?.Invoke();
                _onInitialized = null;
            }
            else
            {
                Debug.LogError($"[TikTokBridge] SDK init failed. Code: {code}");
            }
        });
#else
        Debug.Log("[TikTokBridge] Running without TTSDK (local debug mode)");
        onInitialized?.Invoke();
#endif
    }

    public static void ShareScore(int score)
    {
        if (!_isInitialized) return;

#if TTSDK
        TT.ShareAppMessage(new ShareAppMessageParam
        {
            Title = "2048",
            Subtitle = $"I scored {score} in 2048! Can you beat me?",
            TemplateType = 0,
            ImageUrl = "",
            Success = () => Debug.Log("[TikTokBridge] Share success"),
            Fail = err => Debug.Log($"[TikTokBridge] Share fail: {err.ErrMsg}"),
            Complete = () => Debug.Log("[TikTokBridge] Share complete")
        });
#else
        Debug.Log($"[TikTokBridge] Share score: {score}");
#endif
    }

    public static void ShowRewardedAd(System.Action<bool> onComplete, System.Action onUnavailable = null)
    {
        if (!_isInitialized)
        {
            onComplete?.Invoke(false);
            onUnavailable?.Invoke();
            return;
        }

#if TTSDK
        if (string.IsNullOrEmpty(_rewardedAdUnitId))
        {
            Debug.LogWarning("[TikTokBridge] Rewarded ad unit id is empty. Falling back to direct reward.");
            onComplete?.Invoke(false);
            onUnavailable?.Invoke();
            return;
        }

        var ad = TT.CreateRewardedVideoAd(new CreateRewardedVideoAdParam
        {
            AdUnitId = _rewardedAdUnitId
        });

        bool callbackDone = false;
        System.Action<bool> once = result =>
        {
            if (callbackDone) return;
            callbackDone = true;
            onComplete?.Invoke(result);
            if (!result) onUnavailable?.Invoke();
        };

        ad.OnLoad += () => ad.Show();
        ad.OnError += (code, msg) =>
        {
            Debug.LogError($"[TikTokBridge] Rewarded ad error. Code: {code}, Msg: {msg}");
            once(false);
        };
        ad.OnClose += isEnded =>
        {
            Debug.Log($"[TikTokBridge] Rewarded ad closed. isEnded: {isEnded}");
            once(isEnded);
        };
        ad.Load();
#else
        Debug.Log("[TikTokBridge] Rewarded ad (SDK not enabled)");
        onComplete?.Invoke(false);
#endif
    }

    public static void ShowInterstitialAd()
    {
        if (!_isInitialized) return;

#if TTSDK
        if (string.IsNullOrEmpty(_interstitialAdUnitId))
        {
            Debug.LogError("[TikTokBridge] Interstitial ad unit id is empty. Set it in Assets/Resources/AdConfig.json");
            return;
        }

        var ad = TT.CreateInterstitialAd(new CreateInterstitialAdParam
        {
            InterstitialAdId = _interstitialAdUnitId
        });

        ad.OnLoad += () => ad.Show();
        ad.OnError += (code, msg) =>
            Debug.LogError($"[TikTokBridge] Interstitial ad error. Code: {code}, Msg: {msg}");
        ad.Load();
#else
        Debug.Log("[TikTokBridge] Interstitial ad (SDK not enabled)");
#endif
    }

    public static bool IsInitialized => _isInitialized;

    /// <summary>激励视频广告位是否已配置（未配置时各广告入口应降级提示，不进入 SDK 流程）</summary>
    public static bool IsRewardedAdConfigured()
    {
        if (_rewardedAdUnitId == null) LoadAdConfig();   // 理论上 Initialize 已加载，双保险
        return !string.IsNullOrEmpty(_rewardedAdUnitId);
    }

    // ========== Required: Silent Login ==========
    public static void SilentLogin(System.Action<string> onComplete)
    {
        if (!_isInitialized) { onComplete?.Invoke(null); return; }

#if TTSDK
        TT.Login(code =>
        {
            Debug.Log($"[TikTokBridge] Login success. Code: {code}");
            onComplete?.Invoke(code);
        }, errMsg =>
        {
            Debug.Log($"[TikTokBridge] Login failed. Msg: {errMsg}");
            onComplete?.Invoke(null);
        });
#else
        Debug.Log("[TikTokBridge] Silent login (SDK not enabled)");
        onComplete?.Invoke(null);
#endif
    }

    // ========== Required: Home Screen Shortcut ==========
    public static void AddHomeScreenShortcut(System.Action<bool> onComplete)
    {
        if (!_isInitialized) { onComplete?.Invoke(false); return; }

#if TTSDK
        TT.AddShortcut(success =>
        {
            Debug.Log($"[TikTokBridge] AddShortcut: {success}");
            onComplete?.Invoke(success);
        }, true);
#else
        Debug.Log("[TikTokBridge] AddShortcut (SDK not enabled)");
        onComplete?.Invoke(false);
#endif
    }

    // ========== Required: Revisit from Profile ==========
    public static void StartEntranceMission()
    {
        if (!_isInitialized) return;

#if TTSDK
        TT.StartEntranceMission(new TTStartEntranceMissionParam
        {
            success = res => Debug.Log($"[TikTokBridge] StartEntranceMission ok. is_sufficient: {res.is_sufficient}"),
            fail = err => Debug.Log($"[TikTokBridge] StartEntranceMission fail: {err.ErrMsg}"),
            complete = res => Debug.Log("[TikTokBridge] StartEntranceMission complete")
        });
#else
        Debug.Log("[TikTokBridge] StartEntranceMission (SDK not enabled)");
#endif
    }

    public static void GetEntranceMissionReward(System.Action<bool> onComplete)
    {
        if (!_isInitialized) { onComplete?.Invoke(false); return; }

#if TTSDK
        TT.GetEntranceMissionReward(new TTGetEntranceMissionRewardParam
        {
            success = res =>
            {
                bool rewarded = res.canReceiveReward;
                Debug.Log($"[TikTokBridge] GetEntranceMissionReward: {rewarded}");
                onComplete?.Invoke(rewarded);
            },
            fail = err =>
            {
                Debug.Log($"[TikTokBridge] GetEntranceMissionReward fail: {err.ErrMsg}");
                onComplete?.Invoke(false);
            },
            complete = res => { }
        });
#else
        Debug.Log("[TikTokBridge] GetEntranceMissionReward (SDK not enabled)");
        onComplete?.Invoke(false);
#endif
    }

    public static void SetString(string key, string value)
    {
#if TTSDK
        if (_isInitialized)
        {
            TT.PlayerPrefs.SetString(key, value);
            return;
        }
        _pendingKeys.Add(key);
        PlayerPrefs.SetString(key, value);
#else
        PlayerPrefs.SetString(key, value);
#endif
    }

    public static string GetString(string key, string defaultValue = "")
    {
#if TTSDK
        if (_isInitialized)
            return TT.PlayerPrefs.GetString(key, defaultValue);
        _pendingKeys.Add(key);
        return PlayerPrefs.GetString(key, defaultValue);
#else
        return PlayerPrefs.GetString(key, defaultValue);
#endif
    }

    public static void SetInt(string key, int value)
    {
        SetString(key, value.ToString());
    }

    public static int GetInt(string key, int defaultValue = 0)
    {
        string raw = GetString(key, defaultValue.ToString());
        return int.TryParse(raw, out int result) ? result : defaultValue;
    }

    public static void Save()
    {
#if TTSDK
        if (_isInitialized)
        {
            TT.PlayerPrefs.Save();
            return;
        }
        PlayerPrefs.Save();
#else
        PlayerPrefs.Save();
#endif
    }

    private static void MigratePendingValues()
    {
        if (_pendingKeys.Count == 0) return;

        foreach (var key in _pendingKeys)
        {
            if (PlayerPrefs.HasKey(key))
                TT.PlayerPrefs.SetString(key, PlayerPrefs.GetString(key));
        }
        TT.PlayerPrefs.Save();
        _pendingKeys.Clear();
        Debug.Log("[TikTokBridge] Migrated local PlayerPrefs values to TT.PlayerPrefs");
    }
}
