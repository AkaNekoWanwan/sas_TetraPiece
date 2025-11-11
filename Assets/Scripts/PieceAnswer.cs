using UnityEngine;

public class PieceAnswer : MonoBehaviour
{
    [Header("このピースがハマる正解セル群")]
    [Tooltip("StageGridParent の子セルをドラッグして順に登録")]
    public RectTransform[] answerCells; 
}
