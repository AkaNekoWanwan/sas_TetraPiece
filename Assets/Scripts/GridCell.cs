using UnityEngine;

public class GridCell : MonoBehaviour
{
    public bool isOccupied = false;
    public bool isUpSide;

    // 今までは親 (PieceDragController) を持ってた
    // public PieceDragController occupiedBy;

    // 子オブジェクト単位にしたいので Transform に変更
    public Transform occupiedByChild;

    [Header("Grid Coordinates")]
    public int gridX;
    public int gridY;
}
