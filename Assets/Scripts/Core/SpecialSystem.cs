using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 特技加速系统（对齐微信版 2048-wechat-canvas src/special.js）：
///   - 触发：一次合并新生成 512（256+256）时触发（更高等级不触发）
///   - 闪烁机会：棋盘随机空格出现 ⚡×512 按钮，10 秒倒计时（最后 30% 变红发热）
///     点击/超时 → 沉入特技池；该格被占用自动转移；无空格直接入池；池满直接兑现 +2048
///   - 特技池：FIFO 队列，上限 10，单局最多获得 10 次，持久化存储
///   - 奖励：每局第一次使用免广告 +2048；之后完整观看激励视频 +2048；
///     广告失败/中途关闭不扣库存，可重试；点击 1 秒冷却
/// 纯 C# 类，不由 MonoBehaviour 持有；由 GameManager 创建并每帧调用 Update()。
/// </summary>
public class SpecialSystem
{
    public const int SpecialTriggerValue = 512;
    public const int SpecialCap = 10;            // 特技池上限
    public const int SpecialMaxPerGame = 10;     // 单局最多获得次数
    public const long SpecialReward = 2048;      // 每次奖励分数
    public const float SpecialFloatMs = 10000f;  // 闪烁按钮存在时长
    public const float SpecialFadeMs = 260f;     // 倒计时结束后的渐隐时长
    public const float SpecialAdLoadTimeout = 5f;// 广告加载超时（秒）
    public const float SpecialAdCooldown = 1f;   // 点击冷却（秒）
    public const string SaveKey = "fk2048_special_pool";

    /// <summary>调测开关：true 时特技/复活/回撤跳过激励视频直接发放（上线前改 false）</summary>
    public bool DebugSkipAd = false;

    [Serializable]
    public class SpecialItem
    {
        public int id;
        public int value = SpecialTriggerValue;
        public long ts;
    }

    public class SpecialPending
    {
        public int gx;
        public int gy;
        public float spawnAt;
        public SpecialPending(int x, int y, float t) { gx = x; gy = y; spawnAt = t; }
    }

    private readonly Game2048 _game;
    private readonly List<SpecialItem> _pool = new List<SpecialItem>(SpecialCap);
    private int _idSeq = 1;
    private SpecialPending _pending;
    private int _triggeredInGame;          // 本局已获得次数
    private bool _freeUsedInGame;          // 本局首次免广告是否已用
    private bool _adBusy;                  // 广告进行中
    private int _pendingItemId;            // 正在广告中的条目 id
    private float _cooldownUntil;          // 点击冷却截止时间
    private float _adWatchdogUntil;        // 广告播放看门狗截止时间

    public IReadOnlyList<SpecialItem> Pool => _pool.AsReadOnly();
    public SpecialPending Pending => _pending;
    public bool AdBusy => _adBusy;
    public int TriggeredInGame => _triggeredInGame;
    /// <summary>本局首次免广告是否已使用</summary>
    public bool FreeUsedInGame => _freeUsedInGame;
    public bool AdConfigAvailable { get; internal set; } = true;   // 由 GameManager 按广告位配置设置

    // ---------- 事件（GameManager 订阅后转发 UI） ----------
    public event Action OnPoolChanged;                       // 池内容变化（刷新入口/弹窗）
    public event Action<SpecialPending> OnPendingCreated;    // 新闪烁机会出现
    public event Action OnPendingBanked;                     // 闪烁机会入池/兑现完成（按钮消失）
    public event Action<long> OnRewardGranted;               // 特技奖励发放（参数：奖励分数）
    public event Action<string> OnFloatText;                 // 飘字请求
    public event Action<string> OnTrackEvent;                // 埋点（key, extra 拼好）

    public SpecialSystem(Game2048 game)
    {
        _game = game;
        LoadPool();
    }

    // ---------- 埋点 ----------
    private void Track(string evt, string extra = "")
    {
        Debug.Log("[SpecialTrack] " + evt + (string.IsNullOrEmpty(extra) ? "" : " " + extra));
        OnTrackEvent?.Invoke(evt + (string.IsNullOrEmpty(extra) ? "" : " " + extra));
    }

    // ---------- 持久化 ----------
    private void LoadPool()
    {
        try
        {
            string raw = TikTokBridge.GetString(SaveKey, "");
            if (string.IsNullOrEmpty(raw)) return;
            // 格式：id|ts,id|ts,...（value 固定 512）
            string[] parts = raw.Split(',');
            int maxId = 0;
            foreach (string p in parts)
            {
                if (string.IsNullOrEmpty(p)) continue;
                string[] kv = p.Split('|');
                if (kv.Length < 2) continue;
                if (!int.TryParse(kv[0], out int id)) continue;
                if (!long.TryParse(kv[1], out long ts)) continue;
                if (_pool.Count >= SpecialCap) break;
                _pool.Add(new SpecialItem { id = id, value = SpecialTriggerValue, ts = ts });
                if (id > maxId) maxId = id;
            }
            _idSeq = maxId + 1;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[SpecialSystem] LoadPool failed: " + e.Message);
            _pool.Clear();
        }
    }

    private void SavePool()
    {
        try
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < _pool.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(_pool[i].id).Append('|').Append(_pool[i].ts);
            }
            TikTokBridge.SetString(SaveKey, sb.ToString());
        }
        catch (Exception e)
        {
            Debug.LogWarning("[SpecialSystem] SavePool failed: " + e.Message);
        }
    }

    // ---------- 新局重置 ----------
    public void OnNewGame()
    {
        // 未入池的闪烁机会先入池，数据不丢失
        if (_pending != null) BankPending(false);
        _triggeredInGame = 0;
        _freeUsedInGame = false;
        _adBusy = false;
        _pendingItemId = 0;
    }

    // ---------- 触发与入池 ----------
    /// <summary>一次合并新生成 512 时调用</summary>
    public void TrySpawn()
    {
        if (_triggeredInGame >= SpecialMaxPerGame)
        {
            Track("trigger_blocked", "reason=game_cap");
            return;
        }
        if (_pool.Count + (_pending != null ? 1 : 0) >= SpecialCap)
        {
            Track("trigger_blocked", "reason=pool_full");
            return;
        }

        bool replacing = _pending != null;
        if (replacing)
        {
            Track("replace");
            BankPending(false);   // 旧机会立即入池，数据不丢失
        }

        _triggeredInGame++;

        // 只在空格子里出现，绝不覆盖已有数字
        var cells = _game.GetEmptyCells();
        if (cells.Count == 0)
        {
            // 满盘时没有安全位置，直接入池；池也满时按奖励兑现，机会不丢失
            if (!PushPoolItem(false))
            {
                GrantOverflowReward();
            }
            Track("trigger", "placed=pool_no_space index=" + _triggeredInGame);
            return;
        }

        var cell = cells[UnityEngine.Random.Range(0, cells.Count)];
        _pending = new SpecialPending(cell.x, cell.y, Time.unscaledTime);
        Track("trigger", "index=" + _triggeredInGame + " gx=" + cell.x + " gy=" + cell.y);
        OnPendingCreated?.Invoke(_pending);
    }

    /// <summary>闪烁机会入池（含飞行动画，由 UI 表现）；返回是否成功</summary>
    public bool PushPoolItem(bool fromClick)
    {
        if (_pool.Count >= SpecialCap) return false;
        _pool.Add(new SpecialItem { id = _idSeq++, value = SpecialTriggerValue, ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });
        SavePool();
        OnPoolChanged?.Invoke();
        if (fromClick)
            OnFloatText?.Invoke("Boost banked!");
        return true;
    }

    public void BankPending(bool fromClick)
    {
        if (_pending == null) return;
        _pending = null;
        if (!PushPoolItem(fromClick))
            GrantOverflowReward();   // 池满兜底：机会不丢弃，直接按特技奖励兑现分数
        OnPendingBanked?.Invoke();
    }

    private void GrantOverflowReward()
    {
        _game.AddScore(SpecialReward);
        OnFloatText?.Invoke("Pool full - auto-cashed +2048!");
        Track("bank_overflow_direct_reward", "score=" + _game.Score);
    }

    // ---------- 每帧更新（由 GameManager.Update 调用） ----------
    public void Update()
    {
        // 广告播放看门狗：SDK 长时间不回调时释放锁
        if (_adBusy && _adWatchdogUntil > 0 && Time.unscaledTime >= _adWatchdogUntil)
        {
            FailAd("Ad not finished. No reward.");
            return;
        }

        if (_pending == null) return;

        // 该格子被方块占用时，自动换到其他空格，保证任何数字都不会被遮挡
        if (_game.HasTileAt(_pending.gx, _pending.gy))
        {
            var cells = _game.GetEmptyCells();
            if (cells.Count > 0)
            {
                var cell = cells[UnityEngine.Random.Range(0, cells.Count)];
                _pending.gx = cell.x;
                _pending.gy = cell.y;
                Track("relocate", "gx=" + _pending.gx + " gy=" + _pending.gy);
                OnPendingCreated?.Invoke(_pending);   // UI 跟随新位置
            }
            else
            {
                Track("bank_no_space");
                BankPending(false);
                return;
            }
        }

        if (_pending != null &&
            Time.unscaledTime - _pending.spawnAt >= (SpecialFloatMs + SpecialFadeMs) / 1000f)
        {
            Track("expire");
            BankPending(false);
        }
    }

    // ---------- 特技奖励 / 广告闭环 ----------
    /// <summary>点击特技池条目；返回 true 表示已经处理（免广告发放或拉起广告）</summary>
    public bool TryUseItem(SpecialItem item)
    {
        if (item == null) return false;
        if (_adBusy) return false;
        if (!_pool.Contains(item)) return false;
        if (Time.unscaledTime < _cooldownUntil) return false;

        if (DebugSkipAd)
        {
            Track("debug_skip_ad", "scene=special");
            OnFloatText?.Invoke("Debug: ad skipped");
            GrantReward(item.id, false);
            return true;
        }

        // 每局第一次特技免广告，直接发放，用于让玩家体验效果
        if (!_freeUsedInGame)
        {
            _freeUsedInGame = true;
            Track("free_use", "itemId=" + item.id);
            GrantReward(item.id, true);
            return true;
        }

        // 广告位未配置直接降级提示
        if (!AdConfigAvailable)
        {
            OnFloatText?.Invoke("Ad unavailable, try again later");
            _cooldownUntil = Time.unscaledTime + SpecialAdCooldown;
            return true;
        }

        _adBusy = true;
        _pendingItemId = item.id;
        _cooldownUntil = Time.unscaledTime + SpecialAdCooldown;
        Track("ad_request", "itemId=" + item.id);
        return true;   // 广告展示由 GameManager 路由（TikTokBridge.ShowRewardedAd）
    }

    /// <summary>广告展示完成（GameManager 回调）；watched = 是否完整观看</summary>
    public void FinishAd(bool watched)
    {
        _adBusy = false;
        _adWatchdogUntil = 0;
        int itemId = _pendingItemId;
        _pendingItemId = 0;
        _cooldownUntil = Time.unscaledTime + SpecialAdCooldown;

        if (!watched)
        {
            Track("ad_incomplete");
            OnFloatText?.Invoke("Watch ad fully to claim");   // 条目保留、库存不变，可再次点击
            return;
        }
        Track("ad_complete");
        GrantReward(itemId, false);
    }

    /// <summary>广告失败（加载失败/播放失败/超时），释放锁</summary>
    public void FailAd(string msg)
    {
        _adBusy = false;
        _adWatchdogUntil = 0;
        _pendingItemId = 0;
        _cooldownUntil = Time.unscaledTime + SpecialAdCooldown;
        Track("ad_fail", "msg=" + msg);
        if (!string.IsNullOrEmpty(msg))
            OnFloatText?.Invoke(msg);
    }

    /// <summary>开始播放广告时设置看门狗（由 GameManager 在展示前调用）</summary>
    public void ArmAdWatchdog()
    {
        _adWatchdogUntil = Time.unscaledTime + SpecialAdLoadTimeout;
    }

    /// <summary>发放特技奖励：+2048，移除条目</summary>
    private void GrantReward(int itemId, bool isFree)
    {
        int idx = _pool.FindIndex(it => it.id == itemId);
        if (idx == -1) return;

        _pool.RemoveAt(idx);
        SavePool();
        OnPoolChanged?.Invoke();

        _game.AddScore(SpecialReward);
        OnRewardGranted?.Invoke(SpecialReward);
        Track("reward", "score=" + _game.Score + " remain=" + _pool.Count + " free=" + isFree);
        OnFloatText?.Invoke(isFree ? "+2048 free boost!" : "+2048 boost!");
    }
}
