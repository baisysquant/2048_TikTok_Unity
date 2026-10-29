using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField] private float _tileAnimationDuration = 0.12f;
    [SerializeField] private float _spawnAnimationDuration = 0.2f;
    [SerializeField] private Color _boardBackgroundColor = new Color(0.96f, 0.94f, 0.90f);
    [SerializeField] private int _boardSize = 4;

    public Game2048 Game { get; private set; }
    public SpecialSystem Special { get; private set; }
    public float TileAnimationDuration => _tileAnimationDuration;
    public float SpawnAnimationDuration => _spawnAnimationDuration;

    /// <summary>调测开关：true 时复活/特技/回撤跳过激励视频直接发放（上线前改 false）</summary>
    public bool DebugSkipAd = false;

    private const string DifficultySaveKey = "fk2048_difficulty";

    private BoardRenderer _boardRenderer;
    private InputHandler _inputHandler;
    private ScoreDisplay _scoreDisplay;
    private AudioManager _audioManager;

    private int _currentGridSize = Game2048.DefaultGridSize;

    /// <summary>有全屏弹窗或广告进行中时，棋盘不响应滑动</summary>
    public bool IsInputBlocked
    {
        get
        {
            if (_scoreDisplay != null && _scoreDisplay.IsAnyOverlayOpen) return true;
            if (Special != null && Special.AdBusy) return true;
            return false;
        }
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        Game = new Game2048();
        Special = new SpecialSystem(Game) { DebugSkipAd = DebugSkipAd };
        _boardRenderer = GetComponent<BoardRenderer>();
        _inputHandler = GetComponent<InputHandler>();
        _scoreDisplay = GetComponent<ScoreDisplay>();

        if (_boardRenderer == null)
            _boardRenderer = gameObject.AddComponent<BoardRenderer>();
        if (_inputHandler == null)
            _inputHandler = gameObject.AddComponent<InputHandler>();
        if (_scoreDisplay == null)
            _scoreDisplay = gameObject.AddComponent<ScoreDisplay>();
        if (_audioManager == null)
            _audioManager = gameObject.AddComponent<AudioManager>();
    }

    private void Start()
    {
        TikTokBridge.Initialize(() =>
        {
            Debug.Log("[GameManager] TikTok SDK initialized");
            TikTokBridge.SilentLogin(code =>
            {
                if (!string.IsNullOrEmpty(code))
                    Debug.Log("[GameManager] Silent login success");
                else
                    Debug.Log("[GameManager] Silent login failed or unavailable");
            });
        });

        // 广告位未配置时特技/回撤广告入口降级提示
        Special.AdConfigAvailable = TikTokBridge.IsRewardedAdConfigured();

        if (Camera.main == null)
        {
            var camGO = new GameObject("MainCamera", typeof(Camera), typeof(AudioListener));
            camGO.tag = "MainCamera";
            var cam = camGO.GetComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.96f, 0.94f, 0.90f);
        }

        var canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
        }

        var eventSystem = FindObjectOfType<UnityEngine.EventSystems.EventSystem>();
        if (eventSystem == null)
        {
            var esGO = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));
        }

        _boardRenderer.Initialize(canvas);
        _inputHandler.Initialize();
        _scoreDisplay.Initialize(canvas, _boardRenderer.BoardContainer);
        _scoreDisplay.SetCallbacks(OnNewGameClicked, OnUndoClicked, OnPoolClicked, OnSpecialItemUse, OnDifficultySelected);
        _scoreDisplay.BindPendingButton(OnPendingClicked);

        // 事件绑定
        Game.OnStateChanged += OnGameStateChanged;
        Game.OnScoreChanged += OnScoreChanged;
        Game.OnGameOverChanged += OnGameOverChanged;
        Game.OnWonChanged += OnWonChanged;
        Game.OnMoveExecuted += OnMoveExecuted;
        Game.OnTileSpawned += OnTileSpawned;
        Game.OnTileMerged += OnTileMerged;
        Game.OnSpecialTrigger += OnSpecialTriggered;

        Special.OnPendingCreated += OnPendingCreated;
        Special.OnPendingBanked += OnPendingBanked;
        Special.OnPoolChanged += OnSpecialPoolChanged;
        Special.OnFloatText += OnFloatText;
        Special.OnRewardGranted += OnRewardGranted;

        // 启动流程：先弹难度选择，选中后开局
        int last = LoadLastDifficulty();
        if (last != 4 && last != 5) last = Game2048.DefaultGridSize;
        _currentGridSize = last;
        _scoreDisplay.UpdateSpecialPoolBar(Special.Pool.Count, false);
        _scoreDisplay.UpdateUndoButton(false, Game2048.UndoFreeCount);
        _scoreDisplay.ShowDifficultyDialog(last);
    }

    private void Update()
    {
        if (Game == null || Special == null) return;

        // 最高分去抖落盘
        Game.TickBestFlush();

        // 特技系统更新（闪烁按钮生命周期）
        Special.Update();

        // 闪烁按钮 UI 每帧跟随
        if (_scoreDisplay != null)
            _scoreDisplay.UpdatePendingButton(Special.Pending, _boardRenderer);
    }

    // ========== 难度系统 ==========

    private int LoadLastDifficulty()
    {
        string raw = TikTokBridge.GetString(DifficultySaveKey, "");
        int.TryParse(raw, out int n);
        return (n == 4 || n == 5) ? n : Game2048.DefaultGridSize;
    }

    private void OnDifficultySelected(int size)
    {
        _currentGridSize = size;
        try
        {
            TikTokBridge.SetString(DifficultySaveKey, size.ToString());
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[GameManager] Save difficulty failed: " + e.Message);
        }

        _scoreDisplay.HideDifficultyDialog();
        _audioManager?.PlayClick();

        _boardRenderer.SetGridSize(size);
        _boardRenderer.ClearBoard();
        Special.OnNewGame();   // 重置本局特技触发次数与免广告额度
        Game.StartNewGame(size);
        _boardRenderer.RefreshBoard(Game.Tiles, false);
        _scoreDisplay.UpdateScore(Game.Score, Game.BestScore, Game.MoveCount);
        _scoreDisplay.UpdateUndoButton(Game.CanUndo, Game.UndoFreeLeft);
        _scoreDisplay.UpdateSpecialPoolBar(Special.Pool.Count);
    }

    // ========== 游戏事件 ==========

    private void OnGameStateChanged()
    {
        _boardRenderer.RefreshBoard(Game.Tiles, true);
        _scoreDisplay.UpdateUndoButton(Game.CanUndo, Game.UndoFreeLeft);
    }

    private void OnScoreChanged(long score)
    {
        _scoreDisplay.UpdateScore(score, Game.BestScore, Game.MoveCount);
    }

    private void OnGameOverChanged(bool isOver)
    {
        if (isOver)
        {
            Game.FlushBestScore();
            _audioManager?.PlayGameOver();
            _scoreDisplay.ShowGameOver(Game.Score, OnRestartClicked, Game.CanRevive, OnReviveClicked);
            TikTokBridge.AddHomeScreenShortcut(success =>
            {
                if (success) Debug.Log("[GameManager] Home shortcut added");
            });
        }
    }

    private void OnWonChanged(bool isWon)
    {
        if (isWon && !Game.KeepPlaying)
        {
            _audioManager?.PlayWin();
            _scoreDisplay.ShowWin(OnContinueClicked, OnRestartClicked);
        }
    }

    private void OnMoveExecuted(MoveDirection direction)
    {
        _audioManager?.PlayMove();
        _scoreDisplay.UpdateScore(Game.Score, Game.BestScore, Game.MoveCount);
    }

    private void OnTileSpawned(int value)
    {
        _audioManager?.PlaySpawn();
    }

    private void OnTileMerged(int value)
    {
        _audioManager?.PlayMerge(value);
        if (value >= 256)
            _scoreDisplay?.PlayConfetti(35);
    }

    // ========== 特技加速 ==========

    private void OnSpecialTriggered()
    {
        // 合成 512 触发特技机会（音效三连音 + SpecialSystem 分配位置/入池）
        _audioManager?.PlaySpecial();
        Special.TrySpawn();
    }

    private void OnPendingCreated(SpecialSystem.SpecialPending pending)
    {
        // 闪烁按钮 UI 每帧由 Update() 驱动；这里只做日志
        Debug.Log($"[SpecialSystem] Pending boost at ({pending.gx},{pending.gy})");
    }

    private void OnPendingBanked()
    {
        _audioManager?.PlayBank();
        _scoreDisplay.UpdateSpecialPoolBar(Special.Pool.Count);
        RefreshSpecialPanelIfOpen();
    }

    private void OnSpecialPoolChanged()
    {
        _scoreDisplay.UpdateSpecialPoolBar(Special.Pool.Count);
        RefreshSpecialPanelIfOpen();
    }

    private void OnFloatText(string text)
    {
        _scoreDisplay.ShowFloatText(text);
    }

    private void OnRewardGranted(long reward)
    {
        _audioManager?.PlayReward();
    }

    private void RefreshSpecialPanelIfOpen()
    {
        if (_scoreDisplay.IsSpecialPanelOpen)
            _scoreDisplay.RefreshSpecialPanel(Special.Pool, !Special.FreeUsedInGame);
    }

    private void OnPoolClicked()
    {
        if (_scoreDisplay.IsSpecialPanelOpen)
        {
            _scoreDisplay.CloseSpecialPanel();
            return;
        }
        _audioManager?.PlayClick();
        _scoreDisplay.ShowSpecialPanel(Special.Pool, Special.FreeUsedInGame == false);
    }

    private void OnPendingClicked()
    {
        // 点击棋盘闪烁按钮：立即入池
        if (Special.Pending == null) return;
        Special.BankPending(true);
        TrackSpecial("float_click");
    }

    private void OnSpecialItemUse(SpecialSystem.SpecialItem item)
    {
        if (Special.TryUseItem(item))
        {
            // 需要看广告（非免费、非调测）时由 AdBusy 状态进入广告路由
            if (Special.AdBusy)
                PlaySpecialAd();
        }
    }

    private void TrackSpecial(string evt, string extra = "")
    {
        Debug.Log("[SpecialTrack] " + evt + (string.IsNullOrEmpty(extra) ? "" : " " + extra));
    }

    // ========== 后悔回撤（免费 5 次/局，之后看广告） ==========

    private void OnUndoClicked()
    {
        // 广告进行中 / 特技弹窗打开时不可用
        if (Special.AdBusy) return;
        if (_scoreDisplay != null && _scoreDisplay.IsAnyOverlayOpen &&
            !_scoreDisplay.IsGameOverPanelOpen) return;

        if (!Game.CanUndo)
        {
            ShowFloat("没有可回撤的操作");
            return;
        }

        // 每盘局 5 次免费，用完后需观看广告
        if (Game.UndoFreeLeft > 0)
        {
            Game.ConsumeUndoFree();
            int remain = Game.UndoFreeLeft;
            DoUndo(remain > 0 ? $"免费剩 {remain} 次" : "免费次数已用完");
            return;
        }

        if (DebugSkipAd)
        {
            TrackSpecial("debug_skip_ad", "scene=undo");
            DoUndo();
            return;
        }

        ShowFloat("完整观看广告才能回撤");
        PlayUndoAd();
    }

    private void PlayUndoAd()
    {
        if (!TikTokBridge.IsRewardedAdConfigured())
        {
            ShowFloat("广告加载失败，无法回退");
            return;
        }
        TikTokBridge.ShowRewardedAd(rewarded =>
        {
            if (rewarded)
            {
                ShowFloat("广告观看完成，给予回撤奖励");
                DoUndo();
            }
            else
            {
                TrackSpecial("undo_ad_incomplete");
                ShowFloat("完整观看广告才能回撤");
                _scoreDisplay.UpdateUndoButton(Game.CanUndo, Game.UndoFreeLeft);
            }
        });
    }

    private void DoUndo()
    {
        int steps = Game.Undo();
        if (steps <= 0) return;
        _audioManager?.PlayUndo();
        _scoreDisplay.HideOverlays();   // 失败弹窗悔棋救场：回撤成功后关闭弹窗
        _boardRenderer.ClearBoard();
        _boardRenderer.RefreshBoard(Game.Tiles, false);
        _scoreDisplay.UpdateScore(Game.Score, Game.BestScore, Game.MoveCount);
        _scoreDisplay.UpdateUndoButton(Game.CanUndo, Game.UndoFreeLeft);
        ShowFloat($"↩ 已回撤 {steps} 步");
    }

    // ========== 特技广告路由 ==========

    private void PlaySpecialAd()
    {
        if (!TikTokBridge.IsRewardedAdConfigured())
        {
            Special.FailAd("广告暂不可用，请稍后重试");
            return;
        }
        Special.ArmAdWatchdog();
        TikTokBridge.ShowRewardedAd(rewarded =>
        {
            Special.FinishAd(rewarded);
        });
    }

    // ========== 复活（对齐微信版：移除 2 个最小方块继续当前局） ==========

    private void OnReviveClicked()
    {
        if (DebugSkipAd)
        {
            TrackSpecial("debug_skip_ad", "scene=revive");
            ShowFloat("调测模式：跳过广告");
            DoRevive();
            return;
        }
        if (!TikTokBridge.IsRewardedAdConfigured())
        {
            ShowFloat("广告暂不可用");
            return;
        }
        TikTokBridge.ShowRewardedAd(rewarded =>
        {
            if (!rewarded) return;
            DoRevive();
        },
        () => ShowFloat("广告暂不可用，请稍后重试"));
    }

    private void DoRevive()
    {
        Game.Revive();
        _audioManager?.PlayReward();
        _scoreDisplay.HideOverlays();
        _boardRenderer.ClearBoard();
        _boardRenderer.RefreshBoard(Game.Tiles, true);
        _scoreDisplay.UpdateScore(Game.Score, Game.BestScore, Game.MoveCount);
        _scoreDisplay.UpdateUndoButton(Game.CanUndo, Game.UndoFreeLeft);
        TrackSpecial("revive");
    }

    // ========== 按钮回调 ==========

    public void OnMove(MoveDirection direction)
    {
        if (IsInputBlocked) return;
        Game.Move(direction);
    }

    private void OnRestartClicked()
    {
        _scoreDisplay.HideOverlays();
        StartSameSizeGame();
    }

    private void OnNewGameClicked()
    {
        _scoreDisplay.HideOverlays();
        StartSameSizeGame();
    }

    private void StartSameSizeGame()
    {
        _boardRenderer.SetGridSize(_currentGridSize);
        _boardRenderer.ClearBoard();
        Special.OnNewGame();
        Game.StartNewGame(_currentGridSize);
        _boardRenderer.RefreshBoard(Game.Tiles, false);
        _scoreDisplay.UpdateScore(Game.Score, Game.BestScore, Game.MoveCount);
        _scoreDisplay.UpdateUndoButton(Game.CanUndo, Game.UndoFreeLeft);
        _scoreDisplay.UpdateSpecialPoolBar(Special.Pool.Count);
    }

    private void OnContinueClicked()
    {
        _scoreDisplay.HideOverlays();
        Game.ContinueAfterWin();
    }

    private void ShowFloat(string text)
    {
        _scoreDisplay.ShowFloatText(text);
    }
}
