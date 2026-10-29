using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 3D 版棋盘渲染器（玩法仍为 2D 移动逻辑，仅表现层立体化）。
/// 保持与旧 2D 版一致的对外接口：
///   Initialize / SetGridSize / ClearBoard / RefreshBoard / GetCellPosition
///   CellSize / GridSize / BoardContainer / 静态取色方法
/// 差异：
///   - 场景由 3D 棋盘（底部面板 + 格子凹槽 + 立体方块）构成，相机为透视俯视；
///   - GetCellPosition 返回 canvas 逻辑坐标（棋盘投影），供特技按钮跟随；
///   - BoardContainer 是虚拟 RectTransform，其位置/尺寸=棋盘在屏幕上的投影矩形，
///     供 ScoreDisplay 布局使用（顶部标题/底部按钮会自动围绕 3D 棋盘排布）。
/// </summary>
public class BoardRenderer : MonoBehaviour
{
    // ========== 3D 世界参数（调优观感：改这里即可） ==========
    private const float WorldBoardSize = 6.8f;    // 棋盘（面板）世界边长
    private const float PanelOverhang = 0.30f;    // 面板比格子区外扩
    private const float WorldPadding = 0.22f;     // 面板边缘到首个格子中心
    private const float WorldGap = 0.12f;         // 格子间隙
    private const float BoardThickness = 0.16f;   // 底部面板厚度
    private const float CellFloorThickness = 0.05f; // 格子凹槽厚度
    private const float TileThickness = 0.34f;    // 活动方块厚度
    private const float BoardTopY = 0.02f;        // 面板顶面高度
    private const float CellBaseY = 0.05f;        // 格子底顶面 = 方块底面高度
    private const float CamElevation = 24f;       // 相机俯角（度）
    private const float CamBoardMargin = 1.06f;   // 棋盘在屏幕宽度上的占比
    private const float FieldOfView = 50f;        // 相机纵向 FOV

    /// <summary>方块中心 Y（只读给 TileView 用）</summary>
    public static float TileCenterY => CellBaseY + TileThickness * 0.5f;

    private Canvas _canvas;
    private RectTransform _boardContainer;
    private Transform _sceneRoot;
    private List<TileView> _activeTiles;
    private Dictionary<int, TileView> _tileViewMap;
    private List<int> _removeBuffer;
    private Transform _cellBgContainer;

    private int _gridSize = 4;
    private float _cellSize;
    private float _panelHalf => (WorldBoardSize + PanelOverhang) * 0.5f;

    // 材质
    private Material _boardMat;
    private Material _cellMat;
    private static readonly Dictionary<int, Material> TileMatCache = new Dictionary<int, Material>();

    private static readonly Color BoardBgColor = new Color(0.71f, 0.65f, 0.60f);
    private static readonly Color CellBgColor = new Color(0.78f, 0.73f, 0.68f);

    private static readonly Dictionary<int, Color> TileColors = new Dictionary<int, Color>
    {
        { 0,   new Color(0.78f, 0.73f, 0.68f) },
        { 2,   new Color(0.93f, 0.89f, 0.85f) },
        { 4,   new Color(0.93f, 0.88f, 0.78f) },
        { 8,   new Color(0.95f, 0.69f, 0.47f) },
        { 16,  new Color(0.96f, 0.58f, 0.39f) },
        { 32,  new Color(0.96f, 0.49f, 0.37f) },
        { 64,  new Color(0.96f, 0.37f, 0.23f) },
        { 128, new Color(0.93f, 0.81f, 0.45f) },
        { 256, new Color(0.93f, 0.80f, 0.38f) },
        { 512, new Color(0.93f, 0.78f, 0.31f) },
        { 1024,new Color(0.93f, 0.77f, 0.25f) },
        { 2048,new Color(0.93f, 0.76f, 0.14f) },
        { 4096,new Color(0.89f, 0.67f, 0.10f) },
        { 8192,new Color(0.86f, 0.58f, 0.07f) },
        { 16384,new Color(0.82f, 0.49f, 0.05f) },
        { 32768,new Color(0.78f, 0.40f, 0.03f) },
        { 65536,new Color(0.74f, 0.31f, 0.01f) },
    };

    private static readonly Color TextColorDark = new Color(0.47f, 0.43f, 0.40f);
    private static readonly Color TextColorLight = new Color(0.96f, 0.96f, 0.94f);

    public void Initialize(Canvas canvas)
    {
        _canvas = canvas;
        _activeTiles = new List<TileView>();
        _tileViewMap = new Dictionary<int, TileView>();
        _removeBuffer = new List<int>();

        SetupSceneAndCamera();
        CreateBoardBackground();
        ComputeBoardAnchor();

        // 初始网格尺寸
        _cellSize = ComputeCellSize(_gridSize);
        CreateCellBackgrounds();
        ComputeBoardAnchor();
    }

    // ================= 场景与相机 =================

    private void SetupSceneAndCamera()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            var camGO = new GameObject("MainCamera", typeof(Camera), typeof(AudioListener));
            camGO.tag = "MainCamera";
            cam = camGO.GetComponent<Camera>();
        }
        cam.orthographic = false;
        cam.fieldOfView = FieldOfView;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.96f, 0.94f, 0.90f);
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 60f;

        // 竖屏下让棋盘宽度占屏幕约 CamBoardMargin 倍（自动适配横竖屏）
        float aspect = (float)Screen.width / Mathf.Max(1f, (float)Screen.height);
        float hfovHalf = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * aspect;
        float dist = (_panelHalf * CamBoardMargin) / Mathf.Max(0.001f, hfovHalf);
        Vector3 lookTarget = new Vector3(0f, 0.35f, 0f);
        Vector3 dir = new Vector3(0f, Mathf.Sin(CamElevation * Mathf.Deg2Rad), -Mathf.Cos(CamElevation * Mathf.Deg2Rad));
        cam.transform.position = lookTarget + dir * dist;
        cam.transform.LookAt(lookTarget);

        // 光照：新建场景或手动放相机时保证有主方向光与环境补光
        if (FindObjectOfType<Light>() == null)
        {
            var lightGO = new GameObject("Directional Light", typeof(Light));
            lightGO.transform.rotation = Quaternion.Euler(55f, -30f, 0f);
            lightGO.GetComponent<Light>().type = LightType.Directional;
            lightGO.GetComponent<Light>().intensity = 1.0f;
        }
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.66f, 0.66f, 0.62f);
    }

    // ================= 棋盘构建 =================

    private void CreateBoardBackground()
    {
        _sceneRoot = new GameObject("Board3D").transform;
        _sceneRoot.SetParent(transform, false);

        // 底部面板（比格子区略大一圈）
        var boardCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        boardCube.name = "BoardPanel";
        Destroy(boardCube.GetComponent<Collider>());
        var bt = boardCube.transform;
        bt.SetParent(_sceneRoot, false);
        bt.localScale = new Vector3(WorldBoardSize + PanelOverhang, BoardThickness, WorldBoardSize + PanelOverhang);
        bt.localPosition = new Vector3(0f, BoardTopY - BoardThickness * 0.5f, 0f);
        _boardMat = new Material(Shader.Find("Standard"));
        _boardMat.color = BoardBgColor;
        boardCube.GetComponent<Renderer>().sharedMaterial = _boardMat;

        // 格子底容器
        _cellBgContainer = new GameObject("CellBackgrounds").transform;
        _cellBgContainer.SetParent(_sceneRoot, false);

        // 虚拟 UI 锚点：棋盘在屏幕上的投影矩形（供 ScoreDisplay 布局）
        var anchorGO = new GameObject("Board3DAnchor", typeof(RectTransform));
        _boardContainer = anchorGO.GetComponent<RectTransform>();
        _boardContainer.SetParent(_canvas.transform, false);
        _boardContainer.anchorMin = new Vector2(0.5f, 0.5f);
        _boardContainer.anchorMax = new Vector2(0.5f, 0.5f);
        _boardContainer.pivot = new Vector2(0.5f, 0.5f);
        _boardContainer.sizeDelta = new Vector2(1f, 1f);
    }

    /// <summary>切换网格尺寸（4/5），重建格子凹槽并重算格子尺寸（棋盘外框不变）</summary>
    public void SetGridSize(int gridSize)
    {
        if (gridSize < 3 || gridSize > 6) return;
        _gridSize = gridSize;
        _cellSize = ComputeCellSize(gridSize);
        CreateCellBackgrounds();
    }

    private float ComputeCellSize(int gridSize)
    {
        return (WorldBoardSize - WorldPadding * 2f - WorldGap * (gridSize - 1)) / gridSize;
    }

    private void CreateCellBackgrounds()
    {
        if (_cellBgContainer == null) return;
        foreach (Transform child in _cellBgContainer)
            Destroy(child.gameObject);

        if (_cellMat == null)
        {
            _cellMat = new Material(Shader.Find("Standard"));
            _cellMat.color = CellBgColor;
        }

        for (int r = 0; r < _gridSize; r++)
        {
            for (int c = 0; c < _gridSize; c++)
            {
                var cellGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cellGO.name = $"CellBg_{r}_{c}";
                Destroy(cellGO.GetComponent<Collider>());
                var ct = cellGO.transform;
                ct.SetParent(_cellBgContainer, false);
                ct.localScale = new Vector3(_cellSize * 0.96f, CellFloorThickness, _cellSize * 0.96f);
                Vector3 w = CellWorldNoY(r, c);
                ct.localPosition = new Vector3(w.x, CellBaseY - CellFloorThickness * 0.5f, w.z);
                cellGO.GetComponent<Renderer>().sharedMaterial = _cellMat;
            }
        }
    }

    // ================= 坐标换算 =================

    /// <summary>格子中心世界坐标（不含 Y，方块动画以整块为准）</summary>
    private Vector3 CellWorldNoY(int row, int col)
    {
        float x = -WorldBoardSize * 0.5f + WorldPadding + col * (_cellSize + WorldGap) + _cellSize * 0.5f;
        float z = WorldBoardSize * 0.5f - WorldPadding - row * (_cellSize + WorldGap) - _cellSize * 0.5f;
        return new Vector3(x, 0f, z);
    }

    /// <summary>格子中心世界坐标（含方块中心 Y，TileView 定位用）</summary>
    public Vector3 GetCellWorldPosition(int row, int col)
    {
        Vector3 p = CellWorldNoY(row, col);
        p.y = TileCenterY;
        return p;
    }

    /// <summary>格子中心 → canvas 逻辑坐标（中心锚点，特技按钮 UI 跟随用）</summary>
    public Vector2 GetCellPosition(int row, int col)
    {
        return WorldToCanvas(GetCellWorldPosition(row, col));
    }

    private Vector2 WorldToCanvas(Vector3 world)
    {
        Vector3 sp = Camera.main != null ? Camera.main.WorldToScreenPoint(world) : Vector3.zero;
        float scale = _canvas != null ? Mathf.Max(0.001f, _canvas.scaleFactor) : 1f;
        return new Vector2((sp.x - Screen.width * 0.5f) / scale,
                           (Screen.height * 0.5f - sp.y) / scale);
    }

    /// <summary>用棋盘四角投影更新虚拟 BoardContainer（棋盘在屏幕上的投影矩形）</summary>
    private void ComputeBoardAnchor()
    {
        if (_boardContainer == null || Camera.main == null) return;
        Vector3[] corners =
        {
            new Vector3(-_panelHalf, 0f, -_panelHalf),
            new Vector3(_panelHalf, 0f, -_panelHalf),
            new Vector3(-_panelHalf, 0f, _panelHalf),
            new Vector3(_panelHalf, 0f, _panelHalf),
        };
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 max = new Vector2(float.MinValue, float.MinValue);
        foreach (var c in corners)
        {
            Vector2 p = WorldToCanvas(c);
            min.x = Mathf.Min(min.x, p.x); min.y = Mathf.Min(min.y, p.y);
            max.x = Mathf.Max(max.x, p.x); max.y = Mathf.Max(max.y, p.y);
        }
        float w = max.x - min.x;
        float h = max.y - min.y;
        if (w < 10f || h < 10f) return;              // 投影异常时保留旧值
        _boardContainer.sizeDelta = new Vector2(w, h);
        _boardContainer.anchoredPosition = new Vector2((min.x + max.x) * 0.5f, (min.y + max.y) * 0.5f);
    }

    public float CellSize => _cellSize;
    public int GridSize => _gridSize;
    public RectTransform BoardContainer => _boardContainer;

    // ================= Tile 管理 =================

    public void ClearBoard()
    {
        if (_tileViewMap == null) return;
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
                    tv.AnimateMove(GetCellWorldPosition(tile.Row, tile.Col), GameManager.Instance.TileAnimationDuration);
                }
                else
                {
                    tv.SetPosition(GetCellWorldPosition(tile.Row, tile.Col));
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
                tv.SetPosition(GetCellWorldPosition(tile.Row, tile.Col));
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
        var go = new GameObject($"Tile_{tile.Id}", typeof(TileView));
        go.transform.SetParent(_sceneRoot, false);

        var cubeGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cubeGO.name = "Cube";
        Destroy(cubeGO.GetComponent<Collider>());
        var cubeT = cubeGO.transform;
        cubeT.SetParent(go.transform, false);
        float s = _cellSize * 0.92f;
        cubeT.localScale = new Vector3(s, TileThickness, s);
        cubeT.localPosition = new Vector3(0f, TileCenterY, 0f);
        var rend = cubeGO.GetComponent<Renderer>();
        rend.sharedMaterial = GetTileMaterial(tile.Value);

        // 顶面数字（世界空间 TMP，平躺朝上；挂在 root 上避免 Cube 缩放干扰）
        var textGO = new GameObject("Value");
        textGO.transform.SetParent(go.transform, false);
        var tmp = textGO.AddComponent<TMPro.TextMeshPro>();
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.fontSize = 90f;
        tmp.color = GetTextColor(tile.Value);
        tmp.text = FormatValue(tile.Value);
        var t = textGO.transform;
        t.localRotation = Quaternion.Euler(90f, 0f, 0f);
        t.localPosition = new Vector3(0f, TileCenterY + TileThickness * 0.5f + 0.02f, 0f);

        var tv = go.GetComponent<TileView>();
        tv.Initialize(cubeGO.transform, tmp, this);
        return tv;
    }

    /// <summary>按数值缩放顶面文字（字符高度 = cellSize 的固定比例，兼顾位数）</summary>
    public float GetTextScaleForValue(int value)
    {
        float charH = 0.30f;   // 字符高 / 格子宽
        if (value >= 1000000) charH *= 0.72f;
        else if (value >= 10000) charH *= 0.80f;
        return charH / 90f;    // TMP fontSize=90，按比例缩小到世界尺寸
    }

    private static string FormatValue(int value)
    {
        if (value >= 10000000) return (value / 1000000f).ToString("0.#") + "M";
        if (value >= 10000) return (value / 1000f).ToString("0.#") + "K";
        return value.ToString();
    }

    // ================= 静态取色/材质 =================

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

    /// <summary>方块材质（按数值缓存共享，避免实例化）</summary>
    public static Material GetTileMaterial(int value)
    {
        if (TileMatCache.TryGetValue(value, out var m) && m != null)
            return m;
        var mat = new Material(Shader.Find("Standard"));
        mat.color = GetTileColor(value);
        TileMatCache[value] = mat;
        return mat;
    }

    private void OnDestroy()
    {
        foreach (var kvp in TileMatCache)
        {
            if (kvp.Value != null)
                Destroy(kvp.Value);
        }
        TileMatCache.Clear();
    }
}
