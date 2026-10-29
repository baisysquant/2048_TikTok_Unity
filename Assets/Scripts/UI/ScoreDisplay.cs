using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 分数/UI 管理器（对齐微信版 2048-wechat-canvas 玩法布局）：
///   - 顶部：浣熊标题 + 得分 / 最佳 / 步数
///   - 左上角：声音开关圆形按钮
///   - 棋盘正下方一行：左侧「后悔」胶囊（免费次数副标题），右侧「特技池」条（有库存呼吸发光）
///   - 特技池弹窗（FIFO 列表，本局首次免广告提示，点击使用/看广告）
///   - 棋盘闪烁按钮（⚡×512，10 秒倒计时，最后 30% 变红）
///   - 难度选择弹窗（经典 4x4 / 挑战 5x5，记录上次使用）
///   - 结算弹窗（游戏结束：复活 / 重开；胜利：继续 / 重开）
/// </summary>
public class ScoreDisplay : MonoBehaviour
{
    private const string DifficultySaveKey = "fk2048_difficulty";

    private Canvas _canvas;

    private TMPro.TextMeshProUGUI _scoreText;
    private TMPro.TextMeshProUGUI _bestScoreText;
    private TMPro.TextMeshProUGUI _moveCountText;
    private TMPro.TextMeshProUGUI _gameOverScoreText;
    private GameObject _gameOverPanel;
    private GameObject _winPanel;
    private ConfettiEffect _confetti;
    private FloatingText _floatingText;

    // 底部行
    private TMPro.TextMeshProUGUI _undoSubText;
    private Image _undoImage;
    private Image _poolImage;
    private RectTransform _poolRect;
    private CanvasGroup _poolGlow;

    // 特技池弹窗
    private GameObject _specialPanel;
    private RectTransform _specialListRoot;
    private TMPro.TextMeshProUGUI _specialFreeHint;
    private TMPro.TextMeshProUGUI _specialCountText;
    private readonly List<GameObject> _specialRows = new List<GameObject>();

    // 棋盘闪烁按钮
    private GameObject _pendingBtn;
    private Image _pendingBg;
    private TMPro.TextMeshProUGUI _pendingText;
    private Image _pendingProgress;
    private RectTransform _pendingRT;

    // 难度弹窗
    private GameObject _difficultyPanel;

    // 回调（由 GameManager 注入）
    private System.Action _onNewGame;
    private System.Action _onUndo;
    private System.Action _onPoolClicked;
    private System.Action<SpecialSystem.SpecialItem> _onSpecialItemUse;
    private System.Action<int> _onDifficultySelected;

    // 弹窗回调
    private System.Action _restartCallback;
    private System.Action _continueCallback;

    // 浣熊眨眼
    private RectTransform _eyeL;
    private RectTransform _eyeR;

    private RectTransform _soundBtnRT;

    private static readonly Color FurColor = new Color(0.87f, 0.82f, 0.77f);
    private static readonly Color FurDark = new Color(0.76f, 0.68f, 0.62f);
    private static readonly Color BellyColor = new Color(0.94f, 0.91f, 0.88f);
    private static readonly Color EyeColor = new Color(0.23f, 0.20f, 0.19f);
    private static readonly Color NoseColor = new Color(0.23f, 0.20f, 0.19f);
    private static readonly Color TitleTextColor = new Color(0.47f, 0.43f, 0.40f);
    private static readonly Color BtnBrown = new Color(0.56f, 0.48f, 0.40f);
    private static readonly Color BtnOrange = new Color(0.95f, 0.69f, 0.47f);
    private static readonly Color BtnGray = new Color(0.89f, 0.87f, 0.83f);

    private static readonly Dictionary<string, Sprite> _spriteCache = new Dictionary<string, Sprite>();

    public void Initialize(Canvas canvas, RectTransform boardContainer)
    {
        _canvas = canvas;

        var confGO = new GameObject("Confetti", typeof(ConfettiEffect));
        confGO.transform.SetParent(_canvas.transform, false);
        _confetti = confGO.GetComponent<ConfettiEffect>();
        _confetti.Initialize(canvas);

        CreateScoreUI(boardContainer);
        CreateBottomRow(boardContainer);
        CreateSoundButton();
        CreateDifficultyDialog();
        CreateSpecialPanel();
        CreateGameOverOverlay();
        CreateWinOverlay();

        // 飘字：棋盘中心行出现 → 上浮 → 棋盘顶部消失（对齐微信版 pushFloatText）
        float floatStartY = boardContainer.anchoredPosition.y;    // 棋盘中心（容器锚点即棋盘中心）
        float floatRise = boardContainer.sizeDelta.y / 2f + 60f;  // 上浮到棋盘顶部之上
        var floatGO = new GameObject("FloatingText", typeof(FloatingText));
        floatGO.transform.SetParent(_canvas.transform, false);
        _floatingText = floatGO.GetComponent<FloatingText>();
        _floatingText.Initialize(canvas, floatStartY, floatRise);

        StartCoroutine(BlinkRoutine());
    }

    // ========== 顶部：浣熊标题 + 分数 ==========

    private void CreateScoreUI(RectTransform boardContainer)
    {
        float boardWidth = boardContainer.sizeDelta.x;
        float boardHeight = boardContainer.sizeDelta.y;
        float boardTop = boardContainer.anchoredPosition.y + boardHeight / 2f;

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
    }

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

    // ========== 底部行：New | 后悔 | 特技池（对齐微信版：后悔+特技池同行且不超棋盘） ==========

    private void CreateBottomRow(RectTransform boardContainer)
    {
        float boardWidth = boardContainer.sizeDelta.x;
        float boardBottom = boardContainer.anchoredPosition.y - boardContainer.sizeDelta.y / 2f;

        var bottomContainer = new GameObject("BottomContainer", typeof(RectTransform));
        var bottomRT = bottomContainer.GetComponent<RectTransform>();
        bottomRT.SetParent(_canvas.transform, false);
        bottomRT.anchorMin = new Vector2(0.5f, 0.5f);
        bottomRT.anchorMax = new Vector2(0.5f, 0.5f);
        bottomRT.sizeDelta = new Vector2(boardWidth, 96);
        bottomRT.anchoredPosition = new Vector2(0, boardBottom - 130);

        float gap = 12;
        float usable = boardWidth;
        float newW = Mathf.Min(150, usable * 0.22f);
        float undoW = usable * 0.32f;
        float poolW = usable - newW - undoW - gap * 2;
        float x = -boardWidth / 2;

        // ---- New 按钮 ----
        var newBtn = CreateActionButton(bottomRT, "NewBtn", "↻ 新局", new Vector2(x + newW / 2, 0));
        newBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(newW, 88);
        newBtn.GetComponent<Button>().onClick.AddListener(() => _onNewGame?.Invoke());
        x += newW + gap;

        // ---- 后悔按钮（胶囊，带免费次数副标题） ----
        var undoBtnGO = new GameObject("UndoBtn", typeof(Image), typeof(Button), typeof(CanvasGroup));
        var undoRT = undoBtnGO.GetComponent<RectTransform>();
        undoRT.SetParent(bottomRT, false);
        undoRT.anchorMin = new Vector2(0.5f, 1);
        undoRT.anchorMax = new Vector2(0.5f, 1);
        undoRT.sizeDelta = new Vector2(undoW, 88);
        undoRT.anchoredPosition = new Vector2(x + undoW / 2, 0);
        _undoImage = undoBtnGO.GetComponent<Image>();
        _undoImage.sprite = CreateRoundedRectSprite(undoW, 88, 44);
        _undoImage.color = BtnBrown;
        _undoImage.type = Image.Type.Sliced;
        var undoBtn = undoBtnGO.GetComponent<Button>();
        undoBtn.targetGraphic = _undoImage;
        undoBtn.onClick.AddListener(() => _onUndo?.Invoke());

        var undoTextGO = new GameObject("Text", typeof(TMPro.TextMeshProUGUI));
        var undoTextRT = undoTextGO.GetComponent<RectTransform>();
        undoTextRT.SetParent(undoRT, false);
        undoTextRT.anchorMin = Vector2.zero;
        undoTextRT.anchorMax = Vector2.one;
        undoTextRT.sizeDelta = Vector2.zero;
        undoTextRT.offsetMin = new Vector2(4, 30);
        undoTextRT.offsetMax = new Vector2(-4, -4);
        var undoTMP = undoTextGO.GetComponent<TMPro.TextMeshProUGUI>();
        undoTMP.text = "后悔";
        undoTMP.fontSize = 26;
        undoTMP.fontStyle = TMPro.FontStyles.Bold;
        undoTMP.alignment = TMPro.TextAlignmentOptions.Center;
        undoTMP.color = Color.white;

        var undoSubGO = new GameObject("Sub", typeof(TMPro.TextMeshProUGUI));
        var undoSubRT = undoSubGO.GetComponent<RectTransform>();
        undoSubRT.SetParent(undoRT, false);
        undoSubRT.anchorMin = Vector2.zero;
        undoSubRT.anchorMax = Vector2.one;
        undoSubRT.sizeDelta = Vector2.zero;
        undoSubRT.offsetMin = new Vector2(4, 2);
        undoSubRT.offsetMax = new Vector2(-4, -26);
        _undoSubText = undoSubGO.GetComponent<TMPro.TextMeshProUGUI>();
        _undoSubText.text = "";
        _undoSubText.fontSize = 16;
        _undoSubText.alignment = TMPro.TextAlignmentOptions.Center;
        _undoSubText.color = new Color(1, 1, 1, 0.85f);
        x += undoW + gap;

        // ---- 特技池条（⚡ 特技池，有库存时呼吸发光） ----
        var poolGO = new GameObject("SpecialPoolBar", typeof(Image), typeof(Button), typeof(CanvasGroup));
        _poolRect = poolGO.GetComponent<RectTransform>();
        _poolRect.SetParent(bottomRT, false);
        _poolRect.anchorMin = new Vector2(0.5f, 1);
        _poolRect.anchorMax = new Vector2(0.5f, 1);
        _poolRect.sizeDelta = new Vector2(poolW, 88);
        _poolRect.anchoredPosition = new Vector2(x + poolW / 2, 0);
        _poolImage = poolGO.GetComponent<Image>();
        _poolImage.sprite = CreateRoundedRectSprite(poolW, 88, 44);
        _poolImage.type = Image.Type.Sliced;
        _poolImage.color = BtnGray;
        var poolBtn = poolGO.GetComponent<Button>();
        poolBtn.targetGraphic = _poolImage;
        poolBtn.onClick.AddListener(() => _onPoolClicked?.Invoke());
        _poolGlow = poolGO.GetComponent<CanvasGroup>();

        // ⚡ 图标（左侧圆底）
        var iconGO = new GameObject("BoltIcon", typeof(Image));
        var iconRT = iconGO.GetComponent<RectTransform>();
        iconRT.SetParent(_poolRect, false);
        iconRT.anchorMin = new Vector2(0, 0.5f);
        iconRT.anchorMax = new Vector2(0, 0.5f);
        iconRT.sizeDelta = new Vector2(46, 46);
        iconRT.anchoredPosition = new Vector2(34, 0);
        var iconImg = iconGO.GetComponent<Image>();
        iconImg.sprite = CreateCircleSprite(46);
        iconImg.color = new Color(1, 1, 1, 0.30f);
        iconImg.raycastTarget = false;

        var boltGO = new GameObject("Bolt", typeof(TMPro.TextMeshProUGUI));
        var boltRT = boltGO.GetComponent<RectTransform>();
        boltRT.SetParent(iconRT, false);
        boltRT.anchorMin = Vector2.zero;
        boltRT.anchorMax = Vector2.one;
        boltRT.sizeDelta = Vector2.zero;
        var boltTMP = boltGO.GetComponent<TMPro.TextMeshProUGUI>();
        boltTMP.text = "⚡";
        boltTMP.fontSize = 26;
        boltTMP.fontStyle = TMPro.FontStyles.Bold;
        boltTMP.color = new Color(1f, 0.97f, 0.9f);
        boltTMP.alignment = TMPro.TextAlignmentOptions.Center;
        boltTMP.raycastTarget = false;

        var poolTextGO = new GameObject("Text", typeof(TMPro.TextMeshProUGUI));
        var poolTextRT = poolTextGO.GetComponent<RectTransform>();
        poolTextRT.SetParent(_poolRect, false);
        poolTextRT.anchorMin = Vector2.zero;
        poolTextRT.anchorMax = Vector2.one;
        poolTextRT.sizeDelta = Vector2.zero;
        poolTextRT.offsetMin = new Vector2(64, 0);
        poolTextRT.offsetMax = new Vector2(-16, 0);
        var poolTMP = poolTextGO.GetComponent<TMPro.TextMeshProUGUI>();
        poolTMP.text = "特技池";
        poolTMP.fontSize = 26;
        poolTMP.fontStyle = TMPro.FontStyles.Bold;
        poolTMP.alignment = TMPro.TextAlignmentOptions.Center;
        poolTMP.color = new Color(0.45f, 0.38f, 0.30f);
        poolTMP.raycastTarget = false;
    }

    /// <summary>刷新特技池入口条：库存 > 0 时金色 + 呼吸发光，否则置灰</summary>
    public void UpdateSpecialPoolBar(int count, bool specialButtonPlaced)
    {
        if (_poolImage == null) return;
        bool active = count > 0;
        _poolImage.color = active ? BtnOrange : BtnGray;
        var glow = _poolGlow;
        if (active)
        {
            // 呼吸光效：1.0 ~ 1.12 缩放
            float pulse = 1f + 0.12f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f));
            _poolRect.localScale = new Vector3(pulse, pulse, 1);
        }
        else
        {
            _poolRect.localScale = Vector3.one;
        }
        glow.alpha = 1;
        _ = specialButtonPlaced;
    }

    /// <summary>刷新后悔按钮：可回撤时高亮，置灰时副标题变化</summary>
    public void UpdateUndoButton(bool canUndo, int freeLeft)
    {
        if (_undoImage == null) return;
        _undoImage.color = canUndo ? BtnBrown : BtnGray;
        if (_undoSubText == null) return;
        _undoSubText.text = !canUndo
            ? ""
            : (freeLeft > 0 ? $"免费 ×{freeLeft}" : "完整看广告");
        _undoSubText.color = canUndo
            ? new Color(1, 1, 1, 0.85f)
            : new Color(0.55f, 0.50f, 0.45f);
    }

    // ========== 左上角声音开关（对齐微信版放置位置） ==========

    private void CreateSoundButton()
    {
        var go = new GameObject("MusicToggle", typeof(Image), typeof(Button));
        _soundBtnRT = go.GetComponent<RectTransform>();
        var rt = _soundBtnRT;
        rt.SetParent(_canvas.transform, false);
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.sizeDelta = new Vector2(64, 64);
        rt.anchoredPosition = new Vector2(48, -56);

        var img = go.GetComponent<Image>();
        img.sprite = CreateCircleSprite(64);
        img.color = new Color(0.56f, 0.48f, 0.40f, 0.35f);
        img.raycastTarget = true;
        img.type = Image.Type.Simple;

        var textGO = new GameObject("Icon", typeof(TMPro.TextMeshProUGUI));
        var textRT = textGO.GetComponent<RectTransform>();
        textRT.SetParent(rt, false);
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.sizeDelta = Vector2.zero;
        var tmp = textGO.GetComponent<TMPro.TextMeshProUGUI>();
        var am = FindObjectOfType<AudioManager>();
        bool soundOn = am == null || am.IsSoundEnabled;
        tmp.text = soundOn ? "\u266B" : "\u266B\u0338";
        tmp.fontSize = 30;
        tmp.fontStyle = TMPro.FontStyles.Bold;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.color = new Color(0.33f, 0.27f, 0.22f);

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() =>
        {
            var audio = FindObjectOfType<AudioManager>();
            if (audio != null)
            {
                audio.ToggleSound();
                tmp.text = audio.IsSoundEnabled ? "\u266B" : "\u266B\u0338";
            }
        });
    }

    // ========== 通用控件工厂 ==========

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

    private GameObject CreateActionButton(RectTransform parent, string name, string text, Vector2 position)
    {
        var go = new GameObject(name, typeof(Image), typeof(Button));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0.5f, 1);
        rt.anchorMax = new Vector2(0.5f, 1);
        rt.sizeDelta = new Vector2(180, 88);
        rt.anchoredPosition = position;

        var img = go.GetComponent<Image>();
        img.color = new Color(0.71f, 0.65f, 0.60f);
        img.sprite = CreateRoundedRectSprite(180, 88, 16);

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

    // ========== 难度选择弹窗（经典 4x4 / 挑战 5x5） ==========

    private void CreateDifficultyDialog()
    {
        _difficultyPanel = CreateOverlay("DifficultyPanel", true);
        _difficultyPanel.GetComponent<Image>().color = new Color(0.14f, 0.11f, 0.09f, 0.55f);

        float panelW = Mathf.Min(880, _canvas.GetComponent<RectTransform>().rect.width - 80);
        float panelH = 480;
        var panel = new GameObject("Panel", typeof(Image));
        var panelRT = panel.GetComponent<RectTransform>();
        panelRT.SetParent(_difficultyPanel.transform, false);
        panelRT.anchorMin = new Vector2(0.5f, 0.5f);
        panelRT.anchorMax = new Vector2(0.5f, 0.5f);
        panelRT.sizeDelta = new Vector2(panelW, panelH);
        panelRT.anchoredPosition = Vector2.zero;
        var panelImg = panel.GetComponent<Image>();
        panelImg.color = new Color(0.98f, 0.97f, 0.94f);
        panelImg.sprite = CreateRoundedRectSprite(Mathf.RoundToInt(panelW), Mathf.RoundToInt(panelH), 26);

        var title = new GameObject("Title", typeof(TMPro.TextMeshProUGUI));
        var titleRT = title.GetComponent<RectTransform>();
        titleRT.SetParent(panelRT, false);
        titleRT.anchorMin = new Vector2(0, 0.5f);
        titleRT.anchorMax = new Vector2(1, 0.5f);
        titleRT.sizeDelta = new Vector2(0, 70);
        titleRT.anchoredPosition = new Vector2(0, 150);
        var titleTMP = title.GetComponent<TMPro.TextMeshProUGUI>();
        titleTMP.text = "选择难度";
        titleTMP.fontSize = 44;
        titleTMP.fontStyle = TMPro.FontStyles.Bold;
        titleTMP.alignment = TMPro.TextAlignmentOptions.Center;
        titleTMP.color = TitleTextColor;

        var subtitle = new GameObject("Subtitle", typeof(TMPro.TextMeshProUGUI));
        var subRT = subtitle.GetComponent<RectTransform>();
        subRT.SetParent(panelRT, false);
        subRT.anchorMin = new Vector2(0, 0.5f);
        subRT.anchorMax = new Vector2(1, 0.5f);
        subRT.sizeDelta = new Vector2(0, 40);
        subRT.anchoredPosition = new Vector2(0, 100);
        var subTMP = subtitle.GetComponent<TMPro.TextMeshProUGUI>();
        subTMP.text = "选择棋盘开始游戏";
        subTMP.fontSize = 26;
        subTMP.alignment = TMPro.TextAlignmentOptions.Center;
        subTMP.color = new Color(0.61f, 0.55f, 0.48f);

        // 两个难度按钮
        float gap = 26;
        float btnW = (panelW - 80 - gap) / 2f;
        float btnH = 110;
        float y = 0;
        CreateDifficultyButton(panelRT, "Btn4", "经典 4×4", 4, new Vector2(-btnW / 2 - gap / 2, y), btnW, btnH);
        CreateDifficultyButton(panelRT, "Btn5", "挑战 5×5", 5, new Vector2(btnW / 2 + gap / 2, y), btnW, btnH);

        var tip = new GameObject("Tip", typeof(TMPro.TextMeshProUGUI));
        var tipRT = tip.GetComponent<RectTransform>();
        tipRT.SetParent(panelRT, false);
        tipRT.anchorMin = new Vector2(0, 0.5f);
        tipRT.anchorMax = new Vector2(1, 0.5f);
        tipRT.sizeDelta = new Vector2(0, 34);
        tipRT.anchoredPosition = new Vector2(0, -165);
        var tipTMP = tip.GetComponent<TMPro.TextMeshProUGUI>();
        tipTMP.text = "再次点按上次使用的难度可直接开局";
        tipTMP.fontSize = 22;
        tipTMP.alignment = TMPro.TextAlignmentOptions.Center;
        tipTMP.color = new Color(0.68f, 0.62f, 0.55f);

        _difficultyPanel.SetActive(false);
    }

    private void CreateDifficultyButton(RectTransform parent, string name, string label, int size, Vector2 pos, float w, float h)
    {
        var go = new GameObject(name, typeof(Image), typeof(Button));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = pos;

        var img = go.GetComponent<Image>();
        img.sprite = CreateRoundedRectSprite(Mathf.RoundToInt(w), Mathf.RoundToInt(h), 18);
        img.type = Image.Type.Sliced;
        img.color = BtnBrown;

        var text = new GameObject("Label", typeof(TMPro.TextMeshProUGUI));
        var textRT = text.GetComponent<RectTransform>();
        textRT.SetParent(rt, false);
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.sizeDelta = Vector2.zero;
        textRT.offsetMin = new Vector2(0, 30);
        textRT.offsetMax = new Vector2(0, -6);
        var tmp = text.GetComponent<TMPro.TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 36;
        tmp.fontStyle = TMPro.FontStyles.Bold;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.color = new Color(0.96f, 0.96f, 0.94f);

        var sub = new GameObject("LastUsed", typeof(TMPro.TextMeshProUGUI));
        var subRT = sub.GetComponent<RectTransform>();
        subRT.SetParent(rt, false);
        subRT.anchorMin = Vector2.zero;
        subRT.anchorMax = Vector2.one;
        subRT.sizeDelta = Vector2.zero;
        subRT.offsetMin = new Vector2(0, 2);
        subRT.offsetMax = new Vector2(0, -26);
        var subTMP = sub.GetComponent<TMPro.TextMeshProUGUI>();
        subTMP.text = "";
        subTMP.fontSize = 20;
        subTMP.fontStyle = TMPro.FontStyles.Bold;
        subTMP.alignment = TMPro.TextAlignmentOptions.Center;
        subTMP.color = new Color(0.36f, 0.29f, 0.23f);

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        var cachedImg = img;
        var cachedSub = subTMP;
        int cachedSize = size;
        btn.onClick.AddListener(() =>
        {
            // 高亮选中并标注"上次使用"
            HighlightDifficultyButtons(cachedSize);
            _onDifficultySelected?.Invoke(cachedSize);
        });

        // 存储引用以便高亮
        _difficultyBtnImages[size] = cachedImg;
        _difficultyBtnSubs[size] = cachedSub;
    }

    private readonly Dictionary<int, Image> _difficultyBtnImages = new Dictionary<int, Image>();
    private readonly Dictionary<int, TMPro.TextMeshProUGUI> _difficultyBtnSubs = new Dictionary<int, TMPro.TextMeshProUGUI>();

    private void HighlightDifficultyButtons(int selected)
    {
        foreach (var kvp in _difficultyBtnImages)
        {
            kvp.Value.color = kvp.Key == selected ? BtnOrange : BtnBrown;
        }
        foreach (var kvp in _difficultyBtnSubs)
        {
            kvp.Value.text = kvp.Key == selected ? "上次使用" : "";
        }
    }

    /// <summary>声音开关在任意界面（含弹窗）都可点（对齐微信版：左上角按钮常驻顶层）</summary>
    private void BringSoundButtonToFront()
    {
        if (_soundBtnRT != null)
            _soundBtnRT.SetAsLastSibling();
    }

    /// <summary>显示难度弹窗；lastSelected 为上次选择的难度（-1 表示无记录）</summary>
    public void ShowDifficultyDialog(int lastSelected)
    {
        if (_difficultyPanel != null)
        {
            HighlightDifficultyButtons(lastSelected);
            _difficultyPanel.SetActive(true);
            _difficultyPanel.transform.SetAsLastSibling();
            BringSoundButtonToFront();
        }
    }

    public void HideDifficultyDialog()
    {
        if (_difficultyPanel != null)
            _difficultyPanel.SetActive(false);
    }

    // ========== 特技池弹窗（FIFO 列表） ==========

    private void CreateSpecialPanel()
    {
        _specialPanel = CreateOverlay("SpecialPanel", true);
        _specialPanel.GetComponent<Image>().color = new Color(0.10f, 0.08f, 0.07f, 0.45f);

        float panelW = Mathf.Min(900, _canvas.GetComponent<RectTransform>().rect.width - 60);
        float listH = 700;   // 容纳 10 行（10×58 + 9×10 = 670）
        float panelH = 170 + listH + 40;

        var panel = new GameObject("Panel", typeof(Image));
        var panelRT = panel.GetComponent<RectTransform>();
        panelRT.SetParent(_specialPanel.transform, false);
        panelRT.anchorMin = new Vector2(0.5f, 0.5f);
        panelRT.anchorMax = new Vector2(0.5f, 0.5f);
        panelRT.sizeDelta = new Vector2(panelW, panelH);
        panelRT.anchoredPosition = Vector2.zero;
        var panelImg = panel.GetComponent<Image>();
        panelImg.color = new Color(0.98f, 0.96f, 0.92f);
        panelImg.sprite = CreateRoundedRectSprite(Mathf.RoundToInt(panelW), Mathf.RoundToInt(panelH), 24);

        // 标题行
        var title = new GameObject("Title", typeof(TMPro.TextMeshProUGUI));
        var titleRT = title.GetComponent<RectTransform>();
        titleRT.SetParent(panelRT, false);
        titleRT.anchorMin = new Vector2(0, 0.5f);
        titleRT.anchorMax = new Vector2(1, 0.5f);
        titleRT.sizeDelta = new Vector2(0, 60);
        titleRT.anchoredPosition = new Vector2(0, panelH / 2 - 60);
        var titleTMP = title.GetComponent<TMPro.TextMeshProUGUI>();
        titleTMP.text = "⚡ 特技池";
        titleTMP.fontSize = 36;
        titleTMP.fontStyle = TMPro.FontStyles.Bold;
        titleTMP.alignment = TMPro.TextAlignmentOptions.Center;
        titleTMP.color = TitleTextColor;

        var countGO = new GameObject("Count", typeof(TMPro.TextMeshProUGUI));
        var countRT = countGO.GetComponent<RectTransform>();
        countRT.SetParent(panelRT, false);
        countRT.anchorMin = new Vector2(0, 0.5f);
        countRT.anchorMax = new Vector2(1, 0.5f);
        countRT.sizeDelta = new Vector2(0, 40);
        countRT.anchoredPosition = new Vector2(0, panelH / 2 - 118);
        _specialCountText = countGO.GetComponent<TMPro.TextMeshProUGUI>();
        _specialCountText.alignment = TMPro.TextAlignmentOptions.Center;
        _specialCountText.fontSize = 24;
        _specialCountText.color = new Color(0.61f, 0.55f, 0.48f);

        var hintGO = new GameObject("FreeHint", typeof(TMPro.TextMeshProUGUI));
        var hintRT = hintGO.GetComponent<RectTransform>();
        hintRT.SetParent(panelRT, false);
        hintRT.anchorMin = new Vector2(0, 0.5f);
        hintRT.anchorMax = new Vector2(1, 0.5f);
        hintRT.sizeDelta = new Vector2(0, 34);
        hintRT.anchoredPosition = new Vector2(0, panelH / 2 - 160);
        _specialFreeHint = hintGO.GetComponent<TMPro.TextMeshProUGUI>();
        _specialFreeHint.text = "";
        _specialFreeHint.alignment = TMPro.TextAlignmentOptions.Center;
        _specialFreeHint.fontSize = 22;
        _specialFreeHint.fontStyle = TMPro.FontStyles.Bold;
        _specialFreeHint.color = new Color(0.28f, 0.60f, 0.35f);

        // 列表容器
        _specialListRoot = new GameObject("List", typeof(RectTransform)).GetComponent<RectTransform>();
        _specialListRoot.SetParent(panelRT, false);
        _specialListRoot.anchorMin = new Vector2(0, 0.5f);
        _specialListRoot.anchorMax = new Vector2(1, 0.5f);
        _specialListRoot.sizeDelta = new Vector2(panelW - 80, listH);
        _specialListRoot.anchoredPosition = new Vector2(0, -110);

        // 关闭按钮（右上角）
        var closeGO = new GameObject("Close", typeof(Image), typeof(Button));
        var closeRT = closeGO.GetComponent<RectTransform>();
        closeRT.SetParent(panelRT, false);
        closeRT.anchorMin = new Vector2(0.5f, 0.5f);
        closeRT.anchorMax = new Vector2(0.5f, 0.5f);
        closeRT.sizeDelta = new Vector2(64, 64);
        closeRT.anchoredPosition = new Vector2(panelW / 2 - 70, panelH / 2 - 64);
        var closeImg = closeGO.GetComponent<Image>();
        closeImg.sprite = CreateCircleSprite(64);
        closeImg.color = new Color(0.56f, 0.48f, 0.40f, 0.18f);
        var closeText = new GameObject("X", typeof(TMPro.TextMeshProUGUI));
        var closeTextRT = closeText.GetComponent<RectTransform>();
        closeTextRT.SetParent(closeRT, false);
        closeTextRT.anchorMin = Vector2.zero;
        closeTextRT.anchorMax = Vector2.one;
        closeTextRT.sizeDelta = Vector2.zero;
        var closeTMP = closeText.GetComponent<TMPro.TextMeshProUGUI>();
        closeTMP.text = "✕";
        closeTMP.fontSize = 34;
        closeTMP.fontStyle = TMPro.FontStyles.Bold;
        closeTMP.alignment = TMPro.TextAlignmentOptions.Center;
        closeTMP.color = TitleTextColor;
        closeGO.GetComponent<Button>().onClick.AddListener(CloseSpecialPanel);

        _specialPanel.SetActive(false);
    }

    public bool IsSpecialPanelOpen => _specialPanel != null && _specialPanel.activeSelf;

    public void CloseSpecialPanel()
    {
        if (_specialPanel != null)
            _specialPanel.SetActive(false);
    }

    /// <summary>打开并刷新特技池弹窗（每次打开/池变化时重建列表）</summary>
    public void ShowSpecialPanel(IReadOnlyList<SpecialSystem.SpecialItem> pool, bool freeAvailable)
    {
        if (_specialPanel == null) return;
        RefreshSpecialPanel(pool, freeAvailable);
        _specialPanel.SetActive(true);
        _specialPanel.transform.SetAsLastSibling();
        BringSoundButtonToFront();
    }

    public void RefreshSpecialPanel(IReadOnlyList<SpecialSystem.SpecialItem> pool, bool freeAvailable)
    {
        for (int i = 0; i < _specialRows.Count; i++)
        {
            if (_specialRows[i] != null)
                Destroy(_specialRows[i]);
        }
        _specialRows.Clear();

        if (_specialCountText != null)
            _specialCountText.text = $"库存 {pool.Count} / {SpecialSystem.SpecialCap}";

        if (_specialFreeHint != null)
            _specialFreeHint.text = freeAvailable ? "本局首次使用免广告 🎁" : "";

        if (_specialListRoot == null) return;
        float rowH = 58;
        float gap = 10;
        for (int i = 0; i < pool.Count; i++)
        {
            var item = pool[i];
            var row = new GameObject($"Row_{i}", typeof(Image), typeof(Button));
            var rowRT = row.GetComponent<RectTransform>();
            rowRT.SetParent(_specialListRoot, false);
            rowRT.anchorMin = new Vector2(0, 1);
            rowRT.anchorMax = new Vector2(0, 1);
            rowRT.sizeDelta = new Vector2(_specialListRoot.sizeDelta.x, rowH);
            rowRT.anchoredPosition = new Vector2(0, -(i * (rowH + gap) + rowH / 2));

            var rowImg = row.GetComponent<Image>();
            rowImg.color = new Color(0.94f, 0.91f, 0.86f);
            rowImg.sprite = CreateRoundedRectSprite(Mathf.RoundToInt(_specialListRoot.sizeDelta.x), Mathf.RoundToInt(rowH), 14);
            rowImg.type = Image.Type.Sliced;

            var idx = new GameObject("Idx", typeof(TMPro.TextMeshProUGUI));
            var idxRT = idx.GetComponent<RectTransform>();
            idxRT.SetParent(rowRT, false);
            idxRT.anchorMin = new Vector2(0, 0.5f);
            idxRT.anchorMax = new Vector2(0, 0.5f);
            idxRT.sizeDelta = new Vector2(120, rowH);
            idxRT.anchoredPosition = new Vector2(70, 0);
            var idxTMP = idx.GetComponent<TMPro.TextMeshProUGUI>();
            idxTMP.text = $"#{i + 1}";
            idxTMP.fontSize = 28;
            idxTMP.fontStyle = TMPro.FontStyles.Bold;
            idxTMP.color = new Color(0.61f, 0.55f, 0.48f);
            idxTMP.alignment = TMPro.TextAlignmentOptions.Center;

            var val = new GameObject("Val", typeof(TMPro.TextMeshProUGUI));
            var valRT = val.GetComponent<RectTransform>();
            valRT.SetParent(rowRT, false);
            valRT.anchorMin = new Vector2(0.5f, 0.5f);
            valRT.anchorMax = new Vector2(0.5f, 0.5f);
            valRT.sizeDelta = new Vector2(200, rowH);
            valRT.anchoredPosition = new Vector2(10, 0);
            var valTMP = val.GetComponent<TMPro.TextMeshProUGUI>();
            valTMP.text = $"⚡ ×{item.value}";
            valTMP.fontSize = 30;
            valTMP.fontStyle = TMPro.FontStyles.Bold;
            valTMP.color = new Color(0.42f, 0.35f, 0.28f);
            valTMP.alignment = TMPro.TextAlignmentOptions.Center;

            var use = new GameObject("UseHint", typeof(TMPro.TextMeshProUGUI));
            var useRT = use.GetComponent<RectTransform>();
            useRT.SetParent(rowRT, false);
            useRT.anchorMin = new Vector2(1, 0.5f);
            useRT.anchorMax = new Vector2(1, 0.5f);
            useRT.sizeDelta = new Vector2(160, rowH);
            useRT.anchoredPosition = new Vector2(-86, 0);
            var useTMP = use.GetComponent<TMPro.TextMeshProUGUI>();
            useTMP.text = "+" + SpecialSystem.SpecialReward + " ›";
            useTMP.fontSize = 24;
            useTMP.fontStyle = TMPro.FontStyles.Bold;
            useTMP.color = new Color(0.95f, 0.69f, 0.47f);
            useTMP.alignment = TMPro.TextAlignmentOptions.Center;

            var btn = row.GetComponent<Button>();
            btn.targetGraphic = rowImg;
            var clicked = item;
            btn.onClick.AddListener(() => _onSpecialItemUse?.Invoke(clicked));

            _specialRows.Add(row);
        }

        if (pool.Count == 0)
        {
            var empty = new GameObject("Empty", typeof(TMPro.TextMeshProUGUI));
            var emptyRT = empty.GetComponent<RectTransform>();
            emptyRT.SetParent(_specialListRoot, false);
            emptyRT.anchorMin = new Vector2(0, 0.5f);
            emptyRT.anchorMax = new Vector2(1, 0.5f);
            emptyRT.sizeDelta = new Vector2(_specialListRoot.sizeDelta.x, 60);
            var emptyTMP = empty.GetComponent<TMPro.TextMeshProUGUI>();
            emptyTMP.text = "特技池空空如也，合成 512 获取机会 ⚡";
            emptyTMP.fontSize = 24;
            emptyTMP.alignment = TMPro.TextAlignmentOptions.Center;
            emptyTMP.color = new Color(0.61f, 0.55f, 0.48f);
            _specialRows.Add(empty);
        }
    }

    // ========== 棋盘闪烁按钮（⚡×512 倒计时） ==========

    private System.Action _onPendingClicked;

    public void BindPendingButton(System.Action onClick)
    {
        _onPendingClicked = onClick;
    }

    /// <summary>
    /// 每帧刷新闪烁按钮：无 pending 则销毁；有则创建/跟随位置/更新倒计时与颜色。
    /// remain01：剩余时间比例 0..1；最后 30% 变红并加强发光。
    /// </summary>
    public void UpdatePendingButton(SpecialSystem.SpecialPending pending, BoardRenderer board)
    {
        if (pending == null)
        {
            DestroyPendingButton();
            return;
        }

        if (_pendingBtn == null)
        {
            float size = Mathf.Max(84, board.CellSize * 0.82f);
            _pendingBtn = new GameObject("PendingBoost", typeof(Image), typeof(Button));
            _pendingRT = _pendingBtn.GetComponent<RectTransform>();
            _pendingRT.SetParent(board.BoardContainer, false);
            _pendingRT.anchorMin = new Vector2(0, 1);
            _pendingRT.anchorMax = new Vector2(0, 1);
            _pendingRT.sizeDelta = new Vector2(size, size);

            _pendingBg = _pendingBtn.GetComponent<Image>();
            _pendingBg.sprite = CreateCircleSprite(Mathf.RoundToInt(size));
            _pendingBg.color = BtnOrange;
            _pendingBg.type = Image.Type.Simple;

            var textGO = new GameObject("Text", typeof(TMPro.TextMeshProUGUI));
            var textRT = textGO.GetComponent<RectTransform>();
            textRT.SetParent(_pendingRT, false);
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.sizeDelta = Vector2.zero;
            textRT.offsetMin = new Vector2(2, size * 0.22f);
            textRT.offsetMax = new Vector2(-2, -2);
            _pendingText = textGO.GetComponent<TMPro.TextMeshProUGUI>();
            _pendingText.text = "⚡\n512";
            _pendingText.fontSize = size * 0.24f;
            _pendingText.fontStyle = TMPro.FontStyles.Bold;
            _pendingText.alignment = TMPro.TextAlignmentOptions.Center;
            _pendingText.color = new Color(1, 0.97f, 0.90f);
            _pendingText.raycastTarget = false;

            var progGO = new GameObject("Progress", typeof(Image));
            var progRT = progGO.GetComponent<RectTransform>();
            progRT.SetParent(_pendingRT, false);
            progRT.anchorMin = new Vector2(0.08f, 0.04f);
            progRT.anchorMax = new Vector2(0.92f, 0.14f);
            progRT.sizeDelta = Vector2.zero;
            _pendingProgress = progGO.GetComponent<Image>();
            _pendingProgress.type = Image.Type.Filled;
            _pendingProgress.fillMethod = Image.FillMethod.Horizontal;
            _pendingProgress.fillOrigin = 0;
            _pendingProgress.color = new Color(1, 1, 1, 0.9f);

            var btn = _pendingBtn.GetComponent<Button>();
            btn.targetGraphic = _pendingBg;
            btn.onClick.AddListener(() =>
            {
                // 点击入池（UI 层的 @_onPendingClicked → SpecialSystem.BankPending(true)）
                if (pending == null) return;
                _onPendingClicked?.Invoke();
            });
        }

        // 位置跟随格子
        Vector2 pos = board.GetCellPosition(pending.gx, pending.gy);
        _pendingRT.anchoredPosition = pos;

        // 倒计时
        float total = SpecialSystem.SpecialFloatMs / 1000f;
        float remain = Mathf.Max(0, total - (Time.unscaledTime - pending.spawnAt));
        float remain01 = Mathf.Clamp01(remain / total);
        bool danger = remain01 < 0.30f;
        float pulse = 0.88f + 0.12f * Mathf.Sin(Time.unscaledTime * (danger ? 9f : 5f));

        _pendingBg.color = danger
            ? new Color(0.93f, 0.32f, 0.28f)
            : new Color(BtnOrange.r, BtnOrange.g, BtnOrange.b, 1);
        Color c = _pendingBg.color;
        c.a = 0.92f * pulse;
        _pendingBg.color = c;

        // 秒数角标
        if (_pendingText != null && remain > 0)
        {
            string sec = Mathf.CeilToInt(remain).ToString();
            // 主文本右上角小标签（挂在 _pendingRT 根下，从根查找避免每帧重复创建）
            var sub = _pendingRT != null ? _pendingRT.Find("Sec")?.GetComponent<TMPro.TextMeshProUGUI>() : null;
            if (sub == null)
            {
                var subGO = new GameObject("Sec", typeof(TMPro.TextMeshProUGUI));
                var subRT = subGO.GetComponent<RectTransform>();
                subRT.SetParent(_pendingRT, false);
                subRT.anchorMin = new Vector2(1, 1);
                subRT.anchorMax = new Vector2(1, 1);
                subRT.sizeDelta = new Vector2(40, 24);
                subRT.anchoredPosition = new Vector2(-6, -6);
                sub = subGO.GetComponent<TMPro.TextMeshProUGUI>();
                sub.fontSize = 20;
                sub.fontStyle = TMPro.FontStyles.Bold;
                sub.alignment = TMPro.TextAlignmentOptions.Center;
                sub.color = Color.white;
            }
            sub.text = sec + "s";
        }

        if (_pendingProgress != null)
            _pendingProgress.fillAmount = remain01;
    }

    public void DestroyPendingButton()
    {
        if (_pendingBtn != null)
        {
            Destroy(_pendingBtn);
            _pendingBtn = null;
            _pendingBg = null;
            _pendingText = null;
            _pendingProgress = null;
            _pendingRT = null;
        }
    }

    // ========== 飘字 ==========

    public void ShowFloatText(string text, float duration = 1.5f, int fontSize = 34)
    {
        _floatingText?.Show(text, duration, fontSize);
    }

    // ========== 结算弹窗 ==========

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
        textTMP.text = "游戏结束";
        textTMP.fontSize = 48;
        textTMP.fontStyle = TMPro.FontStyles.Bold;
        textTMP.color = TitleTextColor;
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
        _gameOverScoreText.color = TitleTextColor;

        var reviveBtn = CreateButton(_gameOverPanel.transform, "ReviveBtn", "看广告复活", new Vector2(0, -57));
        var reviveBtnComponent = reviveBtn.GetComponent<Button>();
        reviveBtnComponent.interactable = false;
        reviveBtn.SetActive(false);

        var undoBtn = CreateButton(_gameOverPanel.transform, "UndoBtn", "后悔救场 ↩", new Vector2(-135, -137));
        undoBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(240, 56);
        undoBtn.GetComponent<Button>().onClick.AddListener(() =>
        {
            // 失败弹窗内悔棋救场：回到上一局面（隐藏弹窗由 GameManager.DoUndo 完成）
            _onUndo?.Invoke();
        });

        var restartBtn = CreateButton(_gameOverPanel.transform, "RestartBtn", "重新开始", new Vector2(135, -137));
        restartBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(240, 56);

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
        textTMP.text = "恭喜获胜!";
        textTMP.fontSize = 48;
        textTMP.fontStyle = TMPro.FontStyles.Bold;
        textTMP.color = TitleTextColor;
        textTMP.alignment = TMPro.TextAlignmentOptions.Center;

        var subGO = new GameObject("Sub", typeof(TMPro.TextMeshProUGUI));
        var subRT = subGO.GetComponent<RectTransform>();
        subRT.SetParent(_winPanel.transform, false);
        subRT.anchorMin = new Vector2(0, 0.5f);
        subRT.anchorMax = new Vector2(1, 0.5f);
        subRT.sizeDelta = new Vector2(0, 50);
        subRT.anchoredPosition = new Vector2(0, -30);
        var subTMP = subGO.GetComponent<TMPro.TextMeshProUGUI>();
        subTMP.text = "达成 2048!";
        subTMP.fontSize = 30;
        subTMP.color = TitleTextColor;
        subTMP.alignment = TMPro.TextAlignmentOptions.Center;

        var continueBtn = CreateButton(_winPanel.transform, "ContinueBtn", "继续游戏", new Vector2(-140, -140));
        continueBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(250, 56);
        var restartBtn = CreateButton(_winPanel.transform, "RestartBtn", "重新开始", new Vector2(140, -140));
        restartBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(250, 56);
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
        img.color = BtnBrown;
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

    // ========== 公共 API ==========

    public void SetCallbacks(
        System.Action onNewGame,
        System.Action onUndo,
        System.Action onPoolClicked,
        System.Action<SpecialSystem.SpecialItem> onSpecialItemUse,
        System.Action<int> onDifficultySelected)
    {
        _onNewGame = onNewGame;
        _onUndo = onUndo;
        _onPoolClicked = onPoolClicked;
        _onSpecialItemUse = onSpecialItemUse;
        _onDifficultySelected = onDifficultySelected;
    }

    public void UpdateScore(long score, long bestScore, int moveCount)
    {
        if (_scoreText != null)
            _scoreText.text = FormatScore(score);
        if (_bestScoreText != null)
            _bestScoreText.text = FormatScore(bestScore);
        if (_moveCountText != null)
            _moveCountText.text = moveCount.ToString();
    }

    /// <summary>长数字格式化（>1 万 显示 k，>1 千万 显示 M），与方块标签风格一致</summary>
    private string FormatScore(long value)
    {
        if (value >= 10000000) return (value / 1000000d).ToString("0.#") + "M";
        if (value >= 10000) return (value / 1000d).ToString("0.#") + "K";
        return value.ToString();
    }

    public void ShowGameOver(long score, System.Action onRestart, bool canRevive, System.Action onRevive)
    {
        _restartCallback = onRestart;
        if (_gameOverPanel != null)
        {
            _gameOverPanel.SetActive(true);
            _gameOverPanel.transform.SetAsLastSibling();
            BringSoundButtonToFront();
            if (_gameOverScoreText != null)
                _gameOverScoreText.text = $"最终得分: {FormatScore(score)}";

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
                if (canRevive)
                {
                    var reviveBtn = reviveBtnGO.GetComponent<Button>();
                    reviveBtn.interactable = true;
                    reviveBtn.onClick.RemoveAllListeners();
                    reviveBtn.onClick.AddListener(() =>
                    {
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
            _winPanel.transform.SetAsLastSibling();
            BringSoundButtonToFront();
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

    /// <summary>是否有任何全屏弹窗打开（难度/特技池/结算），供输入层屏蔽棋盘操作</summary>
    public bool IsAnyOverlayOpen
    {
        get
        {
            if (_specialPanel != null && _specialPanel.activeSelf) return true;
            if (_difficultyPanel != null && _difficultyPanel.activeSelf) return true;
            if (_gameOverPanel != null && _gameOverPanel.activeSelf) return true;
            if (_winPanel != null && _winPanel.activeSelf) return true;
            return false;
        }
    }

    /// <summary>游戏结束弹窗是否打开（打开时仍允许「后悔」救场）</summary>
    public bool IsGameOverPanelOpen => _gameOverPanel != null && _gameOverPanel.activeSelf;

    // ========== 圆角/圆形 Sprite 工厂（运行时生成，无外部资源） ==========

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

    private Sprite CreateCircleSprite(int size)
    {
        string key = $"circle{size}";
        if (_spriteCache.TryGetValue(key, out var cached))
            return cached;

        var tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        float r = size / 2f;
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - r + 0.5f;
                float dy = y - r + 0.5f;
                bool inside = dx * dx + dy * dy <= r * r;
                pixels[y * size + x] = inside ? Color.white : Color.clear;
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();

        var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100);
        _spriteCache[key] = sprite;
        return sprite;
    }

    private void OnDestroy()
    {
        _confetti = null;
        _floatingText = null;
    }
}
