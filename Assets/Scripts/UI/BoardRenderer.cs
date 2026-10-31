using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BoardRenderer : MonoBehaviour
{
    private Canvas _canvas;
    private RectTransform _boardContainer;
    private List<TileView> _activeTiles;
    private Dictionary<int, TileView> _tileViewMap;

    private static readonly Color BoardBgColor = new Color(0.830f, 0.797f, 0.886f); // 淡紫托盘（稍加深）
    private static readonly Color CellBgColor = new Color(0.910f, 0.890f, 0.950f); // 更浅紫白格子（微加深）

    private static readonly Dictionary<int, Color> TileColors = new Dictionary<int, Color>
    {
        // 糖果色轮：每个数值专属一色（2 粉红 → 65536 深玫红，色相绕满一圈）
        { 0, new Color(0.910f, 0.890f, 0.950f) },   // 空格 紫白
        { 2, new Color(0.941f, 0.569f, 0.686f) },   // 粉红      RGB(240,145,175)
        { 4, new Color(1.000f, 0.698f, 0.549f) },   // 蜜桃橙    RGB(255,178,140)
        { 8, new Color(0.980f, 0.804f, 0.431f) },   // 奶油黄    RGB(250,205,110)
        { 16, new Color(0.961f, 0.639f, 0.322f) },  // 杏橙      RGB(245,163,82)
        { 32, new Color(0.510f, 0.765f, 0.922f) },  // 天蓝      RGB(130,195,235)
        { 64, new Color(0.412f, 0.647f, 0.882f) },  // 晴朗蓝    RGB(105,165,225)
        { 128, new Color(0.745f, 0.627f, 0.863f) }, // 淡紫      RGB(190,160,220)
        { 256, new Color(0.608f, 0.549f, 0.804f) }, // 香芋紫    RGB(155,140,205)
        { 512, new Color(0.510f, 0.843f, 0.725f) }, // 薄荷绿    RGB(130,215,185)
        { 1024, new Color(0.647f, 0.824f, 0.471f) },// 青柠绿    RGB(165,210,120)
        { 2048, new Color(0.910f, 0.392f, 0.490f) },// 草莓红    RGB(232,100,125)
        { 4096, new Color(0.549f, 0.471f, 0.765f) },// 葡萄紫    RGB(140,120,195)
        { 8192, new Color(0.353f, 0.549f, 0.843f) },// 深海蓝    RGB(90,140,215)
        { 16384, new Color(0.667f, 0.490f, 0.314f) },// 焦糖棕   RGB(170,125,80)
        { 32768, new Color(0.275f, 0.647f, 0.627f) },// 松石青   RGB(70,165,160)
        { 65536, new Color(0.824f, 0.373f, 0.569f) },// 深玫红   RGB(210,95,145)
    };

    private static readonly Color TextColorDark = new Color(0.40f, 0.35f, 0.48f);
    private static readonly Color TextColorLight = new Color(0.97f, 0.97f, 0.98f);

    private int _gridSize = 4;
    private float _cellSize;
    private float _gap;
    private float _boardPadding;
    private RectTransform _cellBgContainer;
    private List<int> _removeBuffer;

    public void Initialize(Canvas canvas)
    {
        _canvas = canvas;
        _activeTiles = new List<TileView>();
        _tileViewMap = new Dictionary<int, TileView>();
        _removeBuffer = new List<int>();

        CreateBoardBackground();
        CreateCellBackgrounds();
    }

    /// <summary>切换网格尺寸（4/5），重建格子背景并重算格子尺寸</summary>
    public void SetGridSize(int gridSize)
    {
        if (gridSize < 3 || gridSize > 6) return;
        _gridSize = gridSize;
        // 棋盘外框尺寸不变，仅重算格子大小与间隙
        float boardSize = _boardContainer != null ? _boardContainer.sizeDelta.x
            : Mathf.Min(_canvas.GetComponent<RectTransform>().rect.width, _canvas.GetComponent<RectTransform>().rect.height) * 0.88f;
        _boardPadding = boardSize * 0.04f;
        _gap = boardSize * 0.02f;
        _cellSize = (boardSize - _boardPadding * 2 - _gap * (_gridSize - 1)) / _gridSize;
        CreateCellBackgrounds();
    }

    private void CreateBoardBackground()
    {
        // Force canvas layout update, then calculate board size from canvas
        Canvas.ForceUpdateCanvases();
        var canvasRect = _canvas.GetComponent<RectTransform>().rect;
        float boardSize = Mathf.Min(canvasRect.width, canvasRect.height) * 0.88f;

        var boardGO = new GameObject("Board", typeof(Image), typeof(CanvasGroup));
        var boardRT = boardGO.GetComponent<RectTransform>();
        boardRT.SetParent(_canvas.transform, false);
        boardRT.sizeDelta = new Vector2(boardSize, boardSize);

        var boardImg = boardGO.GetComponent<Image>();
        boardImg.color = BoardBgColor;

        boardRT.anchorMin = new Vector2(0.5f, 0.5f);
        boardRT.anchorMax = new Vector2(0.5f, 0.5f);
        boardRT.anchoredPosition = new Vector2(0, -50);

        _boardContainer = boardRT;
        _boardPadding = boardSize * 0.04f;
        _gap = boardSize * 0.02f;
        _cellSize = (boardSize - _boardPadding * 2 - _gap * (_gridSize - 1)) / _gridSize;
    }

    private void CreateCellBackgrounds()
    {
        if (_cellBgContainer != null)
            Destroy(_cellBgContainer.gameObject);

        var bgGO = new GameObject("CellBackgrounds", typeof(RectTransform));
        _cellBgContainer = bgGO.GetComponent<RectTransform>();
        _cellBgContainer.SetParent(_boardContainer, false);
        _cellBgContainer.anchorMin = Vector2.zero;
        _cellBgContainer.anchorMax = Vector2.one;
        _cellBgContainer.sizeDelta = Vector2.zero;
        _cellBgContainer.anchoredPosition = Vector2.zero;

        for (int r = 0; r < _gridSize; r++)
        {
            for (int c = 0; c < _gridSize; c++)
            {
                var cellGO = new GameObject($"CellBg_{r}_{c}", typeof(Image));
                var cellRT = cellGO.GetComponent<RectTransform>();
                cellRT.SetParent(_cellBgContainer, false);

                Vector2 pos = GetCellPosition(r, c);
                cellRT.anchorMin = new Vector2(0, 1);
                cellRT.anchorMax = new Vector2(0, 1);
                cellRT.sizeDelta = new Vector2(_cellSize, _cellSize);
                cellRT.anchoredPosition = pos;

                var img = cellGO.GetComponent<Image>();
                img.color = CellBgColor;
            }
        }
    }

    public Vector2 GetCellPosition(int row, int col)
    {
        float x = _boardPadding + col * (_cellSize + _gap) + _cellSize / 2f;
        float y = -(_boardPadding + row * (_cellSize + _gap) + _cellSize / 2f);
        return new Vector2(x, y);
    }

    public float CellSize => _cellSize;
    public int GridSize => _gridSize;
    public RectTransform BoardContainer => _boardContainer;

    public void ClearBoard()
    {
        foreach (var kvp in _tileViewMap)
        {
            if (kvp.Value != null && kvp.Value.gameObject != null)
                Destroy(kvp.Value.gameObject);
        }
        _tileViewMap.Clear();
        _activeTiles.Clear();
    }

    public void RefreshBoard(IReadOnlyList<TileData> tiles, bool animate)
    {
        _removeBuffer.Clear();
        foreach (var kvp in _tileViewMap)
            _removeBuffer.Add(kvp.Key);

        foreach (var tile in tiles)
        {
            if (_tileViewMap.ContainsKey(tile.Id))
            {
                _removeBuffer.Remove(tile.Id);
                var tv = _tileViewMap[tile.Id];
                if (animate && (tile.Row != tile.PreviousRow || tile.Col != tile.PreviousCol))
                {
                    tv.AnimateMove(GetCellPosition(tile.Row, tile.Col), GameManager.Instance.TileAnimationDuration);
                }
                else
                {
                    tv.SetPosition(GetCellPosition(tile.Row, tile.Col));
                }
                tv.UpdateDisplay(tile.Value);

                if (tile.JustMerged)
                {
                    tv.AnimateMerge(GameManager.Instance.TileAnimationDuration);
                }
            }
            else
            {
                var tv = CreateTileView(tile);
                tv.UpdateDisplay(tile.Value);
                tv.SetPosition(GetCellPosition(tile.Row, tile.Col));
                if (tile.IsNew && animate)
                {
                    tv.AnimateSpawn(GameManager.Instance.SpawnAnimationDuration);
                }
                _tileViewMap[tile.Id] = tv;
                _activeTiles.Add(tv);
            }
        }

        foreach (var id in _removeBuffer)
        {
            if (_tileViewMap.ContainsKey(id))
            {
                var tv = _tileViewMap[id];
                if (tv != null)
                {
                    _activeTiles.Remove(tv);
                    if (tv.MergedFromSelf)
                    {
                        tv.AnimateMerge(GameManager.Instance.TileAnimationDuration);
                    }
                    Destroy(tv.gameObject, GameManager.Instance.TileAnimationDuration + 0.05f);
                }
                _tileViewMap.Remove(id);
            }
        }
    }

    private TileView CreateTileView(TileData tile)
    {
        var go = new GameObject($"Tile_{tile.Id}", typeof(Image), typeof(TileView));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(_boardContainer, false);
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.sizeDelta = new Vector2(_cellSize, _cellSize);

        var img = go.GetComponent<Image>();
        img.color = GetTileColor(tile.Value);

        var textGO = new GameObject("Value", typeof(TMPro.TextMeshProUGUI));
        var textRT = textGO.GetComponent<RectTransform>();
        textRT.SetParent(rt, false);
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.sizeDelta = Vector2.zero;

        var tmp = textGO.GetComponent<TMPro.TextMeshProUGUI>();
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.fontSize = _cellSize * 0.4f;
        tmp.color = GetTextColor(tile.Value);
        tmp.text = tile.Value.ToString();

        var tv = go.GetComponent<TileView>();
        tv.Initialize(img, tmp, this);
        return tv;
    }

    public static Color GetTileColor(int value)
    {
        return TileColors.ContainsKey(value) ? TileColors[value] : new Color(0.745f, 0.627f, 0.863f);
    }

    public static Color GetTextColor(int value)
    {
        // 鲜艳糖果色用深紫灰字；高档深色（≥2048）用白字保证对比度
        return value >= 2048 ? TextColorLight : TextColorDark;
    }

    public static int GetFontSize(int value, float cellSize)
    {
        if (value < 100) return (int)(cellSize * 0.45f);
        if (value < 1000) return (int)(cellSize * 0.35f);
        if (value < 10000) return (int)(cellSize * 0.28f);
        return (int)(cellSize * 0.22f);
    }
}
