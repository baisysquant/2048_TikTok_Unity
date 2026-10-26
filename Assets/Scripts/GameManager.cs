using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField] private float _tileAnimationDuration = 0.12f;
    [SerializeField] private float _spawnAnimationDuration = 0.2f;
    [SerializeField] private Color _boardBackgroundColor = new Color(0.96f, 0.94f, 0.90f);
    [SerializeField] private int _boardSize = 4;

    public Game2048 Game { get; private set; }
    public float TileAnimationDuration => _tileAnimationDuration;
    public float SpawnAnimationDuration => _spawnAnimationDuration;

    private BoardRenderer _boardRenderer;
    private InputHandler _inputHandler;
    private ScoreDisplay _scoreDisplay;
    private AudioManager _audioManager;

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
        _scoreDisplay.SetCallbacks(OnNewGameClicked, OnUndoClicked);

        Game.StartNewGame();

        Game.OnStateChanged += OnGameStateChanged;
        Game.OnScoreChanged += OnScoreChanged;
        Game.OnGameOverChanged += OnGameOverChanged;
        Game.OnWonChanged += OnWonChanged;
        Game.OnMoveExecuted += OnMoveExecuted;
        Game.OnTileSpawned += OnTileSpawned;
        Game.OnTileMerged += OnTileMerged;

        _boardRenderer.RefreshBoard(Game.Tiles, false);
        _scoreDisplay.UpdateScore(Game.Score, Game.BestScore, Game.MoveCount);
    }

    private void OnGameStateChanged()
    {
        _boardRenderer.RefreshBoard(Game.Tiles, true);
    }

    private void OnScoreChanged(int score)
    {
        _scoreDisplay.UpdateScore(score, Game.BestScore, Game.MoveCount);
    }

    private void OnGameOverChanged(bool isOver)
    {
        if (isOver)
        {
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

    public void OnMove(MoveDirection direction)
    {
        Game.Move(direction);
    }

    private void OnRestartClicked()
    {
        _scoreDisplay.HideOverlays();
        _boardRenderer.ClearBoard();
        Game.StartNewGame();
        _boardRenderer.RefreshBoard(Game.Tiles, false);
        _scoreDisplay.UpdateScore(Game.Score, Game.BestScore, Game.MoveCount);
    }

    private void OnNewGameClicked()
    {
        _boardRenderer.ClearBoard();
        Game.StartNewGame();
        _boardRenderer.RefreshBoard(Game.Tiles, false);
        _scoreDisplay.UpdateScore(Game.Score, Game.BestScore, Game.MoveCount);
    }

    private void OnUndoClicked()
    {
        if (Game.CanUndo)
        {
            Game.Undo();
            _boardRenderer.ClearBoard();
            _boardRenderer.RefreshBoard(Game.Tiles, false);
            _scoreDisplay.UpdateScore(Game.Score, Game.BestScore, Game.MoveCount);
        }
    }

    private void OnContinueClicked()
    {
        _scoreDisplay.HideOverlays();
        Game.ContinueAfterWin();
    }

    private void OnReviveClicked()
    {
        TikTokBridge.ShowRewardedAd(
            rewarded =>
            {
                if (!rewarded) return;
                Game.Revive();
                _scoreDisplay.HideOverlays();
            },
            () =>
            {
                Debug.Log("[GameManager] Rewarded ad unavailable");
            });
    }
}