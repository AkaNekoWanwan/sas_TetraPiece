using UnityEngine;

public class StageGridCell : MonoBehaviour
{
    [HideInInspector] public PieceDragger occupiedBy; // このセルに置かれているピース


    public bool IsOccupied => occupiedBy != null;

    public void SetOccupied(PieceDragger piece)
    {
        occupiedBy = piece;
    }

    public void Clear()
    {
        occupiedBy = null;
    }
}
