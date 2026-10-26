using UnityEngine;

public class TileData
{
    public int Id { get; set; }
    public int Value { get; set; }
    public int Row { get; set; }
    public int Col { get; set; }
    public int PreviousRow { get; set; }
    public int PreviousCol { get; set; }
    public bool IsNew { get; set; }
    public bool JustMerged { get; set; }
    public bool MergedFromSelf { get; set; }

    public TileData(int id, int value, int row, int col)
    {
        Id = id;
        Value = value;
        Row = row;
        Col = col;
        PreviousRow = row;
        PreviousCol = col;
    }

    public Vector2Int Position => new Vector2Int(Row, Col);
    public Vector2Int PreviousPosition => new Vector2Int(PreviousRow, PreviousCol);
}