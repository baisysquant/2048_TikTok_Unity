using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 2048 核心逻辑（对齐微信版 2048-wechat-canvas 玩法）：
///   - 动态网格：4x4 经典 / 5x5 挑战（难度弹窗选择）
///   - 后悔回撤：回撤最近 5 步，每盘局前 5 次免费，第 6 次起需看激励视频（由 UI 层路由广告）
///   - 特技加速：仅合成 512（256+256）触发 OnSpecialTrigger，由 SpecialSystem 接管
///     （棋盘闪烁机会 / 特技池 / 广告奖励 / +2048 兑现）
///   - 复活：移除 2 个最小方块，避免满盘复活后立刻再次 Game Over
///   - 最高分：FNV-1a 摘要校验防本地篡改 + 2 秒去抖落盘
///   - 分数：long 安全累加
/// </summary>
public class Game2048
{
    public const int DefaultGridSize = 4;
    public const int UndoFreeCount = 5;       // 每盘局免费回撤次数
    public const int UndoSteps = 5;           // 每次点击回撤步数
    public const int UndoHistoryCap = 60;     // 回撤历史上限

    private const string BestScoreKey = "fk2048_best";
    private const float BestFlushDelay = 2f;

    private int _gridSize = DefaultGridSize;
    private TileData[,] _grid;
    private List<TileData> _tiles;
    private int _nextId;
    private System.Random _random;
    private List<GameStateSnapshot> _history;
    private bool _reviveUsed;
    private int _undoFreeLeft;
    private List<int> _lastMergeValues;   // 本步移动中合并出的数值（用于特技触发统计）

    private bool _bestDirty;
    private float _bestDirtySince;

    public int GridSize => _gridSize;
    public TileData[,] Grid => _grid;
    public IReadOnlyList<TileData> Tiles => _tiles.AsReadOnly();
    public long Score { get; private set; }
    public long BestScore { get; private set; }
    public bool GameOver { get; private set; }
    public bool Won { get; private set; }
    public bool KeepPlaying { get; private set; }
    public int MoveCount { get; private set; }
    public bool CanRevive => !_reviveUsed && _history != null && _history.Count > 0;
    public bool CanUndo => _history != null && _history.Count > 0;
    public int UndoFreeLeft => _undoFreeLeft;

    public event Action OnStateChanged;
    public event Action<long> OnScoreChanged;
    public event Action<bool> OnGameOverChanged;
    public event Action<bool> OnWonChanged;
    public event Action<MoveDirection> OnMoveExecuted;
    public event Action<int> OnTileSpawned;    // 参数：新方块数值
    public event Action<int> OnTileMerged;     // 参数：合并后数值
    public event Action OnSpecialTrigger;      // 一次合并新生成 512（256+256）时触发

    public Game2048()
    {
        _random = new System.Random();
        _grid = new TileData[DefaultGridSize, DefaultGridSize];
        _tiles = new List<TileData>();
        _nextId = 1;
        _history = new List<GameStateSnapshot>(UndoHistoryCap);
        _lastMergeValues = new List<int>(8);
        BestScore = LoadBestScore();
    }

    public void StartNewGame(int gridSize)
    {
        _gridSize = (gridSize == 4 || gridSize == 5) ? gridSize : DefaultGridSize;
        _grid = new TileData[_gridSize, _gridSize];

        _tiles.Clear();
        Score = 0;
        MoveCount = 0;
        GameOver = false;
        Won = false;
        KeepPlaying = false;
        _nextId = 1;
        _history.Clear();
        _reviveUsed = false;
        _undoFreeLeft = UndoFreeCount;   // 每盘局免费次数重置
        _lastMergeValues.Clear();

        SpawnRandomTile();
        SpawnRandomTile();

        OnStateChanged?.Invoke();
        OnScoreChanged?.Invoke(Score);
        OnGameOverChanged?.Invoke(false);
        OnWonChanged?.Invoke(false);

        return k;
    }

    public bool Move(MoveDirection direction)
    {
        if (GameOver) return false;

        // 微信版语义：回撤快照在移动结算【前】拍摄，仅在本步真正移动后入历史
        GameStateSnapshot snapshot = CaptureSnapshot();

        bool moved = MoveTiles(direction);

        if (!moved)
        {
            _lastMergeValues.Clear();
            return false;
        }

        _history.Add(snapshot);
        if (_history.Count > UndoHistoryCap)
            _history.RemoveAt(0);

        MoveCount++;
        if (!KeepPlaying && CheckWin())
        {
            Won = true;
            OnWonChanged?.Invoke(true);
        }

        SpawnRandomTile();

        // 特技触发：仅"新生成 512"（256+256）触发，1024/2048 等更高等级不触发
        // 与微信版一致：在移动结算（含新方块生成）之后统一分发
        for (int i = 0; i < _lastMergeValues.Count; i++)
        {
            if (_lastMergeValues[i] == 512)
                OnSpecialTrigger?.Invoke();
        }
        _lastMergeValues.Clear();

        OnStateChanged?.Invoke();
        OnMoveExecuted?.Invoke(direction);

        if (CheckGameOver())
        {
            GameOver = true;
            FlushBestScore();
            OnGameOverChanged?.Invoke(true);
        }

        return moved;
    }

    public void ContinueAfterWin()
    {
        KeepPlaying = true;
        Won = false;
        OnWonChanged?.Invoke(false);
    }

    public bool HasTileAt(int row, int col)
    {
        return row >= 0 && row < _gridSize && col >= 0 && col < _gridSize && _grid[row, col] != null;
    }

    public TileData GetTileAt(int row, int col)
    {
        if (row >= 0 && row < _gridSize && col >= 0 && col < _gridSize)
            return _grid[row, col];
        return null;
    }

    public int GetEmptyCellCount()
    {
        int count = 0;
        for (int r = 0; r < _gridSize; r++)
            for (int c = 0; c < _gridSize; c++)
                if (_grid[r, c] == null)
                    count++;
        return count;
    }

    public int MaxValue
    {
        get
        {
            int max = 0;
            foreach (var t in _tiles)
                if (t.Value > max) max = t.Value;
            return max;
        }
    }

    /// <summary>分数累加：long 安全，超过 long.MaxValue 时饱和</summary>
    public void AddScore(long points)
    {
        if (points <= 0) return;
        long next = Score + points;
        Score = (next < 0) ? long.MaxValue : next;   // 溢出保护
        OnScoreChanged?.Invoke(Score);
        if (Score > BestScore)
        {
            BestScore = Score;
            MarkBestDirty();
        }
    }

    /// <summary>空位列表（Vector2Int.x = 行, y = 列）；供特技系统定位闪烁按钮</summary>
    public List<Vector2Int> GetEmptyCells()
    {
        var empty = new List<Vector2Int>();
        for (int r = 0; r < _gridSize; r++)
            for (int c = 0; c < _gridSize; c++)
                if (_grid[r, c] == null)
                    empty.Add(new Vector2Int(r, c));
        return empty;
    }

    private void SpawnRandomTile()
    {
        var empty = GetEmptyCells();
        if (empty.Count == 0) return;

        var pos = empty[_random.Next(empty.Count)];
        int value = _random.NextDouble() < 0.9 ? 2 : 4;

        var tile = new TileData(_nextId++, value, pos.x, pos.y) { IsNew = true };
        _grid[pos.x, pos.y] = tile;
        _tiles.Add(tile);
        OnTileSpawned?.Invoke(value);
    }

    private bool MoveTiles(MoveDirection dir)
    {
        bool moved = false;
        _lastMergeValues.Clear();
        foreach (var t in _tiles) { t.PreviousRow = t.Row; t.PreviousCol = t.Col; t.JustMerged = false; t.MergedFromSelf = false; }

        bool horizontal = dir == MoveDirection.Left || dir == MoveDirection.Right;
        bool reverse = dir == MoveDirection.Right || dir == MoveDirection.Down;

        for (int line = 0; line < _gridSize; line++)
        {
            var source = new List<TileData>(_gridSize);
            for (int i = 0; i < _gridSize; i++)
            {
                int r = horizontal ? line : i;
                int c = horizontal ? i : line;
                if (_grid[r, c] != null) source.Add(_grid[r, c]);
            }
            if (reverse) source.Reverse();

            var result = new List<TileData>(_gridSize);
            for (int i = 0; i < source.Count; i++)
            {
                TileData current = source[i];
                TileData last = result.Count > 0 ? result[result.Count - 1] : null;
                if (last != null && last.Value == current.Value && !last.JustMerged)
                {
                    last.Value *= 2;
                    // 饱和保护：与 AddScore 一致，避免极端分数溢出
                    Score = (Score > long.MaxValue - last.Value) ? long.MaxValue : Score + last.Value;
                    OnScoreChanged?.Invoke(Score);
                    last.JustMerged = true;
                    _lastMergeValues.Add(last.Value);
                    OnTileMerged?.Invoke(last.Value);
                    _tiles.Remove(current);
                    last.MergedFromSelf = true;
                    moved = true;
                }
                else
                {
                    result.Add(current);
                }
            }

            for (int i = 0; i < _gridSize; i++)
            {
                int r = horizontal ? line : (reverse ? _gridSize - 1 - i : i);
                int c = horizontal ? (reverse ? _gridSize - 1 - i : i) : line;
                TileData tile = i < result.Count ? result[i] : null;
                _grid[r, c] = tile;
                if (tile != null)
                {
                    if (horizontal)
                    {
                        if (c != tile.PreviousCol) moved = true;
                        tile.Col = c;
                    }
                    else
                    {
                        if (r != tile.PreviousRow) moved = true;
                        tile.Row = r;
                    }
                }
            }
        }

        if (Score > BestScore)
        {
            BestScore = Score;
            MarkBestDirty();
        }
        return moved;
    }

    private bool CheckWin()
    {
        foreach (var t in _tiles)
            if (t.Value >= 2048)
                return true;
        return false;
    }

    private bool CheckGameOver()
    {
        if (GetEmptyCellCount() > 0) return false;

        for (int r = 0; r < _gridSize; r++)
        {
            for (int c = 0; c < _gridSize; c++)
            {
                int val = _grid[r, c].Value;
                if (c < _gridSize - 1 && _grid[r, c + 1].Value == val) return false;
                if (r < _gridSize - 1 && _grid[r + 1, c].Value == val) return false;
            }
        }

        return true;
    }

    // ========== 最高分（FNV-1a 校验 + 去抖落盘，与微信版算法一致） ==========

    /// <summary>FNV-1a 32 位摘要（base36），与微信版 bestDigest 输出一致</summary>
    public static string BestDigest(string value)
    {
        string s = "fk2048:v1:" + value;
        uint h = 0x811c9dc5u;
        for (int i = 0; i < s.Length; i++)
        {
            h ^= (uint)s[i];
            h *= 0x01000193u;
        }
        return ToBase36(h);
    }

    private static string ToBase36(uint v)
    {
        const string chars = "0123456789abcdefghijklmnopqrstuvwxyz";
        if (v == 0) return "0";
        var sb = new System.Text.StringBuilder();
        while (v > 0)
        {
            sb.Insert(0, chars[(int)(v % 36)]);
            v /= 36;
        }
        return sb.ToString();
    }

    private long LoadBestScore()
    {
        try
        {
            string raw = TikTokBridge.GetString(BestScoreKey, "");
            if (string.IsNullOrEmpty(raw)) return 0;

            int sep = raw.IndexOf('|');
            if (sep > 0)
            {
                string v = raw.Substring(0, sep);
                string c = raw.Substring(sep + 1);
                if (c == BestDigest(v) && long.TryParse(v, out long val))
                    return val;
                return 0;   // 校验失败视为被篡改 / 首次运行
            }

            // 兼容旧版本纯数字存储，立即迁移为带校验格式
            if (long.TryParse(raw, out long old))
            {
                BestScore = old;
                FlushBestScore();
                return old;
            }
            return 0;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Game2048] LoadBestScore failed: " + e.Message);
            return 0;
        }
    }

    private void MarkBestDirty()
    {
        _bestDirty = true;
        _bestDirtySince = Time.unscaledTime;
    }

    /// <summary>由 GameManager 每帧调用；超过 2 秒去抖窗口后落盘</summary>
    public void TickBestFlush()
    {
        if (!_bestDirty) return;
        if (Time.unscaledTime - _bestDirtySince < BestFlushDelay) return;
        FlushBestScore();
    }

    public void FlushBestScore()
    {
        if (!_bestDirty) return;
        _bestDirty = false;
        try
        {
            TikTokBridge.SetString(BestScoreKey, BestScore + "|" + BestDigest(BestScore.ToString()));
            TikTokBridge.Save();
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Game2048] FlushBestScore failed: " + e.Message);
        }
    }

    // ========== 回撤快照 ==========

    private struct GameStateSnapshot
    {
        public int[,] GridValues;
        public long Score;
        public int MoveCount;
        public bool KeepPlaying;
    }

    /// <summary>拍摄当前局面快照（移动结算前调用，对齐微信版 takeUndoSnapshot）</summary>
    private GameStateSnapshot CaptureSnapshot()
    {
        var snap = new GameStateSnapshot
        {
            GridValues = new int[_gridSize, _gridSize],
            Score = Score,
            MoveCount = MoveCount,
            KeepPlaying = KeepPlaying
        };
        for (int r = 0; r < _gridSize; r++)
            for (int c = 0; c < _gridSize; c++)
                snap.GridValues[r, c] = _grid[r, c]?.Value ?? 0;
        return snap;
    }

    /// <summary>扣减一次免费回撤次数（由 UI 层在免费路径调用）；返回是否成功</summary>
    public bool ConsumeUndoFree()
    {
        if (_undoFreeLeft <= 0) return false;
        _undoFreeLeft--;
        return true;
    }

    /// <summary>
    /// 回撤最近 UndoSteps 步（快照不足则回撤历史长度步），返回实际回撤步数。
    /// 免费次数的扣减与广告路由由 UI 层（GameManager）负责。
    /// 目标状态为被弹出快照中最早的一条（对齐微信版 applyUndo，恢复移动结算前的局面）。
    /// </summary>
    public int Undo()
    {
        if (!CanUndo) return 0;

        int k = Mathf.Min(UndoSteps, _history.Count);
        GameStateSnapshot target = default;
        // 从最新到最旧依次弹出 k 条快照，目标状态为其中最早的一条（对齐微信版 applyUndo）
        for (int i = 0; i < k; i++)
        {
            target = _history[_history.Count - 1];
            _history.RemoveAt(_history.Count - 1);
        }

        for (int r = 0; r < _gridSize; r++)
            for (int c = 0; c < _gridSize; c++)
                _grid[r, c] = null;
        _tiles.Clear();

        Score = target.Score;
        MoveCount = target.MoveCount;
        KeepPlaying = target.KeepPlaying;
        GameOver = false;
        Won = false;

        for (int r = 0; r < _gridSize; r++)
        {
            for (int c = 0; c < _gridSize; c++)
            {
                int val = target.GridValues[r, c];
                if (val > 0)
                {
                    var tile = new TileData(_nextId++, val, r, c);
                    _grid[r, c] = tile;
                    _tiles.Add(tile);
                }
            }
        }

        OnStateChanged?.Invoke();
        OnScoreChanged?.Invoke(Score);
        OnGameOverChanged?.Invoke(false);
        OnWonChanged?.Invoke(false);

        return k;
    }

    /// <summary>
    /// 复活：从当前残局移除 2 个最小方块以腾出空间，继续当前棋盘（对齐微信版）。
    /// 由 UI 层在看完整激励视频后调用。
    /// </summary>
    public void Revive()
    {
        if (!CanRevive) return;

        _reviveUsed = true;
        // 对齐微信版：复活后仍属当前局，回撤历史与免费次数保留（可继续悔棋救场）

        // 收集所有已占格子，按 数值升序 -> 行降序 -> 列降序 排序，移除前 2 个最小方块
        var cells = new List<Vector2Int>();
        for (int r = 0; r < _gridSize; r++)
        {
            for (int c = 0; c < _gridSize; c++)
            {
                if (_grid[r, c] != null)
                    cells.Add(new Vector2Int(r, c));
            }
        }
        cells.Sort((a, b) =>
        {
            int va = _grid[a.x, a.y].Value;
            int vb = _grid[b.x, b.y].Value;
            if (va != vb) return va.CompareTo(vb);
            if (b.x != a.x) return b.x.CompareTo(a.x);
            return b.y.CompareTo(a.y);
        });

        int removeCount = Mathf.Min(2, cells.Count);
        for (int i = 0; i < removeCount; i++)
        {
            var pos = cells[i];
            var tile = _grid[pos.x, pos.y];
            _grid[pos.x, pos.y] = null;
            _tiles.Remove(tile);
        }

        GameOver = false;
        Won = false;

        OnStateChanged?.Invoke();
        OnScoreChanged?.Invoke(Score);
        OnGameOverChanged?.Invoke(false);
        OnWonChanged?.Invoke(false);

        return k;
    }
}
