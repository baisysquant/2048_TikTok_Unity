using System.Collections;
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
    private TMPro.TextMeshProUGUI _gameOverBestText;
    private TMPro.TextMeshProUGUI _gameOverMovesText;
    private GameObject _gameOverPanel;
    private GameObject _gameOverReviveBtnGO;
    private Button _gameOverRestartBtn;
    private GameObject _winPanel;
    private ConfettiEffect _confetti;

    // 魔法少女对话框（提示语油画框）
    private RectTransform _speechBubble;
    private CanvasGroup _speechCg;
    private TMPro.TextMeshProUGUI _speechText;
    private Coroutine _speechRoutine;

    // 底部行
    private TMPro.TextMeshProUGUI _undoSubText;
    private Image _undoImage;
    private Image _poolImage;
    private RectTransform _poolRect;
    private CanvasGroup _poolGlow;

    // 特技池弹窗（矢量对话框：九宫格 Sliced 渲染 + 行模板克隆拼装）
    private GameObject _specialPanel;
    private RectTransform _specialListRoot;
    private TMPro.TextMeshProUGUI _specialFreeHint;
    private TMPro.TextMeshProUGUI _specialCountText;
    private readonly List<GameObject> _specialRows = new List<GameObject>();
    private GameObject _specialRowTemplate;   // 列表行模板（隐藏，刷新时克隆拼装）
    private RectTransform _specialShadowRT;   // 白卡柔和投影垫层（同 GameOver）
    private RectTransform _specialCardRT;     // 白色大圆角卡片（同 GameOver）
    private RectTransform _specialTitleRT;
    private RectTransform _specialDividerRT;
    private RectTransform _specialCountRT;
    private RectTransform _specialFreeHintRT;
    private RectTransform _specialCloseRT;
    private float _specialCardW;

    // 棋盘闪烁按钮（对齐微信版 drawSpecialPending：磨砂白方形 + 闪电 + 方形周长倒计时进度条）
    private GameObject _pendingBtn;
    private RectTransform _pendingRT;
    private CanvasGroup _pendingCg;                          // 整体淡出
    private Image _pendingBg;                                // 磨砂白按钮主体（+ 点击 target）
    private Image _pendingGlow;                              // 淡金外发光
    private Image _pendingBolt;                              // 闪电图标
    private TMPro.TextMeshProUGUI _pendingText;
    private readonly Image[] _pendingTrack = new Image[4];   // 轨道四边（白，垫底）
    private readonly Image[] _pendingProgress = new Image[4];// 进度四边（橙→红，覆盖轨道）

    // 难度弹窗
    private GameObject _difficultyPanel;

    // 回调（由 GameManager 注入）
    private System.Action _onUndo;
    private System.Action _onGameOverUndo;
    private System.Action _onPoolClicked;
    private System.Action<SpecialSystem.SpecialItem> _onSpecialItemUse;
    private System.Action<int> _onDifficultySelected;

    // 弹窗回调
    private System.Action _restartCallback;
    private System.Action _continueCallback;

    private RectTransform _soundBtnRT;
    private RectTransform _magicGirlRT;

    private static readonly Color TitleTextColor = new Color(0.52f, 0.43f, 0.33f);           // 深糖棕
    private static readonly Color BtnBrown = new Color(0.68f, 0.52f, 0.38f);                 // 焦糖棕（糖果风）
    private static readonly Color BtnOrange = new Color(0.98f, 0.72f, 0.42f);                // 蜜橘橙（糖果风）
    private static readonly Color BtnGray = new Color(0.90f, 0.86f, 0.81f);                  // 奶灰（禁用）
    private static readonly Color ScoreBoxBrown = new Color(0.80f, 0.68f, 0.54f);             // 浅焦糖（SCORE 框底色）

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
    }

    // ========== 顶部：浣熊标题 + 分数 ==========

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

        var cat = CreateCatTitle(topRT, boardWidth);
        cat.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -8);

        _scoreText = CreateScoreLabel(topRT, "Score", 0, "0");
        _bestScoreText = CreateScoreLabel(topRT, "Best", 1, "0");
        _moveCountText = CreateScoreLabel(topRT, "Moves", 2, "0");

        CreateMagicGirl(_canvas.transform, boardWidth, boardBottom);
        CreateMagicSpeechBubble(_magicGirlRT);
    }

    private GameObject CreateCatTitle(RectTransform parent, float boardWidth)
    {
        float logoW = boardWidth * 0.67f;
        float logoH = logoW * 0.27f;

        var root = new GameObject("Cat", typeof(RectTransform));
        var rootRT = root.GetComponent<RectTransform>();
        rootRT.SetParent(parent, false);
        rootRT.anchorMin = new Vector2(0.5f, 1);
        rootRT.anchorMax = new Vector2(0.5f, 1);
        rootRT.sizeDelta = new Vector2(logoW, logoH);

        // MOVES 框中心 x（与 CreateScoreLabel 完全同源），保证橘猫垂直中心线对齐
        float gap = 10;
        float btnWidth = (boardWidth - gap * 2) / 3f;
        float startX = -boardWidth / 2 + btnWidth / 2;
        float movesX = startX + 2f * (btnWidth + gap);

        // 透明背景猫咪 Logo（Assets/Resources/CatLogo.png）：MOVES 正上方，中心线对齐
        var catGO = new GameObject("CatImage", typeof(RectTransform), typeof(Image));
        var catRT = catGO.GetComponent<RectTransform>();
        catRT.SetParent(rootRT, false);
        catRT.anchorMin = new Vector2(0.5f, 0.5f);
        catRT.anchorMax = new Vector2(0.5f, 0.5f);
        catRT.sizeDelta = new Vector2(300, 508); // 图比例 560:944（放大 2 倍）
        catRT.anchoredPosition = new Vector2(movesX, 161);
        var catImg = catGO.GetComponent<Image>();
        catImg.sprite = CreateSprite("CatLogo");
        catImg.preserveAspect = true;
        catImg.raycastTarget = false;

        return root;
    }

    /// <summary>魔法少女装饰：UNDO 按钮正下方，纯展示不拦截操作，高度自适应防出屏</summary>
    private void CreateMagicGirl(Transform canvas, float boardWidth, float boardBottom)
    {
        float undoW = boardWidth * 0.34f;
        float undoCenterX = -boardWidth / 2f + undoW / 2f;   // UNDO 按钮中心 x
        float undoBottomY = boardBottom - 170f;              // UNDO 按钮底边 y（canvas 中心坐标）
        float halfH = canvas.GetComponent<RectTransform>().sizeDelta.y / 2f;
        float avail = undoBottomY + halfH - 12f;             // 按钮底到屏底的可用高度（留 12px 边距）
        float h = Mathf.Min(400f, Mathf.Max(60f, avail));    // 高度自适应，上限 400（放大 1 倍）
        float w = h * (560f / 944f);                         // 图比例 560:944

        var go = new GameObject("MagicGirl", typeof(RectTransform), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(canvas, false);
        _magicGirlRT = rt;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(undoCenterX, undoBottomY - h / 2f);
        var img = go.GetComponent<Image>();
        img.sprite = CreateSprite("MagicGirl");
        img.preserveAspect = true;
        img.raycastTarget = false;
    }

    /// <summary>UNDO 点击特效：魔法少女发出糖果紫闪电击中 MOVES 框</summary>
    private void PlayUndoLightning()
    {
        if (_magicGirlRT == null || _moveCountText == null) return;
        Vector3 from = _magicGirlRT.position
                       + new Vector3(0, _magicGirlRT.rect.height * 0.16f * _magicGirlRT.lossyScale.y, 0);
        Vector2 to = _moveCountText.rectTransform.position;
        var fx = LightningEffect.Play(_canvas.transform, from, to);
        fx._moveCountText = _moveCountText.rectTransform;
    }

    /// <summary>
    /// 魔法少女油画对话框：提示语不再棋盘上浮动，改为复古油画框对话气泡，
    /// 显示在魔法少女右侧，文字纯黑。默认隐藏（alpha 0），ShowFloatText 时淡入淡出。
    /// </summary>
    private void CreateMagicSpeechBubble(RectTransform magicGirlRT)
    {
        // 外层：深棕复古油画框
        var outerGO = new GameObject("SpeechBubble", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        var outerRT = outerGO.GetComponent<RectTransform>();
        outerRT.SetParent(_canvas.transform, false);
        outerRT.anchorMin = new Vector2(0.5f, 0.5f);
        outerRT.anchorMax = new Vector2(0.5f, 0.5f);
        outerRT.sizeDelta = new Vector2(544, 272);   // 放大一倍
        // 对话完整移到魔法少女右侧，按实际尺寸留 40px 间隙，绝不遮住魔法少女
        float bx = magicGirlRT.anchoredPosition.x
                   + magicGirlRT.rect.width * 0.5f
                   + outerRT.sizeDelta.x * 0.5f
                   + 40f;
        outerRT.anchoredPosition = new Vector2(bx, magicGirlRT.anchoredPosition.y);
        var outerImg = outerGO.GetComponent<Image>();
        outerImg.sprite = CreateRoundedRectSprite(544, 272, 32);
        outerImg.color = new Color(0.31f, 0.22f, 0.15f);   // 深棕油画框
        outerImg.type = Image.Type.Sliced;
        outerImg.raycastTarget = false;
        _speechCg = outerGO.GetComponent<CanvasGroup>();
        _speechCg.alpha = 0f;
        _speechCg.blocksRaycasts = false;
        _speechBubble = outerRT;

        // 内层：米白纸面
        var innerGO = new GameObject("Paper", typeof(RectTransform), typeof(Image));
        var innerRT = innerGO.GetComponent<RectTransform>();
        innerRT.SetParent(outerRT, false);
        innerRT.anchorMin = new Vector2(0.5f, 0.5f);
        innerRT.anchorMax = new Vector2(0.5f, 0.5f);
        innerRT.sizeDelta = new Vector2(534, 262);   // 边框再细：每边 5px（原 10px）
        var innerImg = innerGO.GetComponent<Image>();
        innerImg.sprite = CreateRoundedRectSprite(534, 262, 28);
        innerImg.color = new Color(0.99f, 0.97f, 0.91f);   // 米白纸面
        innerImg.type = Image.Type.Sliced;
        innerImg.raycastTarget = false;

        // 文字：纯黑
        var textGO = new GameObject("Text", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
        var textRT = textGO.GetComponent<RectTransform>();
        textRT.SetParent(innerRT, false);
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.sizeDelta = Vector2.zero;
        textRT.offsetMin = new Vector2(28, 20);
        textRT.offsetMax = new Vector2(-28, -20);
        var tmp = textGO.GetComponent<TMPro.TextMeshProUGUI>();
        tmp.text = "";
        tmp.fontSize = 44;      // 放大一倍
        tmp.fontStyle = TMPro.FontStyles.Bold;
        tmp.color = Color.black;   // 纯黑文字
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.textWrappingMode = TMPro.TextWrappingModes.Normal;
        tmp.raycastTarget = false;
        _speechText = tmp;

        // 气泡尾巴：两个空心圆圈（漫画对话框风格）一大一小，沿 3 点方向（水平线）排成一条线；
        // 大环压在框左缘，小环在其左侧同一直线上逐级缩小，指向左侧的魔法少女
        var tailBigGO = new GameObject("TailBig", typeof(RectTransform), typeof(Image));
        var tailBigRT = tailBigGO.GetComponent<RectTransform>();
        tailBigRT.SetParent(outerRT, false);
        tailBigRT.anchorMin = new Vector2(0f, 0.5f);
        tailBigRT.anchorMax = new Vector2(0f, 0.5f);
        tailBigRT.sizeDelta = new Vector2(46, 46);
        tailBigRT.anchoredPosition = new Vector2(-20, 0);
        var tailBigImg = tailBigGO.GetComponent<Image>();
        tailBigImg.sprite = CreateRingSprite(46, 5);   // 空心圆环（大）
        tailBigImg.color = new Color(0.31f, 0.22f, 0.15f);   // 与油画框同深棕
        tailBigImg.raycastTarget = false;

        var tailSmallGO = new GameObject("TailSmall", typeof(RectTransform), typeof(Image));
        var tailSmallRT = tailSmallGO.GetComponent<RectTransform>();
        tailSmallRT.SetParent(outerRT, false);
        tailSmallRT.anchorMin = new Vector2(0f, 0.5f);
        tailSmallRT.anchorMax = new Vector2(0f, 0.5f);
        tailSmallRT.sizeDelta = new Vector2(26, 26);
        tailSmallRT.anchoredPosition = new Vector2(-62, 0);   // 大环左缘外再向左，与大环同一水平线
        var tailSmallImg = tailSmallGO.GetComponent<Image>();
        tailSmallImg.sprite = CreateRingSprite(26, 4);   // 空心圆环（小）
        tailSmallImg.color = new Color(0.31f, 0.22f, 0.15f);
        tailSmallImg.raycastTarget = false;
    }

    private IEnumerator SpeechRoutine(float duration)
    {
        // 淡入
        float t = 0f;
        while (t < 0.14f)
        {
            t += Time.unscaledDeltaTime;
            if (_speechCg != null) _speechCg.alpha = Mathf.Clamp01(t / 0.14f);
            yield return null;
        }
        if (_speechCg != null) _speechCg.alpha = 1f;

        // 停留
        yield return new WaitForSecondsRealtime(Mathf.Max(0.4f, duration));

        // 淡出
        t = 0f;
        while (t < 0.3f)
        {
            t += Time.unscaledDeltaTime;
            if (_speechCg != null) _speechCg.alpha = Mathf.Clamp01(1f - t / 0.3f);
            yield return null;
        }
        if (_speechCg != null) _speechCg.alpha = 0f;
        _speechRoutine = null;
    }

    private Sprite CreateSprite(string resName)
    {
        var tex = Resources.Load<Texture2D>(resName);
        if (tex == null)
        {
            Debug.LogWarning("[ScoreDisplay] " + resName + ".png not found in Assets/Resources");
            return null;
        }
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
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
        float undoW = usable * 0.34f;
        float poolW = usable - undoW - gap * 2;
        float x = -boardWidth / 2;

        // ---- 后悔按钮（胶囊，带免费次数副标题） ----
        var undoBtnGO = new GameObject("UndoBtn", typeof(Image), typeof(Button), typeof(CanvasGroup));
        var undoRT = undoBtnGO.GetComponent<RectTransform>();
        undoRT.SetParent(bottomRT, false);
        undoRT.anchorMin = new Vector2(0.5f, 1);
        undoRT.anchorMax = new Vector2(0.5f, 1);
        undoRT.sizeDelta = new Vector2(undoW, 101);
        undoRT.anchoredPosition = new Vector2(x + undoW / 2, 0);
        _undoImage = undoBtnGO.GetComponent<Image>();
        _undoImage.sprite = CreateRoundedRectSprite(undoW, 101, 16);
        _undoImage.color = ScoreBoxBrown; // 与 SCORE 框同色（浅焦糖）
        _undoImage.type = Image.Type.Sliced;
        var undoBtn = undoBtnGO.GetComponent<Button>();
        undoBtn.targetGraphic = _undoImage;
        undoBtn.onClick.AddListener(() => _onUndo?.Invoke());
        undoBtn.onClick.AddListener(PlayUndoLightning);

        var undoTextGO = new GameObject("Text", typeof(TMPro.TextMeshProUGUI));
        var undoTextRT = undoTextGO.GetComponent<RectTransform>();
        undoTextRT.SetParent(undoRT, false);
        undoTextRT.anchorMin = Vector2.zero;
        undoTextRT.anchorMax = Vector2.one;
        undoTextRT.sizeDelta = Vector2.zero;
        undoTextRT.offsetMin = new Vector2(4, 30);
        undoTextRT.offsetMax = new Vector2(-4, -4);
        var undoTMP = undoTextGO.GetComponent<TMPro.TextMeshProUGUI>();
        undoTMP.text = "UNDO";
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
        _poolRect.sizeDelta = new Vector2(poolW, 101);
        _poolRect.anchoredPosition = new Vector2(x + poolW / 2, 0);
        _poolImage = poolGO.GetComponent<Image>();
        _poolImage.sprite = CreateRoundedRectSprite(poolW, 101, 16);
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
        boltTMP.text = ">";
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
        poolTMP.text = "BOOST";
        poolTMP.fontSize = 26;
        poolTMP.fontStyle = TMPro.FontStyles.Bold;
        poolTMP.alignment = TMPro.TextAlignmentOptions.Center;
        poolTMP.color = new Color(0.45f, 0.38f, 0.30f);   // 深棕（奶灰底）
        poolTMP.raycastTarget = false;
    }

    /// <summary>刷新特技池入口条：库存 > 0 时金色 + 呼吸发光，否则置灰</summary>
    public void UpdateSpecialPoolBar(int count, bool specialButtonPlaced = false)
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
        _undoImage.color = canUndo ? ScoreBoxBrown : BtnGray;
        if (_undoSubText == null) return;
        _undoSubText.text = !canUndo
            ? ""
            : (freeLeft > 0 ? $"FREE x{freeLeft}" : "WATCH AD");
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
        img.color = ScoreBoxBrown; // 浅焦糖（糖果棕）
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

    // ========== 难度选择弹窗（经典 4x4 / 挑战 5x5） ==========

    private void CreateDifficultyDialog()
    {
        _difficultyPanel = CreateOverlay("DifficultyPanel", true);
        _difficultyPanel.GetComponent<Image>().color = new Color(0.20f, 0.13f, 0.08f, 0.55f);

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
        titleTMP.text = "SELECT GRID";
        titleTMP.fontSize = 44;
        titleTMP.fontStyle = TMPro.FontStyles.Bold;
        titleTMP.alignment = TMPro.TextAlignmentOptions.Center;
        titleTMP.color = TitleTextColor;

        // 两个难度按钮（上下排列）
        float btnW = panelW - 80;
        float btnH = 110;
        float vgap = 26;
        float centerY = -30;
        CreateDifficultyButton(panelRT, "Btn4", "CLASSIC 4x4", 4, new Vector2(0, centerY + (btnH + vgap) / 2f), btnW, btnH);
        CreateDifficultyButton(panelRT, "Btn5", "CHALLENGE 5x5", 5, new Vector2(0, centerY - (btnH + vgap) / 2f), btnW, btnH);

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
        textRT.offsetMin = new Vector2(0, 34);
        textRT.offsetMax = new Vector2(0, -4);
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
        subRT.offsetMin = new Vector2(0, 0);
        subRT.offsetMax = new Vector2(0, -52);
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
            kvp.Value.text = kvp.Key == selected ? "LAST USED" : "";
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
        // 不做蒙版：全屏 Image 转全透明（仅保留射线拦截，防止误触棋盘），背景棋盘清晰可见（同 GameOver）
        _specialPanel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);

        float panelW = Mathf.Min(620, _canvas.GetComponent<RectTransform>().rect.width - 64);
        _specialCardW = panelW;
        float initH = 746f;   // 初始高度（LayoutSpecialPanel 按行数重算）

        // 柔和投影垫层（同 GameOver）
        var shadow = new GameObject("Shadow", typeof(Image));
        _specialShadowRT = shadow.GetComponent<RectTransform>();
        _specialShadowRT.SetParent(_specialPanel.transform, false);
        _specialShadowRT.anchorMin = new Vector2(0.5f, 0.5f);
        _specialShadowRT.anchorMax = new Vector2(0.5f, 0.5f);
        _specialShadowRT.sizeDelta = new Vector2(panelW + 18, initH + 18);
        _specialShadowRT.anchoredPosition = new Vector2(0, -8);
        var shadowImg = shadow.GetComponent<Image>();
        shadowImg.sprite = CreateSlicedRoundedSprite(128, 44);
        shadowImg.color = new Color(0.15f, 0.10f, 0.25f, 0.16f);
        shadowImg.type = Image.Type.Sliced;
        shadowImg.raycastTarget = false;

        // 白色大圆角卡片（同 GameOver）
        var card = new GameObject("Card", typeof(Image));
        _specialCardRT = card.GetComponent<RectTransform>();
        _specialCardRT.SetParent(_specialPanel.transform, false);
        _specialCardRT.anchorMin = new Vector2(0.5f, 0.5f);
        _specialCardRT.anchorMax = new Vector2(0.5f, 0.5f);
        _specialCardRT.sizeDelta = new Vector2(panelW, initH);
        _specialCardRT.anchoredPosition = Vector2.zero;
        var cardImg = card.GetComponent<Image>();
        cardImg.sprite = CreateSlicedRoundedSprite(128, 44);
        cardImg.color = Color.white;
        cardImg.type = Image.Type.Sliced;
        cardImg.raycastTarget = false;

        // 糖果绿标题（同 GameOver 标题风格）
        var title = new GameObject("Title", typeof(TMPro.TextMeshProUGUI));
        _specialTitleRT = title.GetComponent<RectTransform>();
        _specialTitleRT.SetParent(_specialCardRT, false);
        _specialTitleRT.anchorMin = new Vector2(0, 0.5f);
        _specialTitleRT.anchorMax = new Vector2(1, 0.5f);
        _specialTitleRT.sizeDelta = new Vector2(0, 64);
        _specialTitleRT.anchoredPosition = new Vector2(0, initH / 2f - 62);
        var titleTMP = title.GetComponent<TMPro.TextMeshProUGUI>();
        titleTMP.text = "BOOST POOL";
        titleTMP.fontSize = 42;
        titleTMP.fontStyle = TMPro.FontStyles.Bold;
        titleTMP.alignment = TMPro.TextAlignmentOptions.Center;
        titleTMP.color = new Color(0.36f, 0.70f, 0.50f);   // 糖果绿（同 GameOver 标题）

        // 浅蓝细分隔线（同 GameOver）
        var divider = new GameObject("Divider", typeof(Image));
        _specialDividerRT = divider.GetComponent<RectTransform>();
        _specialDividerRT.SetParent(_specialCardRT, false);
        _specialDividerRT.anchorMin = new Vector2(0.5f, 0.5f);
        _specialDividerRT.anchorMax = new Vector2(0.5f, 0.5f);
        _specialDividerRT.sizeDelta = new Vector2(panelW - 140, 3);
        _specialDividerRT.anchoredPosition = new Vector2(0, initH / 2f - 112);
        var divImg = divider.GetComponent<Image>();
        divImg.color = new Color(0.72f, 0.86f, 0.96f);   // 浅蓝细线（同 GameOver）
        divImg.raycastTarget = false;

        // 深棕计数行（同 GameOver 统计行配色）
        var countGO = new GameObject("Count", typeof(TMPro.TextMeshProUGUI));
        _specialCountRT = countGO.GetComponent<RectTransform>();
        _specialCountRT.SetParent(_specialCardRT, false);
        _specialCountRT.anchorMin = new Vector2(0, 0.5f);
        _specialCountRT.anchorMax = new Vector2(1, 0.5f);
        _specialCountRT.sizeDelta = new Vector2(0, 40);
        _specialCountRT.anchoredPosition = new Vector2(0, initH / 2f - 154);
        _specialCountText = countGO.GetComponent<TMPro.TextMeshProUGUI>();
        _specialCountText.alignment = TMPro.TextAlignmentOptions.Center;
        _specialCountText.fontSize = 24;
        _specialCountText.color = new Color(0.45f, 0.35f, 0.27f);   // 深棕（同 GameOver 统计色）

        // 糖果绿免费提示（列表下方，同标题绿）
        var hintGO = new GameObject("FreeHint", typeof(TMPro.TextMeshProUGUI));
        _specialFreeHintRT = hintGO.GetComponent<RectTransform>();
        _specialFreeHintRT.SetParent(_specialCardRT, false);
        _specialFreeHintRT.anchorMin = new Vector2(0.5f, 0.5f);
        _specialFreeHintRT.anchorMax = new Vector2(0.5f, 0.5f);
        _specialFreeHintRT.sizeDelta = new Vector2(panelW - 140, 34);
        _specialFreeHintRT.anchoredPosition = new Vector2(0, -(initH / 2f - 40));
        _specialFreeHint = hintGO.GetComponent<TMPro.TextMeshProUGUI>();
        _specialFreeHint.text = "";
        _specialFreeHint.alignment = TMPro.TextAlignmentOptions.Center;
        _specialFreeHint.fontSize = 22;
        _specialFreeHint.fontStyle = TMPro.FontStyles.Bold;
        _specialFreeHint.color = new Color(0.36f, 0.70f, 0.50f);   // 糖果绿

        // 列表容器（宽度 = 行宽 400，行水平居中）
        _specialListRoot = new GameObject("List", typeof(RectTransform)).GetComponent<RectTransform>();
        _specialListRoot.SetParent(_specialCardRT, false);
        _specialListRoot.anchorMin = new Vector2(0.5f, 0.5f);
        _specialListRoot.anchorMax = new Vector2(0.5f, 0.5f);
        _specialListRoot.sizeDelta = new Vector2(400, 550);
        _specialListRoot.anchoredPosition = new Vector2(0, initH / 2f - 154 - 275);

        // 关闭按钮（右上角内：极淡深棕圆底 + 深棕 ✕，低调不抢白卡）
        var closeGO = new GameObject("Close", typeof(Image), typeof(Button));
        _specialCloseRT = closeGO.GetComponent<RectTransform>();
        _specialCloseRT.SetParent(_specialCardRT, false);
        _specialCloseRT.anchorMin = new Vector2(0.5f, 0.5f);
        _specialCloseRT.anchorMax = new Vector2(0.5f, 0.5f);
        _specialCloseRT.sizeDelta = new Vector2(44, 44);
        _specialCloseRT.anchoredPosition = new Vector2(panelW / 2f - 48, initH / 2f - 48);   // 整体在右上角圆角内侧（卡圆角44 + 钮半径22）
        var closeImg = closeGO.GetComponent<Image>();
        closeImg.sprite = CreateCircleSprite(44);
        closeImg.color = new Color(0.45f, 0.35f, 0.27f, 0.10f);   // 极淡深棕圆底（点击区）
        var closeText = new GameObject("X", typeof(TMPro.TextMeshProUGUI));
        var closeTextRT = closeText.GetComponent<RectTransform>();
        closeTextRT.SetParent(_specialCloseRT, false);
        closeTextRT.anchorMin = Vector2.zero;
        closeTextRT.anchorMax = Vector2.one;
        closeTextRT.sizeDelta = Vector2.zero;
        var closeTMP = closeText.GetComponent<TMPro.TextMeshProUGUI>();
        closeTMP.text = "✕";
        closeTMP.fontSize = 26;
        closeTMP.fontStyle = TMPro.FontStyles.Bold;
        closeTMP.alignment = TMPro.TextAlignmentOptions.Center;
        closeTMP.color = new Color(0.45f, 0.35f, 0.27f);   // 深棕 ✕（同 GameOver 文字色）
        closeGO.GetComponent<Button>().onClick.AddListener(CloseSpecialPanel);

        // 行模板：GameOver 按钮风胶囊（金黄底 + 两端深橙菱形 + 深棕粗体），刷新时逐行克隆拼装
        _specialRowTemplate = BuildSpecialRow("RowTemplate", 400f, 60f);
        _specialRowTemplate.SetActive(false);

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
            _specialCountText.text = $"Stock {pool.Count} / {SpecialSystem.SpecialCap}";

        if (_specialFreeHint != null)
            _specialFreeHint.text = freeAvailable ? "First use this game: FREE" : "";

        if (_specialListRoot == null) return;
        if (_specialRowTemplate == null) return;   // 极端时序保护：模板未构建时跳过列表（空状态仍显示）

        float rowH = 60;
        float gap = 10;
        for (int i = 0; i < pool.Count; i++)
        {
            var item = pool[i];
            var row = Object.Instantiate(_specialRowTemplate, _specialListRoot, false);
            row.name = $"Row_{i}";
            row.SetActive(true);
            var rowRT = row.GetComponent<RectTransform>();
            rowRT.anchorMin = new Vector2(0, 1);
            rowRT.anchorMax = new Vector2(0, 1);
            rowRT.pivot = new Vector2(0, 0.5f);   // 左边缘对齐锚点，否则默认 pivot(0.5) 会整体偏出列表左边
            rowRT.sizeDelta = new Vector2(_specialListRoot.sizeDelta.x, rowH);
            rowRT.anchoredPosition = new Vector2(0, -(i * (rowH + gap) + rowH / 2));

            row.transform.Find("Text").GetComponent<TMPro.TextMeshProUGUI>().text = $"USE x{item.value}";
            row.transform.Find("Idx").GetComponent<TMPro.TextMeshProUGUI>().text = $"#{i + 1}";

            var btn = row.GetComponent<Button>();
            btn.onClick.RemoveAllListeners();
            var clicked = item;
            btn.onClick.AddListener(() => _onSpecialItemUse?.Invoke(clicked));

            _specialRows.Add(row);
        }

        if (pool.Count == 0)
        {
            var empty = new GameObject("Empty", typeof(TMPro.TextMeshProUGUI));
            var emptyRT = empty.GetComponent<RectTransform>();
            emptyRT.SetParent(_specialListRoot, false);
            emptyRT.anchorMin = new Vector2(0.5f, 0.5f);
            emptyRT.anchorMax = new Vector2(0.5f, 0.5f);
            emptyRT.sizeDelta = new Vector2(400, 60);
            emptyRT.anchoredPosition = Vector2.zero;
            var emptyTMP = empty.GetComponent<TMPro.TextMeshProUGUI>();
            emptyTMP.text = "No boosts yet. Merge 512 to get one!";
            emptyTMP.fontSize = 24;
            emptyTMP.alignment = TMPro.TextAlignmentOptions.Center;
            emptyTMP.color = new Color(0.45f, 0.35f, 0.27f);   // 深棕（同 GameOver 文字色）
            _specialRows.Add(empty);
        }

        LayoutSpecialPanel(pool.Count);
    }

    /// <summary>构建一行特技池列表（模板）：GameOver 按钮风——金黄胶囊 + 两端深橙菱形 + 深棕粗体字，刷新时逐行克隆拼装</summary>
    private GameObject BuildSpecialRow(string name, float rowW, float rowH)
    {
        var row = new GameObject(name, typeof(Image), typeof(Button));
        var rowRT = row.GetComponent<RectTransform>();
        rowRT.pivot = new Vector2(0, 0.5f);
        rowRT.sizeDelta = new Vector2(rowW, rowH);

        var rowImg = row.GetComponent<Image>();
        rowImg.color = new Color(0.98f, 0.80f, 0.32f);   // 糖果黄底（同 GameOver REVIVE）
        rowImg.sprite = CreateSlicedRoundedSprite(96, 30);   // 九宫格胶囊
        rowImg.type = Image.Type.Sliced;

        // 两端菱形浮雕（同 GameOver 按钮）
        CreateDiamond(rowRT, "GemL", new Vector2(-(rowW / 2f - 46), 0), 22, new Color(0.85f, 0.62f, 0.18f));
        CreateDiamond(rowRT, "GemR", new Vector2(rowW / 2f - 46, 0), 22, new Color(0.85f, 0.62f, 0.18f));

        // 左排序徽章（#序号，深橙浮雕感，浮在文字左侧）
        var idx = new GameObject("Idx", typeof(TMPro.TextMeshProUGUI));
        var idxRT = idx.GetComponent<RectTransform>();
        idxRT.SetParent(rowRT, false);
        idxRT.anchorMin = new Vector2(0.5f, 0.5f);
        idxRT.anchorMax = new Vector2(0.5f, 0.5f);
        idxRT.sizeDelta = new Vector2(120, rowH);
        idxRT.anchoredPosition = new Vector2(-(rowW / 2f - 138), 0);
        var idxTMP = idx.GetComponent<TMPro.TextMeshProUGUI>();
        idxTMP.fontSize = 22;
        idxTMP.fontStyle = TMPro.FontStyles.Bold;
        idxTMP.color = new Color(0.85f, 0.62f, 0.18f);   // 深橙（比黄底深一档）
        idxTMP.alignment = TMPro.TextAlignmentOptions.Center;

        // 主文字：USE x{value}（深棕粗体，同 GameOver 按钮文字）
        var text = new GameObject("Text", typeof(TMPro.TextMeshProUGUI));
        var textRT = text.GetComponent<RectTransform>();
        textRT.SetParent(rowRT, false);
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.sizeDelta = Vector2.zero;
        var textTMP = text.GetComponent<TMPro.TextMeshProUGUI>();
        textTMP.fontSize = 26;
        textTMP.fontStyle = TMPro.FontStyles.Bold;
        textTMP.color = new Color(0.42f, 0.30f, 0.20f);   // 深棕（同 GameOver 按钮字）
        textTMP.alignment = TMPro.TextAlignmentOptions.Center;
        textTMP.raycastTarget = false;

        var btn = row.GetComponent<Button>();
        btn.targetGraphic = rowImg;

        return row;
    }

    /// <summary>按行数动态布局白卡弹窗：标题/分隔线/计数/列表/提示/关闭全部随高度联动（同 GameOver 卡片结构）</summary>
    private void LayoutSpecialPanel(int rows)
    {
        const float rowH = 60f, gap = 10f;
        float listH = rows > 0 ? rows * rowH + (rows - 1) * gap : 96f;
        float panelH = 150f + listH + 46f;

        _specialCardRT.sizeDelta = new Vector2(_specialCardW, panelH);
        _specialShadowRT.sizeDelta = new Vector2(_specialCardW + 18, panelH + 18);
        _specialShadowRT.anchoredPosition = new Vector2(0, -8);
        _specialTitleRT.anchoredPosition = new Vector2(0, panelH / 2f - 62);
        _specialDividerRT.anchoredPosition = new Vector2(0, panelH / 2f - 112);
        _specialCountRT.anchoredPosition = new Vector2(0, panelH / 2f - 154);
        _specialListRoot.sizeDelta = new Vector2(400, listH);
        _specialListRoot.anchoredPosition = new Vector2(0, panelH / 2f - 154 - listH / 2f);
        _specialFreeHintRT.anchoredPosition = new Vector2(0, -(panelH / 2f - 40));
        _specialCloseRT.anchoredPosition = new Vector2(_specialCardW / 2f - 48, panelH / 2f - 48);   // 同创建：整个圆形在圆角内侧
    }

    // ========== 棋盘闪烁按钮（对齐微信版 drawSpecialPending） ==========

    private System.Action _onPendingClicked;

    public void BindPendingButton(System.Action onClick)
    {
        _onPendingClicked = onClick;
    }

    /// <summary>
    /// 每帧刷新闪烁按钮：无 pending 则销毁；有则创建/跟随位置/更新倒计时与颜色。
    /// 视觉对齐微信版 drawSpecialPending：
    ///   - 磨砂白直角方块 + 淡金外发光
    ///   - 沿正方形周长顺时针收缩的倒计时进度条（橙→红）
    ///   - 180ms 弹大进入 + ±4% 呼吸脉动
    ///   - 倒计时结束 260ms 渐隐淡出
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
            float band = Mathf.Max(6f, Mathf.Min(8f, Mathf.Round(size * 0.12f)));

            // 根容器（纯 RectTransform + CanvasGroup，无 Image）
            _pendingBtn = new GameObject("PendingBoost", typeof(CanvasGroup), typeof(RectTransform));
            _pendingRT = _pendingBtn.GetComponent<RectTransform>();
            _pendingRT.SetParent(board.BoardContainer, false);
            _pendingRT.anchorMin = new Vector2(0, 1);
            _pendingRT.anchorMax = new Vector2(0, 1);
            _pendingRT.sizeDelta = new Vector2(size, size);
            _pendingRT.pivot = new Vector2(0.5f, 0.5f);
            _pendingCg = _pendingBtn.GetComponent<CanvasGroup>();

            // ---- 外发光（JS: rgba(237,194,46,0.18) 方块外扩 6px） ----
            _pendingGlow = CreateLayerImage("Glow", _pendingRT, size + 12f, size + 12f);
            _pendingGlow.color = new Color(0.9294f, 0.7608f, 0.1804f, 0.18f);

            // ---- 磨砂白按钮主体（JS: rgba(255,255,255,0.46)） ----
            var bgGO = new GameObject("Bg", typeof(Image));
            var bgRT = bgGO.GetComponent<RectTransform>();
            bgRT.SetParent(_pendingRT, false);
            bgRT.anchorMin = new Vector2(0, 0);
            bgRT.anchorMax = new Vector2(1, 1);
            bgRT.sizeDelta = Vector2.zero;
            _pendingBg = bgGO.GetComponent<Image>();
            _pendingBg.sprite = CreateSquareSprite(Mathf.RoundToInt(size));
            _pendingBg.color = new Color(1f, 1f, 1f, 0.46f);
            _pendingBg.raycastTarget = true;

            var btn = _pendingBtn.AddComponent<Button>();
            btn.targetGraphic = _pendingBg;
            btn.onClick.AddListener(() =>
            {
                if (pending == null) return;
                _onPendingClicked?.Invoke();
            });

            // ---- 轨道四边（JS: rgba(255,255,255,0.62)，压在按钮边框上） ----
            CreateEdgeImage("TrackTop", _pendingRT, size, band, Edge.Top, out _pendingTrack[0]);
            CreateEdgeImage("TrackRight", _pendingRT, band, size, Edge.Right, out _pendingTrack[1]);
            CreateEdgeImage("TrackBottom", _pendingRT, size, band, Edge.Bottom, out _pendingTrack[2]);
            CreateEdgeImage("TrackLeft", _pendingRT, band, size, Edge.Left, out _pendingTrack[3]);

            // ---- 进度四边（Filled 模式覆盖轨道，对齐 JS strokeSquareProgress） ----
            CreateProgressEdge("ProgTop", _pendingRT, size, band, Edge.Top, Image.FillMethod.Horizontal, (int)Image.OriginHorizontal.Left, out _pendingProgress[0]);
            CreateProgressEdge("ProgRight", _pendingRT, band, size, Edge.Right, Image.FillMethod.Vertical, (int)Image.OriginVertical.Top, out _pendingProgress[1]);
            CreateProgressEdge("ProgBottom", _pendingRT, size, band, Edge.Bottom, Image.FillMethod.Horizontal, (int)Image.OriginHorizontal.Right, out _pendingProgress[2]);
            CreateProgressEdge("ProgLeft", _pendingRT, band, size, Edge.Left, Image.FillMethod.Vertical, (int)Image.OriginVertical.Bottom, out _pendingProgress[3]);

            // ---- 闪电图标（JS drawBolt: #D9730D，外高 40%，上移 10%） ----
            float boltH = size * 0.40f;
            float boltW = boltH * 0.66f;   // 闪电原始宽高比 ≈ 1.32/2.0
            var boltGO = new GameObject("Bolt", typeof(Image));
            var boltRT = boltGO.GetComponent<RectTransform>();
            boltRT.SetParent(_pendingRT, false);
            boltRT.anchorMin = boltRT.anchorMax = new Vector2(0.5f, 0.5f);
            boltRT.sizeDelta = new Vector2(boltW, boltH);
            boltRT.anchoredPosition = new Vector2(0, -size * 0.10f);
            _pendingBolt = boltGO.GetComponent<Image>();
            _pendingBolt.sprite = CreateBoltSprite(Mathf.RoundToInt(boltH));
            _pendingBolt.color = new Color(0.851f, 0.451f, 0.051f, 1f);
            _pendingBolt.raycastTarget = false;

            // ---- ×512 文案（JS: #5C4A3A，位于按钮中下方 +22%） ----
            var textGO = new GameObject("Text", typeof(TMPro.TextMeshProUGUI));
            var textRT = textGO.GetComponent<RectTransform>();
            textRT.SetParent(_pendingRT, false);
            textRT.anchorMin = textRT.anchorMax = new Vector2(0.5f, 0.5f);
            textRT.sizeDelta = new Vector2(size * 0.7f, size * 0.4f);
            textRT.anchoredPosition = new Vector2(0, size * 0.22f);
            _pendingText = textGO.GetComponent<TMPro.TextMeshProUGUI>();
            _pendingText.text = "×" + SpecialSystem.SpecialTriggerValue;
            _pendingText.fontSize = Mathf.Max(12, Mathf.RoundToInt(size * 0.20f));
            _pendingText.fontStyle = TMPro.FontStyles.Bold;
            _pendingText.alignment = TMPro.TextAlignmentOptions.Center;
            _pendingText.color = new Color(0.361f, 0.290f, 0.227f);
            _pendingText.raycastTarget = false;
        }

        // ========== 每帧更新 ==========

        // 位置跟随格子
        Vector2 pos = board.GetCellPosition(pending.gx, pending.gy);
        _pendingRT.anchoredPosition = pos;

        // 生命周期
        float elapsed = Time.unscaledTime - pending.spawnAt;
        float total = SpecialSystem.SpecialFloatMs / 1000f;
        float fadeTime = SpecialSystem.SpecialFadeMs / 1000f;
        float remain = Mathf.Max(0f, total - elapsed);
        float remain01 = Mathf.Clamp01(remain / total);

        // 出现放大（180ms 0.72→1.0）+ 呼吸脉动（JS: ±4% @ 周期 190ms）
        float appear = Mathf.Clamp01(elapsed / 0.18f);
        float pulse = 1f + Mathf.Sin(elapsed * 1000f / 190f) * 0.04f;
        float scale = pulse * (0.72f + 0.28f * appear);
        _pendingRT.localScale = new Vector3(scale, scale, 1f);

        // 淡出（超过 floatMs 后 fadeTime 内渐隐，JS: alpha=1-(elapsed-FLOAT)/FADE）
        if (_pendingCg != null)
        {
            float alpha = elapsed > total
                ? Mathf.Max(0f, 1f - (elapsed - total) / fadeTime)
                : 1f;
            _pendingCg.alpha = alpha;
        }

        // 方形倒计时进度：沿正方形周长（左上角起顺时针）收缩 — 对齐 JS strokeSquareProgress
        float side = _pendingRT.sizeDelta.x;
        float len = 4f * side * remain01;   // 剩余弧长
        float[] seg = new float[4];
        seg[0] = Mathf.Min(len, side);                         // 上边
        seg[1] = Mathf.Clamp(len - side, 0f, side);            // 右边
        seg[2] = Mathf.Clamp(len - 2f * side, 0f, side);       // 下边
        seg[3] = Mathf.Clamp(len - 3f * side, 0f, side);       // 左边

        bool danger = remain01 <= 0.30f;
        Color progColor = danger
            ? new Color(0.9098f, 0.3647f, 0.2275f)   // #E85D3A 红
            : new Color(0.9490f, 0.6000f, 0.2902f);  // #F2994A 橙

        for (int i = 0; i < 4; i++)
        {
            if (_pendingProgress[i] != null)
            {
                _pendingProgress[i].fillAmount = side > 0 ? seg[i] / side : 0f;
                _pendingProgress[i].color = progColor;
            }
        }
    }

    /// <summary>四边定位：Top / Right / Bottom / Left</summary>
    private enum Edge { Top, Right, Bottom, Left }

    /// <summary>创建纯色层（外发光等）</summary>
    private Image CreateLayerImage(string name, RectTransform parent, float w, float h)
    {
        var go = new GameObject(name, typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        var img = go.GetComponent<Image>();
        img.raycastTarget = false;
        return img;
    }

    /// <summary>创建轨道/进度条单边（纯色矩形）</summary>
    private void CreateEdgeImage(string name, RectTransform parent, float w, float h, Edge edge, out Image img)
    {
        var go = new GameObject(name, typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.sizeDelta = new Vector2(w, h);
        (rt.anchorMin, rt.anchorMax, rt.pivot, rt.anchoredPosition) = GetEdgeLayout(edge);
        img = go.GetComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0.62f);
        img.raycastTarget = false;
    }

    /// <summary>创建进度条单边（Filled 模式）</summary>
    private void CreateProgressEdge(string name, RectTransform parent, float w, float h, Edge edge, Image.FillMethod method, int fillOrigin, out Image img)
    {
        var go = new GameObject(name, typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.sizeDelta = new Vector2(w, h);
        (rt.anchorMin, rt.anchorMax, rt.pivot, rt.anchoredPosition) = GetEdgeLayout(edge);
        img = go.GetComponent<Image>();
        img.type = Image.Type.Filled;
        img.fillMethod = method;
        img.fillOrigin = fillOrigin;
        img.fillAmount = 1f;
        img.color = new Color(0.9490f, 0.6000f, 0.2902f, 1f);
        img.raycastTarget = false;
    }

    /// <summary>四边定位参数（相对根容器）</summary>
    private static (Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition) GetEdgeLayout(Edge edge)
    {
        switch (edge)
        {
            case Edge.Top:
                return (new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), Vector2.zero);
            case Edge.Right:
                return (new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), Vector2.zero);
            case Edge.Bottom:
                return (new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), Vector2.zero);
            default:  // Edge.Left
                return (new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), Vector2.zero);
        }
    }

    public void DestroyPendingButton()
    {
        if (_pendingBtn != null)
        {
            Destroy(_pendingBtn);
            _pendingBtn = null;
            _pendingRT = null;
            _pendingCg = null;
            _pendingBg = null;
            _pendingGlow = null;
            _pendingBolt = null;
            _pendingText = null;
            for (int i = 0; i < 4; i++)
            {
                _pendingTrack[i] = null;
                _pendingProgress[i] = null;
            }
        }
    }

    // ========== 提示语（魔法少女油画对话框，不再棋盘上浮动） ==========

    public void ShowFloatText(string text, float duration = 1.5f, int fontSize = 34)
    {
        if (_speechText == null || string.IsNullOrEmpty(text)) return;
        _speechText.text = text;
        _speechText.fontSize = fontSize;
        if (_speechRoutine != null) StopCoroutine(_speechRoutine);
        _speechRoutine = StartCoroutine(SpeechRoutine(duration));
    }

    // ========== 结算弹窗 ==========

    private void CreateGameOverOverlay()
    {
        _gameOverPanel = CreateOverlay("GameOverPanel", true);
        // 不做蒙版：全屏 Image 转全透明（仅保留射线拦截，防止误触棋盘），背景棋盘清晰可见
        _gameOverPanel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);

        // 卡片尺寸（Puzzle Complete! 风格：白色大圆角 + 柔和阴影）
        float panelW = Mathf.Min(620, _canvas.GetComponent<RectTransform>().rect.width - 64);
        float panelH = 600;

        // 柔和投影垫层
        var shadow = new GameObject("Shadow", typeof(Image));
        var shadowRT = shadow.GetComponent<RectTransform>();
        shadowRT.SetParent(_gameOverPanel.transform, false);
        shadowRT.anchorMin = new Vector2(0.5f, 0.5f);
        shadowRT.anchorMax = new Vector2(0.5f, 0.5f);
        shadowRT.sizeDelta = new Vector2(panelW + 18, panelH + 18);
        shadowRT.anchoredPosition = new Vector2(0, -8);
        var shadowImg = shadow.GetComponent<Image>();
        shadowImg.sprite = CreateRoundedRectSprite(Mathf.RoundToInt(panelW + 18), Mathf.RoundToInt(panelH + 18), 44);
        shadowImg.color = new Color(0.15f, 0.10f, 0.25f, 0.16f);
        shadowImg.type = Image.Type.Sliced;
        shadowImg.raycastTarget = false;

        // 白色大圆角卡片
        var card = new GameObject("Card", typeof(Image));
        var cardRT = card.GetComponent<RectTransform>();
        cardRT.SetParent(_gameOverPanel.transform, false);
        cardRT.anchorMin = new Vector2(0.5f, 0.5f);
        cardRT.anchorMax = new Vector2(0.5f, 0.5f);
        cardRT.sizeDelta = new Vector2(panelW, panelH);
        cardRT.anchoredPosition = Vector2.zero;
        var cardImg = card.GetComponent<Image>();
        cardImg.sprite = CreateRoundedRectSprite(Mathf.RoundToInt(panelW), Mathf.RoundToInt(panelH), 44);
        cardImg.color = Color.white;
        cardImg.type = Image.Type.Sliced;
        cardImg.raycastTarget = false;

        // 绿色标题
        var textGO = new GameObject("GameOverText", typeof(TMPro.TextMeshProUGUI));
        var textRT = textGO.GetComponent<RectTransform>();
        textRT.SetParent(cardRT, false);
        textRT.anchorMin = new Vector2(0, 0.5f);
        textRT.anchorMax = new Vector2(1, 0.5f);
        textRT.sizeDelta = new Vector2(0, 70);
        textRT.anchoredPosition = new Vector2(0, 236);
        var textTMP = textGO.GetComponent<TMPro.TextMeshProUGUI>();
        textTMP.text = "GAME OVER";
        textTMP.fontSize = 46;
        textTMP.fontStyle = TMPro.FontStyles.Bold;
        textTMP.color = new Color(0.36f, 0.70f, 0.50f);   // 糖果绿
        textTMP.alignment = TMPro.TextAlignmentOptions.Center;

        // 细浅蓝分隔线
        var divider = new GameObject("Divider", typeof(Image));
        var divRT = divider.GetComponent<RectTransform>();
        divRT.SetParent(cardRT, false);
        divRT.anchorMin = new Vector2(0.5f, 0.5f);
        divRT.anchorMax = new Vector2(0.5f, 0.5f);
        divRT.sizeDelta = new Vector2(panelW - 140, 3);
        divRT.anchoredPosition = new Vector2(0, 184);
        var divImg = divider.GetComponent<Image>();
        divImg.color = new Color(0.72f, 0.86f, 0.96f);   // 浅蓝细线
        divImg.raycastTarget = false;

        // 三行棕色统计（SCORE / BEST / MOVES）
        _gameOverScoreText = CreateGameOverStatLine(cardRT, "FinalScore", "SCORE", 148, 32);
        _gameOverBestText = CreateGameOverStatLine(cardRT, "FinalBest", "BEST", 108, 28);
        _gameOverMovesText = CreateGameOverStatLine(cardRT, "FinalMoves", "MOVES", 70, 28);

        // 三个胶囊糖果按钮（黄 / 青绿 / 浅蓝，深棕字，两端菱形浮雕）
        var reviveBtn = CreateGameOverButton(cardRT, "ReviveBtn", "REVIVE",
            new Vector2(0, -18), new Color(0.98f, 0.80f, 0.32f), new Color(0.85f, 0.62f, 0.18f));
        reviveBtn.SetActive(false);   // 无复活机会时隐藏（ShowGameOver 决定）
        _gameOverReviveBtnGO = reviveBtn;

        var undoBtn = CreateGameOverButton(cardRT, "UndoBtn", "UNDO",
            new Vector2(0, -92), new Color(0.42f, 0.78f, 0.74f), new Color(0.24f, 0.60f, 0.56f));
        undoBtn.GetComponent<Button>().onClick.AddListener(() =>
        {
            // 失败弹窗内悔棋救场：直接回撤上一局面并自动关闭弹窗继续玩（不走广告流程）
            _onGameOverUndo?.Invoke();
        });

        var restartBtn = CreateGameOverButton(cardRT, "RestartBtn", "RESTART",
            new Vector2(0, -166), new Color(0.62f, 0.80f, 0.95f), new Color(0.36f, 0.56f, 0.80f));
        _gameOverRestartBtn = restartBtn.GetComponent<Button>();
        // 静态绑定：点击后重新开始（ShowGameOver 会再覆盖一次，保证拿到最新回调）
        _gameOverRestartBtn.onClick.AddListener(() => _restartCallback?.Invoke());

        _gameOverPanel.SetActive(false);
    }

    /// <summary>结算弹窗统计行：标签 + 数值，深棕文字右对齐数值居中排版</summary>
    private TMPro.TextMeshProUGUI CreateGameOverStatLine(RectTransform parent, string name, string label, float y, float fontSize)
    {
        var go = new GameObject(name, typeof(TMPro.TextMeshProUGUI));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0, 0.5f);
        rt.anchorMax = new Vector2(1, 0.5f);
        rt.sizeDelta = new Vector2(0, 44);
        rt.anchoredPosition = new Vector2(0, y);
        var tmp = go.GetComponent<TMPro.TextMeshProUGUI>();
        tmp.text = label + " 0";
        tmp.fontSize = fontSize;
        tmp.fontStyle = TMPro.FontStyles.Bold;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.color = new Color(0.45f, 0.35f, 0.27f);   // 深棕（参考图统计色）
        return tmp;
    }

    /// <summary>旋转 45° 的方形小点，用于四角星点缀 / 按钮两端浮雕</summary>
    private GameObject CreateDiamond(Transform parent, string name, Vector2 pos, float size, Color color)
    {
        var go = new GameObject(name, typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, size);
        rt.anchoredPosition = pos;
        rt.localRotation = Quaternion.Euler(0f, 0f, 45f);
        var img = go.GetComponent<Image>();
        img.sprite = CreateRoundedRectSprite(Mathf.RoundToInt(size), Mathf.RoundToInt(size), 2);
        img.color = color;
        img.raycastTarget = false;
        return go;
    }

    /// <summary>Puzzle Complete! 风格胶囊糖果按钮：纯色 + 两端菱形浮雕 + 深棕粗体字</summary>
    private GameObject CreateGameOverButton(RectTransform parent, string name, string label, Vector2 pos, Color color, Color darkColor)
    {
        const float btnW = 280f, btnH = 64f;
        var go = new GameObject(name, typeof(Image), typeof(Button));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(btnW, btnH);
        rt.anchoredPosition = pos;

        var img = go.GetComponent<Image>();
        img.sprite = CreateRoundedRectSprite(Mathf.RoundToInt(btnW), Mathf.RoundToInt(btnH), 30);
        img.color = color;
        img.type = Image.Type.Sliced;

        // 两端菱形浮雕
        CreateDiamond(rt, "GemL", new Vector2(-(btnW / 2f - 42), 0), 22, darkColor);
        CreateDiamond(rt, "GemR", new Vector2(btnW / 2f - 42, 0), 22, darkColor);

        // 深棕粗体文字
        var textGO = new GameObject("Text", typeof(TMPro.TextMeshProUGUI));
        var textRT = textGO.GetComponent<RectTransform>();
        textRT.SetParent(rt, false);
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.sizeDelta = Vector2.zero;
        var textTMP = textGO.GetComponent<TMPro.TextMeshProUGUI>();
        textTMP.text = label;
        textTMP.fontSize = 27;
        textTMP.fontStyle = TMPro.FontStyles.Bold;
        textTMP.color = new Color(0.42f, 0.30f, 0.20f);   // 深棕字
        textTMP.alignment = TMPro.TextAlignmentOptions.Center;
        textTMP.raycastTarget = false;   // 防止文字挡按钮点击

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        return go;
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
        textTMP.text = "YOU WIN!";
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
        subTMP.text = "Reached 2048!";
        subTMP.fontSize = 30;
        subTMP.color = TitleTextColor;
        subTMP.alignment = TMPro.TextAlignmentOptions.Center;

        var continueBtn = CreateButton(_winPanel.transform, "ContinueBtn", "KEEP GOING", new Vector2(-140, -140));
        continueBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(250, 56);
        var restartBtn = CreateButton(_winPanel.transform, "RestartBtn", "RESTART", new Vector2(140, -140));
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
        img.color = new Color(0.98f, 0.97f, 0.94f, 0.85f);

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
        System.Action onUndo,
        System.Action onPoolClicked,
        System.Action<SpecialSystem.SpecialItem> onSpecialItemUse,
        System.Action<int> onDifficultySelected,
        System.Action onGameOverUndo)
    {
        _onUndo = onUndo;
        _onGameOverUndo = onGameOverUndo;
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

    public void ShowGameOver(long score, long bestScore, int moveCount, System.Action onRestart, bool canRevive, System.Action onRevive)
    {
        _restartCallback = onRestart;
        if (_gameOverPanel != null)
        {
            _gameOverPanel.SetActive(true);
            _gameOverPanel.transform.SetAsLastSibling();
            BringSoundButtonToFront();
            if (_gameOverScoreText != null)
                _gameOverScoreText.text = $"SCORE  {FormatScore(score)}";
            if (_gameOverBestText != null)
                _gameOverBestText.text = $"BEST  {FormatScore(bestScore)}";
            if (_gameOverMovesText != null)
                _gameOverMovesText.text = $"MOVES  {moveCount}";

            var btn = _gameOverRestartBtn;
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => _restartCallback?.Invoke());
            }

            var reviveBtnGO = _gameOverReviveBtnGO;
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

    /// <summary>
    /// 九宫格圆角方形（矢量缩放）：小尺寸源图 + 四边 border，
    /// Image.Type.Sliced 拉伸任意尺寸时四角圆角保持不变（等效矢量对话框，渲染清晰不变形）。
    /// </summary>
    private Sprite CreateSlicedRoundedSprite(int size, int radius)
    {
        string key = $"slice{size}r{radius}";
        if (_spriteCache.TryGetValue(key, out var cached))
            return cached;

        var tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
        tex.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // 2x2 超采样：圆角边缘柔化（避免 Sliced 拉伸后出现锯齿/硬边）
                int hits = 0;
                for (int sy = 0; sy < 2; sy++)
                {
                    for (int sx = 0; sx < 2; sx++)
                    {
                        float fx = x + (sx + 0.5f) * 0.5f;
                        float fy = y + (sy + 0.5f) * 0.5f;
                        bool inside = true;
                        if (fx < radius && fy < radius)
                            inside = (fx - radius) * (fx - radius) + (fy - radius) * (fy - radius) <= radius * radius;
                        else if (fx >= size - radius && fy < radius)
                            inside = (fx - (size - radius)) * (fx - (size - radius)) + (fy - radius) * (fy - radius) <= radius * radius;
                        else if (fx < radius && fy >= size - radius)
                            inside = (fx - radius) * (fx - radius) + (fy - (size - radius)) * (fy - (size - radius)) <= radius * radius;
                        else if (fx >= size - radius && fy >= size - radius)
                            inside = (fx - (size - radius)) * (fx - (size - radius)) + (fy - (size - radius)) * (fy - (size - radius)) <= radius * radius;
                        if (inside) hits++;
                    }
                }
                pixels[y * size + x] = new Color(1f, 1f, 1f, hits * 0.25f);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();

        // border = 四边各 radius 像素：Sliced 时圆角区域不被拉伸，任意尺寸缩放圆角恒为 radius（矢量感）
        var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect,
            new Vector4(radius, radius, radius, radius));
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

    /// <summary>直角方块贴图（纯白实心矩形，供 Image 铺满）</summary>
    private Sprite CreateSquareSprite(int size)
    {
        string key = $"square{size}";
        if (_spriteCache.TryGetValue(key, out var cached))
            return cached;

        var tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
        tex.SetPixels(pixels);
        tex.Apply();

        var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100);
        _spriteCache[key] = sprite;
        return sprite;
    }

    /// <summary>
    /// 闪电贴图：按微信版 special.js drawBolt 七点折线生成（Image.color 染色 #D9730D）。
    /// 顶点在 [-1,1] 坐标空间（y 向下），带 2x2 超采样抗锯齿。
    /// </summary>
    private Sprite CreateBoltSprite(int size)
    {
        string key = $"bolt{size}";
        if (_spriteCache.TryGetValue(key, out var cached))
            return cached;

        var tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
        tex.wrapMode = TextureWrapMode.Clamp;

        Vector2[] poly =
        {
            new Vector2(-0.10f, -1.00f), new Vector2(0.62f, -1.00f),
            new Vector2(0.10f, -0.12f), new Vector2(0.66f, -0.12f),
            new Vector2(-0.44f, 1.00f), new Vector2(-0.12f, 0.14f),
            new Vector2(-0.66f, 0.14f)
        };

        Color[] pixels = new Color[size * size];
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float hit = 0f;
                for (int sy = 0; sy < 2; sy++)
                {
                    for (int sx = 0; sx < 2; sx++)
                    {
                        float nx = (x + (sx + 0.5f) / 2f) / half - 1f;   // -1..1
                        float ny = (y + (sy + 0.5f) / 2f) / half - 1f;
                        if (PointInPolygon(nx, ny, poly)) hit += 0.25f;
                    }
                }
                pixels[y * size + x] = hit > 0f ? new Color(1f, 1f, 1f, hit) : Color.clear;
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();

        var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100);
        _spriteCache[key] = sprite;
        return sprite;
    }

    /// <summary>
    /// 加载 MENU 参考图切出的皇冠位图（Resources/Sprites/crown_menu.png，透明背景含三颗星），
    /// 并缓存为 Sprite。找不到时返回 null，由调用方兜底。
    /// </summary>
    private Sprite LoadCrownSprite()
    {
        const string key = "crown_menu_png";
        if (_spriteCache.TryGetValue(key, out var cached)) return cached;

        var tex = Resources.Load<Texture2D>("Sprites/crown_menu");
        if (tex == null)
        {
            Debug.LogWarning("[ScoreDisplay] 未找到 Resources/Sprites/crown_menu.png，皇冠回退为代码绘制");
            return null;
        }

        var sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        _spriteCache[key] = sp;
        return sp;
    }

    /// <summary>射线法判定点是否在多边形内</summary>
    private static bool PointInPolygon(float px, float py, Vector2[] poly)
    {
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            Vector2 a = poly[i], b = poly[j];
            if ((a.y > py) != (b.y > py) &&
                px < (b.x - a.x) * (py - a.y) / (b.y - a.y) + a.x)
                inside = !inside;
        }
        return inside;
    }

    /// <summary>五角星贴图：代码渲染白星（外部 Image.color 染色），带 2x2 超采样抗锯齿</summary>
    private Sprite CreateStarSprite(int size)
    {
        string key = $"star{size}";
        if (_spriteCache.TryGetValue(key, out var cached))
            return cached;

        var tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
        tex.wrapMode = TextureWrapMode.Clamp;

        float cx = size / 2f, cy = size / 2f;
        float R = size * 0.47f;          // 外半径
        float rr = R * 0.42f;            // 内半径
        Vector2[] poly = new Vector2[10];
        for (int i = 0; i < 10; i++)
        {
            float ang = -Mathf.PI / 2f + i * Mathf.PI / 5f;
            float rad = (i % 2 == 0) ? R : rr;
            poly[i] = new Vector2(cx + Mathf.Cos(ang) * rad, cy + Mathf.Sin(ang) * rad);
        }

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float px = x + 0.5f, py = y + 0.5f;
                float acc = 0f;
                // 2x2 超采样抗锯齿
                for (int sy = 0; sy < 2; sy++)
                {
                    for (int sx = 0; sx < 2; sx++)
                    {
                        if (PointInPoly(poly, px + (sx - 0.5f) * 0.5f, py + (sy - 0.5f) * 0.5f))
                            acc += 0.25f;
                    }
                }
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(acc)));
            }
        }
        tex.Apply();

        var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100);
        _spriteCache[key] = sprite;
        return sprite;
    }

    /// <summary>空心圆环贴图：白色圆环（外部 Image.color 染色），带柔和抗锯齿边缘</summary>
    private Sprite CreateRingSprite(int size, float thickness)
    {
        string key = $"ring{size}x{thickness}";
        if (_spriteCache.TryGetValue(key, out var cached))
            return cached;

        var tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
        tex.wrapMode = TextureWrapMode.Clamp;

        float cx = size / 2f, cy = size / 2f;
        float outerR = size / 2f - 0.35f;
        float innerR = outerR - thickness;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - cx;
                float dy = y + 0.5f - cy;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                // 距离在内外半径之间 → 白色，边缘 0.6px 渐变抗锯齿
                float a = Mathf.Clamp01(Mathf.Min(outerR + 0.6f - dist, dist - (innerR - 0.6f)));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();

        var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100);
        _spriteCache[key] = sprite;
        return sprite;
    }

    /// <summary>射线法判定点是否在任意简单多边形内</summary>
    private bool PointInPoly(Vector2[] poly, float px, float py)
    {
        bool inside = false;
        int n = poly.Length;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            var pi = poly[i];
            var pj = poly[j];
            if ((pi.y > py) != (pj.y > py) &&
                px < (pj.x - pi.x) * (py - pi.y) / (pj.y - pi.y) + pi.x)
                inside = !inside;
        }
        return inside;
    }

    private void OnDestroy()
    {
        _confetti = null;
    }
}
