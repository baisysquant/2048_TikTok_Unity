using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ScoreDisplay : MonoBehaviour
{
    private Canvas _canvas;

    private TMPro.TextMeshProUGUI _scoreText;
    private TMPro.TextMeshProUGUI _bestScoreText;
    private TMPro.TextMeshProUGUI _moveCountText;
    private TMPro.TextMeshProUGUI _gameOverScoreText;
    private GameObject _gameOverPanel;
    private GameObject _winPanel;
    private ConfettiEffect _confetti;

    public void Initialize(Canvas canvas, RectTransform boardContainer)
    {
        _canvas = canvas;

        var confGO = new GameObject("Confetti", typeof(ConfettiEffect));
        confGO.transform.SetParent(_canvas.transform, false);
        _confetti = confGO.GetComponent<ConfettiEffect>();
        _confetti.Initialize(canvas);

        CreateScoreUI(boardContainer);
        CreateGameOverOverlay();
        CreateWinOverlay();
        StartCoroutine(BlinkRoutine());
    }

    private void CreateScoreUI(RectTransform boardContainer)
    {
        float boardWidth = boardContainer.sizeDelta.x;
        float boardHeight = boardContainer.sizeDelta.y;
        float boardTop = boardContainer.anchoredPosition.y + boardHeight / 2f;
        float boardBottom = boardContainer.anchoredPosition.y - boardHeight / 2f;

        var topContainer = new GameObject("TopContainer", typeof(RectTransform), typeof(CanvasGroup));
        var topRT = topContainer.GetComponent<RectTransform>();
        topRT.SetParent(_canvas.transform, false);
        topRT.anchorMin = new Vector2(0.5f, 0.5f);
        topRT.anchorMax = new Vector2(0.5f, 0.5f);
        topRT.sizeDelta = new Vector2(boardWidth, 260);
        topRT.anchoredPosition = new Vector2(0, boardTop + 120);
        topContainer.GetComponent<CanvasGroup>().blocksRaycasts = false;

        var raccoon = CreateRaccoonTitle(topRT, boardWidth);
        raccoon.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -8);

        _scoreText = CreateScoreLabel(topRT, "Score", 0, "0");
        _bestScoreText = CreateScoreLabel(topRT, "Best", 1, "0");
        _moveCountText = CreateScoreLabel(topRT, "Moves", 2, "0");

        var bottomContainer = new GameObject("BottomContainer", typeof(RectTransform));
        var bottomRT = bottomContainer.GetComponent<RectTransform>();
        bottomRT.SetParent(_canvas.transform, false);
        bottomRT.anchorMin = new Vector2(0.5f, 0.5f);
        bottomRT.anchorMax = new Vector2(0.5f, 0.5f);
        bottomRT.sizeDelta = new Vector2(boardWidth, 80);
        bottomRT.anchoredPosition = new Vector2(0, boardBottom - 135);

        float btnWidth = (boardWidth - 20) / 3f;
        float startX = -boardWidth / 2 + btnWidth / 2;

        var restartBtn = CreateActionButton(bottomRT, "RestartBtn", "New", new Vector2(startX, 0));
        CreateMusicToggle(bottomRT, new Vector2(startX + btnWidth + 10, 0));
        var undoBtn = CreateActionButton(bottomRT, "UndoBtn", "Undo", new Vector2(startX + (btnWidth + 10) * 2, 0));

        restartBtn.GetComponent<Button>().onClick.AddListener(() => _onNewGame?.Invoke());
        undoBtn.GetComponent<Button>().onClick.AddListener(() => _onUndo?.Invoke());
    }

    private void CreateMusicToggle(RectTransform parent, Vector2 position)
    {
        float containerWidth = parent.sizeDelta.x;
        float btnWidth = (containerWidth - 20) / 3f;

        var go = new GameObject("MusicToggle", typeof(Image), typeof(Button));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0.5f, 1);
        rt.anchorMax = new Vector2(0.5f, 1);
        rt.sizeDelta = new Vector2(btnWidth, 101);
        rt.anchoredPosition = position;

        var img = go.GetComponent<Image>();
        img.color = new Color(0.71f, 0.65f, 0.60f);
        img.sprite = CreateRoundedRectSprite(btnWidth, 101, 16);
        img.raycastTarget = true;

        var textGO = new GameObject("Icon", typeof(TMPro.TextMeshProUGUI));
        var textRT = textGO.GetComponent<RectTransform>();
        textRT.SetParent(rt, false);
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.sizeDelta = Vector2.zero;
        var tmp = textGO.GetComponent<TMPro.TextMeshProUGUI>();
        tmp.text = "\u266B";
        tmp.fontSize = 30;
        tmp.fontStyle = TMPro.FontStyles.Bold;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.color = new Color(0.96f, 0.96f, 0.94f);

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => {
            var am = FindObjectOfType<AudioManager>();
            if (am != null)
            {
                am.ToggleSound();
                tmp.text = am.IsSoundEnabled ? "\u266B" : "\u266B\u0338";
            }
        });
    }

    private RectTransform _eyeL;
    private RectTransform _eyeR;

    private static readonly Color FurColor = new Color(0.87f, 0.82f, 0.77f);
    private static readonly Color FurDark = new Color(0.76f, 0.68f, 0.62f);
    private static readonly Color BellyColor = new Color(0.94f, 0.91f, 0.88f);
    private static readonly Color EyeColor = new Color(0.23f, 0.20f, 0.19f);
    private static readonly Color NoseColor = new Color(0.23f, 0.20f, 0.19f);
    private static readonly Color TitleTextColor = new Color(0.47f, 0.43f, 0.40f);

    private static readonly Dictionary<string, Sprite> _spriteCache = new Dictionary<string, Sprite>();

    private GameObject CreateRaccoonTitle(RectTransform parent, float boardWidth)
    {
        float raccoonW = boardWidth * 0.67f;
        float raccoonH = raccoonW * 0.27f;
        float scale = raccoonW / 260f;

        var root = new GameObject("Raccoon", typeof(RectTransform));
        var rootRT = root.GetComponent<RectTransform>();
        rootRT.SetParent(parent, false);
        rootRT.anchorMin = new Vector2(0.5f, 1);
        rootRT.anchorMax = new Vector2(0.5f, 1);
        rootRT.sizeDelta = new Vector2(raccoonW, raccoonH);

        var tail = CreateOval(rootRT, "Tail", 70 * scale, 28 * scale, FurDark, new Vector2(-raccoonW * 0.46f, -raccoonH * 0.1f));
        tail.localRotation = Quaternion.Euler(0, 0, 12);
        CreateOval(tail, "Stripe1", 14 * scale, 28 * scale, FurColor, new Vector2(-12 * scale, 0));
        CreateOval(tail, "Stripe2", 14 * scale, 28 * scale, FurColor, new Vector2(6 * scale, 0));

        CreateOval(rootRT, "Body", 180 * scale, 60 * scale, FurColor, new Vector2(-10 * scale, -raccoonH * 0.07f));

        var head = CreateOval(rootRT, "Head", 62 * scale, 58 * scale, FurColor, new Vector2(raccoonW * 0.31f, 0));

        CreateOval(head, "EarL", 18 * scale, 20 * scale, FurDark, new Vector2(-12 * scale, 26 * scale));
        CreateOval(head, "EarR", 18 * scale, 20 * scale, FurDark, new Vector2(12 * scale, 26 * scale));
        CreateOval(head, "EarInnerL", 10 * scale, 12 * scale, FurColor, new Vector2(-12 * scale, 26 * scale));
        CreateOval(head, "EarInnerR", 10 * scale, 12 * scale, FurColor, new Vector2(12 * scale, 26 * scale));

        CreateOval(head, "FaceMask", 44 * scale, 30 * scale, BellyColor, new Vector2(0, -6 * scale));

        _eyeL = CreateOval(head, "EyeL", 10 * scale, 11 * scale, EyeColor, new Vector2(-11 * scale, 6 * scale));
        _eyeR = CreateOval(head, "EyeR", 10 * scale, 11 * scale, EyeColor, new Vector2(11 * scale, 6 * scale));
        CreateOval(_eyeL, "ShineL", 4 * scale, 4 * scale, Color.white, new Vector2(2 * scale, 2 * scale));
        CreateOval(_eyeR, "ShineR", 4 * scale, 4 * scale, Color.white, new Vector2(2 * scale, 2 * scale));

        CreateOval(head, "Nose", 10 * scale, 7 * scale, NoseColor, new Vector2(0, -6 * scale));

        CreateOval(rootRT, "PawL", 22 * scale, 12 * scale, FurDark, new Vector2(-50 * scale, -raccoonH * 0.46f));
        CreateOval(rootRT, "PawR", 22 * scale, 12 * scale, FurDark, new Vector2(30 * scale, -raccoonH * 0.46f));

        var titleGO = new GameObject("Title", typeof(TMPro.TextMeshProUGUI));
        var titleRT = titleGO.GetComponent<RectTransform>();
        titleRT.SetParent(rootRT, false);
        titleRT.anchorMin = new Vector2(0.5f, 0.5f);
        titleRT.anchorMax = new Vector2(0.5f, 0.5f);
        titleRT.sizeDelta = new Vector2(120 * scale, 30 * scale);
        titleRT.anchoredPosition = new Vector2(-20 * scale, 0);
        var titleTMP = titleGO.GetComponent<TMPro.TextMeshProUGUI>();
        titleTMP.text = "<color=#B5A69A>2</color><color=#B5A69A>0</color><color=#B5A69A>4</color><color=#B5A69A>8</color>";
        titleTMP.fontSize = 32 * scale;
        titleTMP.fontStyle = TMPro.FontStyles.Bold;
        titleTMP.richText = true;
        titleTMP.color = Color.white;
        titleTMP.alignment = TMPro.TextAlignmentOptions.Center;

        return root;
    }

    private RectTransform CreateOval(RectTransform parent, string name, float w, float h, Color color, Vector2 pos)
    {
        var go = new GameObject(name, typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = pos;
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return rt;
    }

    private System.Collections.IEnumerator BlinkRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(UnityEngine.Random.Range(3f, 5f));
            if (_eyeL == null || _eyeR == null || _eyeL.gameObject == null || _eyeR.gameObject == null)
                yield break;
            float origH = _eyeL.sizeDelta.y;
            _eyeL.sizeDelta = new Vector2(_eyeL.sizeDelta.x, 2);
            _eyeR.sizeDelta = new Vector2(_eyeR.sizeDelta.x, 2);
            yield return new WaitForSeconds(0.08f);
            if (_eyeL == null || _eyeR == null || _eyeL.gameObject == null || _eyeR.gameObject == null)
                yield break;
            _eyeL.sizeDelta = new Vector2(_eyeL.sizeDelta.x, origH);
            _eyeR.sizeDelta = new Vector2(_eyeR.sizeDelta.x, origH);
        }
    }

    private GameObject CreateActionButton(RectTransform parent, string name, string text, Vector2 position)
    {
        float containerWidth = parent.sizeDelta.x;
        float btnWidth = (containerWidth - 20) / 3f;

        var go = new GameObject(name, typeof(Image), typeof(Button));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0.5f, 1);
        rt.anchorMax = new Vector2(0.5f, 1);
        rt.sizeDelta = new Vector2(btnWidth, 101);
        rt.anchoredPosition = position;

        var img = go.GetComponent<Image>();
        img.color = new Color(0.71f, 0.65f, 0.60f);
        img.sprite = CreateRoundedRectSprite(btnWidth, 101, 16);

        var textGO = new GameObject("Text", typeof(TMPro.TextMeshProUGUI));
        var textRT = textGO.GetComponent<RectTransform>();
        textRT.SetParent(rt, false);
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.sizeDelta = Vector2.zero;
        var textTMP = textGO.GetComponent<TMPro.TextMeshProUGUI>();
        textTMP.text = text;
        textTMP.fontSize = 28;
        textTMP.fontStyle = TMPro.FontStyles.Bold;
        textTMP.color = Color.white;
        textTMP.alignment = TMPro.TextAlignmentOptions.Center;

        return go;
    }

    private TMPro.TextMeshProUGUI CreateScoreLabel(RectTransform parent, string label, int index, string value)
    {
        float containerWidth = parent.sizeDelta.x;
        float gap = 10;
        float btnWidth = (containerWidth - gap * 2) / 3f;
        float startX = -containerWidth / 2 + btnWidth / 2;
        float xPos = startX + index * (btnWidth + gap);

        var go = new GameObject(label, typeof(Image), typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0.5f, 1);
        rt.anchorMax = new Vector2(0.5f, 1);
        rt.sizeDelta = new Vector2(btnWidth, 101);
        rt.anchoredPosition = new Vector2(xPos, -155);

        var img = go.GetComponent<Image>();
        img.color = new Color(0.71f, 0.65f, 0.60f);
        img.sprite = CreateRoundedRectSprite(btnWidth, 101, 16);
        img.raycastTarget = false;

        var labelGO = new GameObject("Label", typeof(TMPro.TextMeshProUGUI));
        var labelRT = labelGO.GetComponent<RectTransform>();
        labelRT.SetParent(rt, false);
        labelRT.anchorMin = Vector2.zero;
        labelRT.anchorMax = Vector2.one;
        labelRT.sizeDelta = new Vector2(0, 0);
        labelRT.offsetMin = new Vector2(5, 60);
        labelRT.offsetMax = new Vector2(-5, -4);
        var labelTMP = labelGO.GetComponent<TMPro.TextMeshProUGUI>();
        labelTMP.text = label.ToUpper();
        labelTMP.fontSize = 28;
        labelTMP.fontStyle = TMPro.FontStyles.Bold;
        labelTMP.color = new Color(0.96f, 0.96f, 0.94f);
        labelTMP.alignment = TMPro.TextAlignmentOptions.Center;

        var textGO = new GameObject("Value", typeof(TMPro.TextMeshProUGUI));
        var textRT = textGO.GetComponent<RectTransform>();
        textRT.SetParent(rt, false);
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.sizeDelta = new Vector2(0, 0);
        textRT.offsetMin = new Vector2(5, 4);
        textRT.offsetMax = new Vector2(-5, -55);
        var textTMP = textGO.GetComponent<TMPro.TextMeshProUGUI>();
        textTMP.text = value;
        textTMP.fontSize = 38;
        textTMP.fontStyle = TMPro.FontStyles.Bold;
        textTMP.color = Color.white;
        textTMP.alignment = TMPro.TextAlignmentOptions.Center;

        return textTMP;
    }

    private void CreateGameOverOverlay()
    {
        _gameOverPanel = CreateOverlay("GameOverPanel", true);

        var textGO = new GameObject("GameOverText", typeof(TMPro.TextMeshProUGUI));
        var textRT = textGO.GetComponent<RectTransform>();
        textRT.SetParent(_gameOverPanel.transform, false);
        textRT.anchorMin = new Vector2(0, 0.5f);
        textRT.anchorMax = new Vector2(1, 0.5f);
        textRT.sizeDelta = new Vector2(0, 80);
        textRT.anchoredPosition = new Vector2(0, 158);
        var textTMP = textGO.GetComponent<TMPro.TextMeshProUGUI>();
        textTMP.text = "Game Over!";
        textTMP.fontSize = 48;
        textTMP.fontStyle = TMPro.FontStyles.Bold;
        textTMP.color = new Color(0.47f, 0.43f, 0.40f);
        textTMP.alignment = TMPro.TextAlignmentOptions.Center;

        var scoreGO = new GameObject("FinalScore", typeof(TMPro.TextMeshProUGUI));
        var scoreRT = scoreGO.GetComponent<RectTransform>();
        scoreRT.SetParent(_gameOverPanel.transform, false);
        scoreRT.anchorMin = new Vector2(0, 0.5f);
        scoreRT.anchorMax = new Vector2(1, 0.5f);
        scoreRT.sizeDelta = new Vector2(0, 80);
        scoreRT.anchoredPosition = new Vector2(0, 58);
        _gameOverScoreText = scoreGO.GetComponent<TMPro.TextMeshProUGUI>();
        _gameOverScoreText.fontSize = 56;
        _gameOverScoreText.fontStyle = TMPro.FontStyles.Bold;
        _gameOverScoreText.alignment = TMPro.TextAlignmentOptions.Center;
        _gameOverScoreText.color = new Color(0.47f, 0.43f, 0.40f);

        var reviveBtn = CreateButton(_gameOverPanel.transform, "ReviveBtn", "Revive", new Vector2(0, -57));
        var reviveBtnComponent = reviveBtn.GetComponent<Button>();
        reviveBtnComponent.interactable = false;
        reviveBtn.SetActive(false);

        var restartBtn = CreateButton(_gameOverPanel.transform, "RestartBtn", "Try Again", new Vector2(0, -157));

        _gameOverPanel.SetActive(false);
    }

    private void CreateWinOverlay()
    {
        _winPanel = CreateOverlay("WinPanel", true);
        var textGO = new GameObject("WinText", typeof(TMPro.TextMeshProUGUI));
        var textRT = textGO.GetComponent<RectTransform>();
        textRT.SetParent(_winPanel.transform, false);
        textRT.anchorMin = new Vector2(0, 0.5f);
        textRT.anchorMax = new Vector2(1, 0.5f);
        textRT.sizeDelta = new Vector2(0, 80);
        textRT.anchoredPosition = new Vector2(0, 40);
        var textTMP = textGO.GetComponent<TMPro.TextMeshProUGUI>();
        textTMP.text = "You Win!";
        textTMP.fontSize = 48;
        textTMP.fontStyle = TMPro.FontStyles.Bold;
        textTMP.color = new Color(0.47f, 0.43f, 0.40f);
        textTMP.alignment = TMPro.TextAlignmentOptions.Center;

        var continueBtn = CreateButton(_winPanel.transform, "ContinueBtn", "Continue", new Vector2(0, -20));
        var restartBtn = CreateButton(_winPanel.transform, "RestartBtn", "New Game", new Vector2(0, -70));
        _winPanel.SetActive(false);
    }

    private GameObject CreateOverlay(string name, bool blockRaycasts)
    {
        var go = new GameObject(name, typeof(Image), typeof(CanvasGroup));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(_canvas.transform, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;

        var img = go.GetComponent<Image>();
        img.color = new Color(0.96f, 0.94f, 0.90f, 0.8f);

        var cg = go.GetComponent<CanvasGroup>();
        cg.blocksRaycasts = blockRaycasts;

        go.transform.SetAsLastSibling();
        return go;
    }

    private GameObject CreateButton(Transform parent, string name, string text, Vector2 position)
    {
        var go = new GameObject(name, typeof(Image), typeof(Button));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.sizeDelta = new Vector2(200, 50);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;

        var img = go.GetComponent<Image>();
        img.color = new Color(0.47f, 0.43f, 0.40f);
        img.sprite = CreateRoundedRectSprite(200, 50, 16);

        var textGO = new GameObject("Text", typeof(TMPro.TextMeshProUGUI));
        var textRT = textGO.GetComponent<RectTransform>();
        textRT.SetParent(rt, false);
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.sizeDelta = Vector2.zero;
        var textTMP = textGO.GetComponent<TMPro.TextMeshProUGUI>();
        textTMP.text = text;
        textTMP.fontSize = 22;
        textTMP.fontStyle = TMPro.FontStyles.Bold;
        textTMP.color = Color.white;
        textTMP.alignment = TMPro.TextAlignmentOptions.Center;

        return go;
    }

    private System.Action _restartCallback;
    private System.Action _continueCallback;
    private System.Action _onNewGame;
    private System.Action _onUndo;

    public void SetCallbacks(System.Action onNewGame, System.Action onUndo)
    {
        _onNewGame = onNewGame;
        _onUndo = onUndo;
    }

    public void UpdateScore(int score, int bestScore, int moveCount)
    {
        if (_scoreText != null)
            _scoreText.text = score.ToString();
        if (_bestScoreText != null)
            _bestScoreText.text = bestScore.ToString();
        if (_moveCountText != null)
            _moveCountText.text = moveCount.ToString();
    }

    public void ShowGameOver(int score, System.Action onRestart, bool canRevive, System.Action onRevive)
    {
        _restartCallback = onRestart;
        if (_gameOverPanel != null)
        {
            _gameOverPanel.SetActive(true);
            if (_gameOverScoreText != null)
                _gameOverScoreText.text = $"Score: {score}";

            var btn = _gameOverPanel.transform.Find("RestartBtn")?.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => _restartCallback?.Invoke());
            }

            var reviveBtnGO = _gameOverPanel.transform.Find("ReviveBtn")?.gameObject;
            if (reviveBtnGO != null)
            {
                reviveBtnGO.SetActive(canRevive);
                var reviveBtn = reviveBtnGO.GetComponent<Button>();
                if (reviveBtn != null)
                {
                    reviveBtn.interactable = true;
                    reviveBtn.onClick.RemoveAllListeners();
                    reviveBtn.onClick.AddListener(() => {
                        reviveBtnGO.SetActive(false);
                        onRevive?.Invoke();
                    });
                }
            }
        }
    }

    public void ShowWin(System.Action onContinue, System.Action onRestart)
    {
        _continueCallback = onContinue;
        _restartCallback = onRestart;
        if (_winPanel != null)
        {
            _winPanel.SetActive(true);
            _confetti?.Play(60);

            var continueBtn = _winPanel.transform.Find("ContinueBtn")?.GetComponent<Button>();
            if (continueBtn != null)
            {
                continueBtn.onClick.RemoveAllListeners();
                continueBtn.onClick.AddListener(() => { _confetti?.Stop(); _continueCallback?.Invoke(); });
            }
            var restartBtn = _winPanel.transform.Find("RestartBtn")?.GetComponent<Button>();
            if (restartBtn != null)
            {
                restartBtn.onClick.RemoveAllListeners();
                restartBtn.onClick.AddListener(() => { _confetti?.Stop(); _restartCallback?.Invoke(); });
            }
        }
    }

    public void PlayConfetti(int count = 30)
    {
        _confetti?.Play(count);
    }

    public void HideOverlays()
    {
        _confetti?.Stop();
        if (_gameOverPanel != null) _gameOverPanel.SetActive(false);
        if (_winPanel != null) _winPanel.SetActive(false);
    }

    private Sprite CreateRoundedRectSprite(float width, float height, float radius)
    {
        int w = Mathf.RoundToInt(width);
        int h = Mathf.RoundToInt(height);
        int r = Mathf.RoundToInt(radius);
        string key = $"{w}x{h}r{r}";

        if (_spriteCache.TryGetValue(key, out var cached))
            return cached;

        var tex = new Texture2D(w, h, TextureFormat.ARGB32, false);
        tex.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                bool inside = true;
                if (x < radius && y < radius)
                    inside = (x - radius) * (x - radius) + (y - radius) * (y - radius) <= radius * radius;
                else if (x >= w - radius && y < radius)
                    inside = (x - (w - radius - 1)) * (x - (w - radius - 1)) + (y - radius) * (y - radius) <= radius * radius;
                else if (x < radius && y >= h - radius)
                    inside = (x - radius) * (x - radius) + (y - (h - radius - 1)) * (y - (h - radius - 1)) <= radius * radius;
                else if (x >= w - radius && y >= h - radius)
                    inside = (x - (w - radius - 1)) * (x - (w - radius - 1)) + (y - (h - radius - 1)) * (y - (h - radius - 1)) <= radius * radius;

                pixels[y * w + x] = inside ? Color.white : Color.clear;
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();

        var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100);
        _spriteCache[key] = sprite;
        return sprite;
    }
}