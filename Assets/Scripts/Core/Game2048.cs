using System;
using System.Collections.Generic;
using UnityEngine;

public class Game2048
{
    private const int GridSize = 4;
    private const int MaxHistory = 5;
    private TileData[,] _grid;
    private List<TileData> _tiles;
    private int _nextId;
    private System.Random _random;
    private List<GameStateSnapshot> _history;
    private bool _reviveUsed;

    public TileData[,] Grid => _grid;
    public IReadOnlyList<TileData> Tiles => _tiles.AsReadOnly();
    public int Score { get; private set; }
    public int BestScore { get; private set; }
    public bool GameOver { get; private set; }
    public bool Won { get; private set; }
    public bool KeepPlaying { get; private set; }
    public int MoveCount { get; private set; }
    public bool CanRevive => !_reviveUsed && _history != null && _history.Count > 0;
    public bool CanUndo => _history != null && _history.Count > 0;

    public event Action OnStateChanged;
    public event Action<int> OnScoreChanged;
    public event Action<bool> OnGameOverChanged;
    public event Action<bool> OnWonChanged;
    public event Action<MoveDirection> OnMoveExecuted;
    public event Action<int> OnTileSpawned;
    public event Action<int> OnTileMerged;

    public Game2048()
    {
        _random = new System.Random();
        _grid = new TileData[GridSize, GridSize];
        _tiles = new List<TileData>();
        _nextId = 1;
        _history = new List<GameStateSnapshot>(MaxHistory);
        BestScore = LoadBestScore();
    }

    public void StartNewGame()
    {
        for (int r = 0; r < GridSize; r++)
            for (int c = 0; c < GridSize; c++)
                _grid[r, c] = null;

        _tiles.Clear();
        Score = 0;
        MoveCount = 0;
        GameOver = false;
        Won = false;
        KeepPlaying = false;
        _nextId = 1;
        _history.Clear();
        _reviveUsed = false;

        SpawnRandomTile();
        SpawnRandomTile();

        OnStateChanged?.Invoke();
        OnScoreChanged?.Invoke(Score);
        OnGameOverChanged?.Invoke(false);
        OnWonChanged?.Invoke(false);
    }

    public bool Move(MoveDirection direction)
    {
        if (GameOver) return false;

        bool moved = MoveTiles(direction);

        if (!moved)
        {
            return false;
        }

        SaveState();

        MoveCount++;
        if (!KeepPlaying && CheckWin())
        {
            Won = true;
            OnWonChanged?.Invoke(true);
        }

        SpawnRandomTile();
        OnStateChanged?.Invoke();
        OnMoveExecuted?.Invoke(direction);

        if (CheckGameOver())
        {
            GameOver = true;
            SaveBestScore();
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
        return row >= 0 && row < GridSize && col >= 0 && col < GridSize && _grid[row, col] != null;
    }

    public TileData GetTileAt(int row, int col)
    {
        if (row >= 0 && row < GridSize && col >= 0 && col < GridSize)
            return _grid[row, col];
        return null;
    }

    public int GetEmptyCellCount()
    {
        int count = 0;
        for (int r = 0; r < GridSize; r++)
            for (int c = 0; c < GridSize; c++)
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

    private List<Vector2Int> GetEmptyCells()
    {
        var empty = new List<Vector2Int>();
        for (int r = 0; r < GridSize; r++)
            for (int c = 0; c < GridSize; c++)
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
        foreach (var t in _tiles) { t.PreviousRow = t.Row; t.PreviousCol = t.Col; t.JustMerged = false; t.MergedFromSelf = false; }

        bool horizontal = dir == MoveDirection.Left || dir == MoveDirection.Right;
        bool reverse = dir == MoveDirection.Right || dir == MoveDirection.Down;

        for (int line = 0; line < GridSize; line++)
        {
            var source = new List<TileData>(GridSize);
            for (int i = 0; i < GridSize; i++)
            {
                int r = horizontal ? line : i;
                int c = horizontal ? i : line;
                if (_grid[r, c] != null) source.Add(_grid[r, c]);
            }
            if (reverse) source.Reverse();

            var result = new List<TileData>(GridSize);
            for (int i = 0; i < source.Count; i++)
            {
                TileData current = source[i];
                TileData last = result.Count > 0 ? result[result.Count - 1] : null;
                if (last != null && last.Value == current.Value && !last.JustMerged)
                {
                    last.Value *= 2;
                    Score += last.Value;
                    OnScoreChanged?.Invoke(Score);
                    last.JustMerged = true;
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

            for (int i = 0; i < GridSize; i++)
            {
                int r = horizontal ? line : (reverse ? GridSize - 1 - i : i);
                int c = horizontal ? (reverse ? GridSize - 1 - i : i) : line;
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

        for (int r = 0; r < GridSize; r++)
        {
            for (int c = 0; c < GridSize; c++)
            {
                int val = _grid[r, c].Value;
                if (c < GridSize - 1 && _grid[r, c + 1].Value == val) return false;
                if (r < GridSize - 1 && _grid[r + 1, c].Value == val) return false;
            }
        }

        return true;
    }

    private int LoadBestScore()
    {
        return TikTokBridge.GetInt("BestScore_2048", 0);
    }

    private void SaveBestScore()
    {
        if (Score > BestScore)
        {
            BestScore = Score;
            TikTokBridge.SetInt("BestScore_2048", BestScore);
            TikTokBridge.Save();
        }
    }

    private struct GameStateSnapshot
    {
        public int[,] GridValues;
        public int Score;
        public int MoveCount;
        public bool KeepPlaying;
    }

    private void SaveState()
    {
        var snap = new GameStateSnapshot
        {
            GridValues = new int[GridSize, GridSize],
            Score = Score,
            MoveCount = MoveCount,
            KeepPlaying = KeepPlaying
        };
        for (int r = 0; r < GridSize; r++)
            for (int c = 0; c < GridSize; c++)
                snap.GridValues[r, c] = _grid[r, c]?.Value ?? 0;

        _history.Add(snap);
        if (_history.Count > MaxHistory)
            _history.RemoveAt(0);
    }

    public void Revive()
    {
        if (!CanRevive) return;

        _reviveUsed = true;
        _history.Clear();

        for (int r = 0; r < GridSize; r++)
            for (int c = 0; c < GridSize; c++)
                _grid[r, c] = null;
        _tiles.Clear();

        GameOver = false;
        Won = false;
        KeepPlaying = false;
        MoveCount = 0;

        SpawnRandomTile();
        SpawnRandomTile();

        OnStateChanged?.Invoke();
        OnScoreChanged?.Invoke(Score);
        OnGameOverChanged?.Invoke(false);
        OnWonChanged?.Invoke(false);
    }

    public void Undo()
    {
        if (!CanUndo) return;

        var snap = _history[_history.Count - 1];
        _history.RemoveAt(_history.Count - 1);

        for (int r = 0; r < GridSize; r++)
            for (int c = 0; c < GridSize; c++)
                _grid[r, c] = null;
        _tiles.Clear();

        Score = snap.Score;
        MoveCount = snap.MoveCount;
        KeepPlaying = snap.KeepPlaying;
        GameOver = false;
        Won = false;

        for (int r = 0; r < GridSize; r++)
        {
            for (int c = 0; c < GridSize; c++)
            {
                int val = snap.GridValues[r, c];
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
    }
}