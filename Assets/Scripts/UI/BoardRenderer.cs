using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BoardRenderer : MonoBehaviour
{
    private Canvas _canvas;
    private RectTransform _boardContainer;
    private List<TileView> _activeTiles;
    private Dictionary<int, TileView> _tileViewMap;

    private static readonly Color BoardBgColor = new Color(0.71f, 0.65f, 0.60f);
    private static readonly Color CellBgColor = new Color(0.78f, 0.73f, 0.68f);

    private static readonly Dictionary<int, Color> TileColors = new Dictionary<int, Color>
    {
        { 0, new Color(0.78f, 0.73f, 0.68f) },
        { 2, new Color(0.93f, 0.89f, 0.85f) },
        { 4, new Color(0.93f, 0.88f, 0.78f) },
        { 8, new Color(0.95f, 0.69f, 0.47f) },
        { 16, new Color(0.96f, 0.58f, 0.39f) },
        { 32, new Color(0.96f, 0.49f, 0.37f) },
        { 64, new Color(0.96f, 0.37f, 0.23f) },
        { 128, new Color(0.93f, 0.81f, 0.45f) },
        { 256, new Color(0.93f, 0.80f, 0.38f) },
        { 512, new Color(0.93f, 0.78f, 0.31f) },
        { 1024, new Color(0.93f, 0.77f, 0.25f) },
        { 2048, new Color(0.93f, 0.76f, 0.14f) },
        { 4096, new Color(0.89f, 0.67f, 0.10f) },
        { 8192, new Color(0.86f, 0.58f, 0.07f) },
        { 16384, new Color(0.82f, 0.49f, 0.05f) },
        { 32768, new Color(0.78f, 0.40f, 0.03f) },
        { 65536, new Color(0.74f, 0.31f, 0.01f) },
    };

    private static readonly Color TextColorDark = new Color(0.47f, 0.43f, 0.40f);
    private static readonly Color TextColorLight = new Color(0.96f, 0.96f, 0.94f);

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
        return TileColors.ContainsKey(value) ? TileColors[value] : new Color(0.74f, 0.31f, 0.01f);
    }

    public static Color GetTextColor(int value)
    {
        return value <= 4 ? TextColorDark : TextColorLight;
    }

    public static int GetFontSize(int value, float cellSize)
    {
        if (value < 100) return (int)(cellSize * 0.45f);
        if (value < 1000) return (int)(cellSize * 0.35f);
        if (value < 10000) return (int)(cellSize * 0.28f);
        return (int)(cellSize * 0.22f);
    }
}