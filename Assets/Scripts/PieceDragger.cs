using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DG.Tweening;

[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(Image))]
[RequireComponent(typeof(PieceAnswer))]
public class PieceDragger : MonoBehaviour, 
    IPointerDownHandler, IPointerUpHandler, 
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private RectTransform rt;
    private Vector2 offset;

    [Header("スナップ設定")]
    public float snapDistance = 50f;

    private Vector3 originalPosition;
    private Vector3 originalScale;
    private PieceAnswer pieceAnswer;
    private bool isLocked = false; // 正解位置に固定されたかどうか

    void Start()
    {
        snapDistance = 5f; // デフォルト
        rt = GetComponent<RectTransform>();
        pieceAnswer = GetComponent<PieceAnswer>();
        originalScale = rt.localScale;

        var img = GetComponent<Image>();
        img.color = new Color(1, 1, 1, 0); // 完全透明
        img.raycastTarget = true;          // Raycastは受ける
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (isLocked) return; 
        rt.DOKill();
        rt.DOScale(Vector3.one, 0.2f).SetEase(Ease.OutQuad);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        // 何もしない
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (isLocked) return;
        originalPosition = rt.position;

        // === 自分が占有していたセルを解放 ===
        var gridCells = FindObjectsOfType<StageGridCell>();
        foreach (var cell in gridCells)
        {
            if (cell.occupiedBy == this)
                cell.Clear();
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rt, eventData.position, eventData.pressEventCamera, out offset);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (isLocked) return;
        Vector2 localPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rt.parent as RectTransform, eventData.position, eventData.pressEventCamera, out localPos);

        rt.anchoredPosition = localPos - offset;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (isLocked) return;

        Vector3 delta;
        StageGridCell[] targetCells;
        bool success = TryFindSnapDeltaAny(out delta, out targetCells);

        if (success && targetCells != null)
        {
            // === 他のピースと衝突していないか確認 ===
            foreach (var cell in targetCells)
            {
                if (cell.IsOccupied && cell.occupiedBy != this)
                {
                    ReturnWithShake();
                    return;
                }
            }

            // === スナップ成功 ===
            rt.position += delta;

            // セルを占有状態に更新
            foreach (var cell in targetCells)
                cell.SetOccupied(this);

            if (IsCorrectAnswer(targetCells))
            {
                Debug.Log($"{gameObject.name} 正解にはまりました！");
                isLocked = true; // ロック
            }
            else
            {
                Debug.Log($"{gameObject.name} 不正解だけど仮置きOK");
                // occupiedBy を残すので仮置きになる
            }
        }
        else
        {
            ReturnWithShake();
        }
    }

    private void ReturnWithShake()
    {
        Debug.Log($"{gameObject.name} returned (wrong place)");
        rt.DOShakeAnchorPos(0.2f, new Vector2(15f, 0f), 10, 0f)
          .OnComplete(() =>
          {
              rt.DOMove(originalPosition, 0.2f).SetEase(Ease.OutQuad);
              rt.DOScale(originalScale, 0.2f).SetEase(Ease.OutQuad);
          });
    }

    // === 正解判定 ===
    private bool IsCorrectAnswer(StageGridCell[] targetCells)
    {
        if (pieceAnswer.answerCells.Length != targetCells.Length) return false;
        for (int i = 0; i < targetCells.Length; i++)
        {
            if (pieceAnswer.answerCells[i] != targetCells[i].GetComponent<RectTransform>())
                return false;
        }
        return true;
    }

    // === 任意のセルにスナップできるか判定 ===
    private bool TryFindSnapDeltaAny(out Vector3 bestDelta, out StageGridCell[] targetCells)
    {
        bestDelta = Vector3.zero;
        targetCells = new StageGridCell[transform.childCount];

        StageGridCell[] allCells = FindObjectsOfType<StageGridCell>();
        if (allCells.Length == 0) return false;

        Transform firstChild = transform.GetChild(0);
        StageGridCell bestCell = null;
        float bestError = float.MaxValue;

        foreach (var candidate in allCells)
        {
            Vector3 delta = candidate.transform.position - firstChild.position;
            float totalError = 0f;
            bool valid = true;

            for (int i = 0; i < transform.childCount; i++)
            {
                Vector3 movedPos = transform.GetChild(i).position + delta;
                // 一番近いセルを探す
                float minDist = float.MaxValue;
                StageGridCell nearest = null;

                foreach (var cell in allCells)
                {
                    float dist = Vector2.Distance(movedPos, cell.transform.position);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        nearest = cell;
                    }
                }

                if (minDist > snapDistance)
                {
                    valid = false;
                    break;
                }

                totalError += minDist;
                targetCells[i] = nearest;
            }

            if (valid && totalError < bestError)
            {
                bestError = totalError;
                bestCell = candidate;
                bestDelta = delta;
            }
        }

        return bestCell != null;
    }
}
