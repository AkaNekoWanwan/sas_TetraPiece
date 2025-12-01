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

    // public bool _isSetGridPos = false;

    // private void OnValidate() {
    //     SetUpGridPos();
    // }

    // public void SetUpGridPos()
    // {
    //     // if(_isSetGridPos)
    //     //     return;
    //     // _isSetGridPos = true;
    //     Vector2Int pos = TextParser.ParseAnswerCoordinates(this.gameObject.name);
    //     gridX = pos.x;
    //     gridY = pos.y;
    // }
}
