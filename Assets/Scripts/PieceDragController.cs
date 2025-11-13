using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;
using System.Collections.Generic;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class PieceDragController : MonoBehaviour,
    IPointerDownHandler, IPointerUpHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Snap Settings")]
    public Transform gridParent;
    public Dictionary<Transform, Material> originalMaterials = new Dictionary<Transform, Material>();

    [Header("Outline Settings")]
    [Tooltip("アウトライン用子オブジェクトの名前 (部分一致)")]
    public string outlineObjectNamePattern = "Outline";

    [Header("Drag Settings")]
    [Tooltip("ドラッグ中のスムージング強度 (高いほど滑らか)")]
    [Range(0.05f, 0.5f)]
    public float smoothingFactor = 0.2f;

    private Vector3 initialScale;
    private float initialZ;

    private RectTransform rt;
    private Vector3 originalPos;
    private Vector3 originalScale;

    private List<GridCell> lastMarkedCells = new List<GridCell>();
    private Dictionary<Transform, GridCell> lastOccupiedMap = new Dictionary<Transform, GridCell>();
    public bool isLocked = false;
    private bool wasDragged = false;
    private Vector3 dragOffset;

    public List<TriangleCellCopyHandler> CellCopyHandlers = new List<TriangleCellCopyHandler>();
    public bool isCreative = false;

    private Tween _moveTween = null;
    
    // ドラッグ中の目標位置とスムージング
    private Vector3 smoothedPosition;
    private bool isDragging = false;
    public List<string> avoidPatternSeeds = default;
    public GridPieceListController _listCtrl = default;
    

    void Awake()
    {
        rt = GetComponent<RectTransform>();
        originalScale = rt.localScale;
        originalPos = rt.position;
        initialScale = rt.localScale;
        initialZ = rt.position.z;

        CacheOriginalMaterials();
        SetOutlineAlpha(1f, 0f);
        _listCtrl = GetComponentInParent<GridPieceListController>();
    }

    void Update()
    {
        // ドラッグ中は滑らかに補間した位置を使用
        if (isDragging && !isLocked)
        {
            rt.position = smoothedPosition;
        }
    }

    void CacheOriginalMaterials(Transform targetRoot = null)
    {
        if (targetRoot == null) targetRoot = transform;

        foreach (Transform child in targetRoot)
        {
            Image img = child.GetComponent<Image>();
            if (img != null && !originalMaterials.ContainsKey(child))
            {
                if (img.materialForRendering != null)
                {
                    originalMaterials[child] = new Material(img.materialForRendering);
                }
            }
            CacheOriginalMaterials(child);
        }
    }

    private Vector3 FixZ(Vector3 pos)
    {
        pos.z = initialZ;
        return pos;
    }

   

    void RestoreChildrenMaterials()
    {
        foreach (Transform child in transform)
        {
            if (originalMaterials.ContainsKey(child))
            {
                Image img = child.GetComponent<Image>();
                if (img != null)
                {
                    img.material = new Material(originalMaterials[child]);
                }
            }

            foreach (Transform grandChild in child)
            {
                if (originalMaterials.ContainsKey(grandChild))
                {
                    Image grandImg = grandChild.GetComponent<Image>();
                    if (grandImg != null)
                    {
                        grandImg.material = new Material(originalMaterials[grandChild]);
                        grandImg.gameObject.SetActive(true);
                    }
                }
            }
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (isLocked) return;
        wasDragged = true;
        isDragging = true;
        
        // DOTweenのアニメーションを停止して、直接制御に切り替え
        // DOTween.Kill(rt);
        _moveTween?.Kill();
        
        // 現在位置から開始
        smoothedPosition = rt.position;
        
        ReleaseOccupiedCells();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (isLocked) return;
        
        Vector3 worldPoint;
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
            rt, eventData.position, eventData.pressEventCamera, out worldPoint))
        {
            // 目標位置を計算
            Vector3 targetPosition = FixZ(worldPoint + dragOffset);

            float addY = targetPosition.y - originalPos.y;
            addY *= 1.0f;
            if( 0f <= addY)
                targetPosition.y += addY;
            
            // 指の細かい動きを無視するためにスムージング
            smoothedPosition = Vector3.Lerp(smoothedPosition, targetPosition, smoothingFactor);
        }
    }

  public void ReleaseOccupiedCells()
{
    if (gridParent == null) return;

    // ★ このピースが占有している全てのセルを解除
    foreach (Transform gridChild in gridParent)
    {
        GridCell cell = gridChild.GetComponent<GridCell>();
        if (cell != null && cell.isOccupied && cell.occupiedByChild != null)
        {
            if (cell.occupiedByChild.IsChildOf(transform))
            {
                Debug.Log($"[ReleaseOccupiedCells] セル {cell.name} の占有を解除 (占有していた子: {cell.occupiedByChild.name})");
                cell.isOccupied = false;
                cell.occupiedByChild = null;
            }
        }
    }
    lastMarkedCells.Clear();
    
    Debug.Log($"[ReleaseOccupiedCells] {gameObject.name} の占有セルを全て解除しました");
}


    void ReturnToOrigin()
    {
        _moveTween?.Kill();
        if(!isCreative)
            _moveTween = rt.DOMove(FixZ(originalPos), 0.2f).SetEase(Ease.OutQuad);
        rt.DOScale(originalScale, 0.15f).SetEase(Ease.OutBack);
    }

    void ReturnToOriginWithOccupancy()
    {
        _moveTween?.Kill();
        _moveTween = rt.DOMove(FixZ(originalPos), 0.2f).SetEase(Ease.OutQuad);
        rt.DOScale(originalScale, 0.15f).SetEase(Ease.OutBack);

        List<Transform> children = new List<Transform>();
        List<GridCell> cells = new List<GridCell>();

        foreach (var pair in lastOccupiedMap)
        {
            if (pair.Key != null && pair.Value != null)
            {
                children.Add(pair.Key);
                cells.Add(pair.Value);
            }
        }

        if (children.Count > 0)
        {
            MarkCells(children, cells, true);
        }
    }

    void SaveOccupiedCells()
    {
    }

    void RestoreOccupiedCells()
    {
    }


bool SnapChildrenToGridsAndRecenterParent()
{
    if (transform.childCount == 0)
    {
        Debug.LogWarning("スナップ失敗: 子が存在しません");
        return false;
    }

    // ★ 子オブジェクトとターゲットグリッドのペアを保存
    List<Transform> children = new List<Transform>();
    List<GridCell> targetCells = new List<GridCell>();
    HashSet<GridCell> usedCells = new HashSet<GridCell>();

    foreach (Transform child in transform)
    {
        children.Add(child);
        
        // ★ 全てのanswerGridの中から最も近いものを探す
        GridCell nearestAnswerCell = FindNearestAnswerGrid(child.position);
        
        if (nearestAnswerCell == null)
        {
            Debug.LogWarning($"スナップ失敗: {child.name} の最寄りのanswerGridが見つかりません");
            return false;
        }

        float distance = Vector2.Distance(child.position, nearestAnswerCell.transform.position);
        Debug.Log($"[最寄りanswerGrid] {child.name} → {nearestAnswerCell.name} (距離: {distance:F2})");

        // 占有チェック
        if (nearestAnswerCell.isOccupied && nearestAnswerCell.occupiedByChild != null)
        {
            if (!nearestAnswerCell.occupiedByChild.IsChildOf(transform))
            {
                Debug.LogWarning($"スナップ失敗: グリッド {nearestAnswerCell.name} は既に {nearestAnswerCell.occupiedByChild.name} に占有されています");
                return false;
            }
        }

        // 重複チェック
        if (usedCells.Contains(nearestAnswerCell))
        {
            Debug.LogWarning($"スナップ失敗: グリッド {nearestAnswerCell.name} に複数の子がスナップしようとしています");
            return false;
        }

        usedCells.Add(nearestAnswerCell);
        targetCells.Add(nearestAnswerCell);
    }

    // ★ 各子の最終的なワールド座標位置を計算（最も近いanswerGridの位置）
    List<Vector3> finalWorldPositions = new List<Vector3>();
    
    for (int i = 0; i < children.Count; i++)
    {
        Vector3 snapPosition = FixZ(targetCells[i].transform.position);
        finalWorldPositions.Add(snapPosition);
    }

    // ★ 新しい親の中心位置を計算
    Vector3 newParentCenter = Vector3.zero;
    foreach (var pos in finalWorldPositions)
    {
        newParentCenter += pos;
    }
    newParentCenter /= finalWorldPositions.Count;
    newParentCenter = FixZ(newParentCenter);

    // ★ 各子の現在のワールド座標を保存（親が動く前に）
    List<Vector3> currentWorldPositions = new List<Vector3>();
    foreach (var child in children)
    {
        currentWorldPositions.Add(child.position);
    }

    // ★ 親を瞬時に新しい中心に移動
    rt.position = newParentCenter;

    // ★ 子のワールド座標を元の位置に戻す（親が動いたのでローカル座標が変わっているため）
    for (int i = 0; i < children.Count; i++)
    {
        children[i].position = currentWorldPositions[i];
    }

    // ★ 子を回転させて、ワールド座標でアニメーション
    for (int i = 0; i < children.Count; i++)
    {
        float targetAngle = targetCells[i].transform.eulerAngles.z;
        children[i].rotation = Quaternion.Euler(0, 0, targetAngle);
        
        // ★ ワールド座標でアニメーション（親はもう正しい位置にいる）
        children[i].DOMove(finalWorldPositions[i], 0.3f).SetEase(Ease.Linear);
    }

    // ★ アニメーション完了後にセルをマーク
    DOVirtual.DelayedCall(0.3f, () =>
    {
        MarkCells(children, targetCells, true);
        Debug.Log($"スナップ完了: {gameObject.name}");
    });

    return true;
}

// ★ 全てのanswerGridの中から最も近いものを探す新しいメソッド
GridCell FindNearestAnswerGrid(Vector3 worldPos)
{
    float minDist = float.MaxValue;
    GridCell nearest = null;

    // gridParent配下の全GridCellをチェック
    foreach (Transform gridChild in gridParent)
    {
        GridCell gc = gridChild.GetComponent<GridCell>();
        if (gc != null)
        {
            // ★ このGridCellが誰かのanswerGridかどうかをチェック
            // (gridParent内の全てのセルをanswerGridとして扱う想定)
            float dist = Vector2.Distance(worldPos, gridChild.position);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = gc;
            }
        }
    }
    return nearest;
}
    void ApplyCellsAfterMaterial()
    {
        Material cellsAfterMaterial = Resources.Load<Material>("Materials/CellsAfter");
        
        if (cellsAfterMaterial == null)
        {
            Debug.LogWarning("CellsAfterマテリアルが見つかりません。パス: Resources/Materials/CellsAfter");
            return;
        }

        foreach (Transform child in transform)
        {
            Image img = child.GetComponent<Image>();
            if (img != null)
            {
                img.material = cellsAfterMaterial;
                Debug.Log($"Applied CellsAfter material to {child.name}");
            }
        }
    }

    void ResetChildren(List<Transform> children, List<Vector3> savedWorldPos, Vector3 parentBefore)
    {
        rt.position = FixZ(parentBefore);
        rt.DOScale(originalScale, 0.15f).SetEase(Ease.OutBack);

        for (int i = 0; i < children.Count; i++)
        {
            children[i].SetParent(transform, true);
            children[i].position = FixZ(savedWorldPos[i]);
        }
    }

    void ResetChildrenPartial(List<Transform> children, List<Vector3> savedWorldPos, Vector3 parentBefore, int processedIndex)
    {
        Debug.Log($"[ResetChildrenPartial] {gameObject.name} を復元開始 (処理中のインデックス: {processedIndex})");

        rt.position = FixZ(parentBefore);
        rt.DOScale(originalScale, 0.15f).SetEase(Ease.OutBack);

        for (int i = 0; i <= processedIndex && i < children.Count; i++)
        {
            Debug.Log($"  [{i}] {children[i].name} を {gameObject.name} の子に戻す (現在の親: {children[i].parent?.name})");
            children[i].SetParent(transform, true);
            children[i].position = FixZ(savedWorldPos[i]);
        }
    }

    public Tween ReturnToList()
    {
        return rt.DOScale(initialScale, 0.15f).SetEase(Ease.OutBack);
    }

    void MarkCells(List<Transform> children, List<GridCell> cells, bool occupied)
    {
        lastMarkedCells.Clear();

        for (int i = 0; i < children.Count && i < cells.Count; i++)
        {
            if (cells[i] != null)
            {
                cells[i].isOccupied = occupied;
                cells[i].occupiedByChild = occupied ? children[i] : null;
                lastMarkedCells.Add(cells[i]);
            }
        }

        if (occupied)
        {
            SaveOccupiedCellsFromMarked(children, cells);
        }
    }

    void SaveOccupiedCellsFromMarked(List<Transform> children, List<GridCell> cells)
    {
        lastOccupiedMap.Clear();

        for (int i = 0; i < children.Count && i < cells.Count; i++)
        {
            if (children[i] != null && cells[i] != null)
            {
                lastOccupiedMap[children[i]] = cells[i];
            }
        }
    }

    bool CheckAnswer()
    {
        foreach (Transform child in transform)
        {
            AnswerGridPos agp = child.GetComponent<AnswerGridPos>();
            if (agp != null && agp.answerGrid != null)
            {
                float dist = Vector2.Distance(child.position, agp.answerGrid.transform.position);
                if (dist > 1f) return false;
            }
        }
        
        FadeOutAnswerOutline();
        SnapChildrenZToAnswer();
        ApplyCellsAfterMaterial();
        RemoveChildrenMaterials();
        
        Debug.Log($"Piece {gameObject.name} completed the answer!");
        FindAnyObjectByType<StageManager>().CountDownPic();
        var iniscax = this.gameObject.GetComponent<RectTransform>().localScale;
        this.gameObject.GetComponent<RectTransform>().DOScale(iniscax * 1.1f, 0.07f).SetEase(Ease.Linear).OnComplete(() =>
        {
            this.gameObject.GetComponent<RectTransform>().DOScale(iniscax, 0.07f).SetEase(Ease.Linear);
        });
        return true;
    }

    void FadeOutAnswerOutline()
    {
        foreach (Transform child in transform)
        {
            AnswerGridPos agp = child.GetComponent<AnswerGridPos>();
            if (agp != null && agp.answerGrid != null)
            {
                Transform answerGrid = agp.answerGrid.transform;
                UnityEngine.UI.Outline outline = answerGrid.GetComponent<UnityEngine.UI.Outline>();
                if (outline != null)
                {
                    outline.DOFade(0f, 0.3f);
                }
            }
        }
    }

    void SnapChildrenZToAnswer()
    {
        foreach (Transform child in transform)
        {
            AnswerGridPos agp = child.GetComponent<AnswerGridPos>();
            if (agp != null && agp.answerGrid != null)
            {
                Vector3 pos = child.position;
                pos.z = agp.answerGrid.transform.position.z;
                child.position = pos;
            }
        }
    }

    void TryMergeNearbyPieces()
    {
        var allPieces = FindObjectsOfType<PieceDragController>();
        foreach (var other in allPieces)
        {
            if (other == this) continue;
            if (other.isLocked || this.isLocked) continue;

            float dist = Vector3.Distance(rt.position, other.transform.position);
            if (dist < 20f && CanMerge(other))
            {
                DoMerge(other);
                break;
            }
        }
    }

    bool CanMerge(PieceDragController other)
    {
        Transform myClosest = null;
        Transform otherClosest = null;
        float minDist = float.MaxValue;

        foreach (Transform myChild in transform)
        {
            var myAns = myChild.GetComponent<AnswerGridPos>();
            if (myAns == null || myAns.answerGrid == null) continue;

            foreach (Transform otherChild in other.transform)
            {
                var otherAns = otherChild.GetComponent<AnswerGridPos>();
                if (otherAns == null || otherAns.answerGrid == null) continue;

                float dist = Vector3.Distance(myChild.position, otherChild.position);
                if (dist < minDist)
                {
                    minDist = dist;
                    myClosest = myChild;
                    otherClosest = otherChild;
                }
            }
        }

        if (myClosest == null || otherClosest == null)
            return false;

        var myAnsGrid = myClosest.GetComponent<AnswerGridPos>().answerGrid;
        var otherAnsGrid = otherClosest.GetComponent<AnswerGridPos>().answerGrid;

        if (myAnsGrid == null || otherAnsGrid == null)
            return false;

        // GridCell g1 = myAnsGrid.GetComponent<GridCell>();
        // GridCell g2 = otherAnsGrid.GetComponent<GridCell>();
        // if (g1 == null || g2 == null || !AreGridCoordsValid(g1, g2))
        // {
        //     // グリッド座標がない場合は、従来のワールド距離判定にフォールバックするか、エラーとする
        //     return FallbackWorldDistanceCheck(); 
        // }

        // 隣接判定用のユーティリティメソッドを導入（下記B参照）
        // if (!IsLogicalGridAdjacent(myAnsGrid, otherAnsGrid, _listCtrl.ShapeType))
        // {
        //     Debug.Log($"[CanMerge] ❌ 物理的に隣接していません: {myAnsGrid.name} と {otherAnsGrid.name}");
        //     return false; // 物理的に隣接していない場合は即座にマージを拒否
        // }
        
        Vector3 ansRel = otherAnsGrid.transform.position - myAnsGrid.transform.position;
        Vector3 curRel = otherClosest.position - myClosest.position;

        float ansLen = ansRel.magnitude;
        float curLen = curRel.magnitude;
        float lenDiff = Mathf.Abs(ansLen - curLen);

        float dot = Vector3.Dot(ansRel.normalized, curRel.normalized);
        float angleDiff = Mathf.Acos(Mathf.Clamp(dot, -1f, 1f)) * Mathf.Rad2Deg;

        float zAngleAnswer = otherAnsGrid.transform.eulerAngles.z - myAnsGrid.transform.eulerAngles.z;
        float zAngleCurrent = otherClosest.eulerAngles.z - myClosest.eulerAngles.z;
        float zAngleDiff = Mathf.Abs(Mathf.DeltaAngle(zAngleAnswer, zAngleCurrent));

        // bool distOK = lenDiff < 2f;
        // bool dirOK = angleDiff < 5f;
        // bool rotOK = zAngleDiff < 5f;
        bool distOK = lenDiff < 0.5f;
        bool dirOK = angleDiff < 2f;
        bool rotOK = zAngleDiff < 2f;

        Debug.Log($"[CanMerge] {minDist}");
        if (distOK && dirOK && rotOK && minDist < 10f)
        {
            Debug.Log($"[CanMerge] ✅ 距離OK({Vector3.Distance(myAnsGrid.transform.position, otherAnsGrid.transform.position):F2}) 向きOK({Vector3.Distance(myClosest.transform.position, otherClosest.transform.position):F2}) 回転OK({zAngleDiff:F2}) between {myClosest.name} and {otherClosest.name}");
            return true;
        }
        else
        {
            Debug.Log($"[CanMerge] ❌ mismatch: 距離Δ={lenDiff:F2}, 向きΔ={angleDiff:F2}, 回転Δ={zAngleDiff:F2}");
            return false;
        }
    }

    // PieceDragController.cs に新しいプライベートメソッドを追加

    // PieceDragController.cs に追加するメソッド

    // ShapeType は外部で定義されている enum ShapeType { Square, Hex, Triangle } を使用

    private bool IsLogicalGridAdjacent(GridCell g1, GridCell g2, ShapeType currentShape)
    {
        // ピース同士が同じセルを参照している場合は隣接ではない
        if (g1 == g2) return false;

        int dx = Mathf.Abs(g1.gridX - g2.gridX);
        int dy = Mathf.Abs(g1.gridY - g2.gridY);
        
        switch (currentShape)
        {
            case ShapeType.Square:
                // 四角形グリッドの隣接判定 (上下左右のみ)
                // 座標差の合計がちょうど1であること
                return (dx == 1 && dy == 0) || (dx == 0 && dy == 1);

            case ShapeType.Hex:
                // 六角形 (スタッガード/オフセット) グリッドの隣接判定
                // xが偶数か奇数かで隣接Y座標が変わる
                
                // ----------------------------------------------------
                // A. X座標の差が0 (垂直方向の隣接)
                if (dx == 0)
                {
                    // 上または下への隣接のみ
                    return dy == 1; 
                }
                
                // B. X座標の差が1 (斜め方向の隣接)
                if (dx == 1)
                {
                    // 隣接X座標が偶数（例: g1.gridXが偶数）の場合
                    if (g1.gridX % 2 == 0)
                    {
                        // 斜め上 (dy=1) または 水平 (dy=0) に隣接
                        return dy == 0 || dy == 1;
                    }
                    // 隣接X座標が奇数（例: g1.gridXが奇数）の場合
                    else
                    {
                        // 斜め下 (dy=0) または 水平 (dy=1) に隣接
                        return dy == 0 || dy == 1; 
                    }
                }
                
                return false; // x, y 差分が大きすぎる場合は非隣接

            case ShapeType.Triangle:
                // 三角形グリッドの隣接判定 (複雑なため、座標系に合わせて調整が必要)
                // 少なくとも四角形と同様の論理で斜めを排除する必要がありますが、
                // isUpSide や gridZ (もしあれば) を使って3方向の隣接を厳密に定義する必要があります。
                // ここでは簡易的に、四角形よりは緩いが、斜め対角線は排除するロジックを想定します。
                
                // 三角形グリッドの実装詳細によるため、四角形ロジックをベースに調整が必要です。
                // 暫定的に、辺を共有する隣接のみを許可する（四角形と同様のロジックが妥当な場合がある）
                return (dx == 1 && dy == 0) || (dx == 0 && dy == 1) || (dx == 1 && dy == 1); // 3方向に隣接する場合

            default:
                // 未定義の形状の場合、安全のため隣接を拒否
                return false;
        }
    }

    void DoMerge(PieceDragController other)
    {
        DOTween.Kill(this.rt);
        CellCopyHandlers.AddRange(other.CellCopyHandlers);

        this.ReleaseOccupiedCells();
        other.ReleaseOccupiedCells();

        foreach (var kvp in other.originalMaterials)
        {
            if (!this.originalMaterials.ContainsKey(kvp.Key))
            {
                this.originalMaterials[kvp.Key] = kvp.Value;
            }
        }

        List<Transform> children = new List<Transform>();
        foreach (Transform c in other.transform) children.Add(c);
        foreach (Transform c in children) c.SetParent(transform, true);

        Destroy(other.gameObject, 0.05f);

        bool snapResult = SnapChildrenToGridsAndRecenterParent();
        RemoveChildrenMaterials();
        FindAnyObjectByType<StageManager>().CountDownPic();

        if(1 <= CellCopyHandlers.Count)
            StartCoroutine(CellCopyHandlers[0].UpdateAllCellCopyTransformCoroutine(CellCopyHandlers));
    }

    GridCell FindClosestGrid(Vector3 worldPos)
    {
        float minDist = float.MaxValue;
        GridCell nearest = null;

        foreach (Transform child in gridParent)
        {
            float dist = Vector2.Distance(worldPos, child.position);
            GridCell gc = child.GetComponent<GridCell>();
            if (gc != null && dist < minDist)
            {
                minDist = dist;
                nearest = gc;
            }
        }
        return nearest;
    }

    void SetOutlineAlpha(float targetAlpha, float duration)
    {
        foreach (Transform child in transform)
        {
            TriangleCellCopyHandler triOutlineHandler = child.GetComponent<TriangleCellCopyHandler>();
            float scale = 1.1f;
            if(isCreative)
                scale = 1.03f;
            if(triOutlineHandler != null)
                scale = triOutlineHandler.Scale;
            foreach (Transform grandChild in child)
            {
                Image img = grandChild.GetComponent<Image>();
                if (targetAlpha == 1f)
                {
                    img.GetComponent<RectTransform>().localScale = Vector3.one * scale;
                }
                else
                {
                    img.GetComponent<RectTransform>().localScale = Vector3.one;
                }
                if (img != null)
                {
                    Debug.Log($"Setting outline alpha for {grandChild.name} to {targetAlpha} over {duration}s");
                    if (img.material.name.IndexOf("(Instance)") == -1)
                    {
                        img.material = Instantiate(img.material);
                    }

                    DOTween.Kill(img.material);
                   
                    img.material.DOFade(targetAlpha, duration).OnComplete(() =>
                    {
                    });
                }
            }
        }
    }

    void RemoveChildrenMaterials()
    {
        foreach (Transform child in transform)
        {
            Image img = child.GetComponent<Image>();
            if (img != null) img.material = null;

            foreach (Transform grandChild in child)
            {
                Image grandImg = grandChild.GetComponent<Image>();
                // if (grandImg != null) grandImg.material = null;
                if (grandImg != null) grandChild.gameObject.SetActive(false);
            }
        }
    }

    [ContextMenu("Recenter Parent To Children")]
    public void RecenterParentToChildren(bool isCreative = false)
    {
        isCreative = isCreative;
        if (transform.childCount == 0)
        {
            Debug.LogWarning("子オブジェクトがありません");
            return;
        }
        
        List<Transform> children = new List<Transform>();
        List<Vector3> savedWorldPos = new List<Vector3>();
        CellCopyHandlers = new List<TriangleCellCopyHandler>();
        foreach (Transform child in transform)
        {
            children.Add(child);
            savedWorldPos.Add(child.position);
            TriangleCellCopyHandler CellCopyHandler = child.GetComponent<TriangleCellCopyHandler>();
            if(CellCopyHandler != null) 
                CellCopyHandlers.Add(CellCopyHandler);
        }

        foreach (var c in children)
        {
            c.SetParent(null, true);
        }

        Vector3 center = Vector3.zero;
        foreach (var pos in savedWorldPos) center += pos;
        center /= savedWorldPos.Count;

        transform.position = FixZ(center);

        for (int i = 0; i < children.Count; i++)
        {
            children[i].SetParent(transform, true);
            children[i].position = FixZ(savedWorldPos[i]);
        }

        Debug.Log($"Recentered {gameObject.name} to children center at {center}");

        if( 1 <= CellCopyHandlers.Count )
            CellCopyHandlers[0].UpdateAllCellCopyTransform(CellCopyHandlers);
    }

    // ★ 元のRenderQueueを保存する辞書を追加
private Dictionary<Material, int> originalRenderQueues = new Dictionary<Material, int>();

public void OnPointerDown(PointerEventData eventData)
{
    if (isLocked) return;
    transform.SetAsLastSibling();
    var hand = FindAnyObjectByType<HandCursorController>();
    originalPos = rt.position;
    originalScale = initialScale;
    wasDragged = false;

    rt.DOScale(Vector3.one, 0.1f).SetDelay(0.06f).SetEase(Ease.OutBack);
    RestoreChildrenMaterials();
    SetOutlineAlpha(1f, 0f);
    
    // ★ RenderQueueを変更
    SetRenderQueue(3004, 3003);

    Vector3 worldPoint;
    if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
        rt, eventData.position, eventData.pressEventCamera, out worldPoint))
    {
        dragOffset = rt.position - worldPoint;
        Vector3 targetPos = FixZ(worldPoint + dragOffset);
        smoothedPosition = targetPos;
        _moveTween?.Kill();
        _moveTween = rt.DOMove(targetPos, 0.2f).SetDelay(0.13f).SetEase(Ease.OutQuad);
    }
}

public void OnEndDrag(PointerEventData eventData)
{
    if (isLocked) return;

    // rt.transform.localScale = Vector3.one;
    rt.DOScale(Vector3.one, 0.1f).SetDelay(0.06f).SetEase(Ease.OutBack);
    
    isDragging = false;

    bool snapStarted = SnapChildrenToGridsAndRecenterParent();
    if (!snapStarted)
    {
        ReleaseOccupiedCells();
        
        // ★ RenderQueueを元に戻す
        RestoreRenderQueue();
        
        // ReturnToOrigin();
        SetOutlineAlpha(1f, 0.2f);
        // var _listCtrl = GetComponentInParent<GridPieceListController>();
        if (_listCtrl != null) _listCtrl.NotifyReturned(this);

        rt.DOScale(originalScale, 0.15f).SetEase(Ease.OutBack);
        return;
    }

    DOVirtual.DelayedCall(0.4f, () =>
    {
        var listCtrlSuccess = GetComponentInParent<GridPieceListController>();
        if (listCtrlSuccess != null) listCtrlSuccess.NotifySnapped(this);

        // ★ RenderQueueを元に戻す
        RestoreRenderQueue();

        if (CheckAnswer() && !isCreative)
        {
            isLocked = true;
            SetOutlineAlpha(0f, 0f);
        }
        else
        {
            RestoreChildrenMaterials();
            SetOutlineAlpha(1f, 0f);
        }

        TryMergeNearbyPieces();
    });
}

public void OnPointerUp(PointerEventData eventData)
{
    if (!wasDragged && !isLocked)
    {
        isDragging = false;
        var hand = FindAnyObjectByType<HandCursorController>();
        
        // ★ RenderQueueを元に戻す
        RestoreRenderQueue();
        
        ReturnToOrigin();
    }
}

// ★ RenderQueueを設定する新しいメソッド
void SetRenderQueue(int cellQueue, int outlineQueue)
{
    originalRenderQueues.Clear();
    
    foreach (Transform child in transform)
    {
        // 子セル（cell）のマテリアル
        Image cellImg = child.GetComponent<Image>();
        if (cellImg != null && cellImg.material != null)
        {
            // 元のRenderQueueを保存
            if (!originalRenderQueues.ContainsKey(cellImg.material))
            {
                originalRenderQueues[cellImg.material] = cellImg.material.renderQueue;
            }
            cellImg.material.renderQueue = cellQueue;
            Debug.Log($"[RenderQueue] {child.name} のセルを {cellQueue} に設定");
        }

        // Outlineのマテリアル
        foreach (Transform grandChild in child)
        {
            Image outlineImg = grandChild.GetComponent<Image>();
            if (outlineImg != null && outlineImg.material != null)
            {
                // 元のRenderQueueを保存
                if (!originalRenderQueues.ContainsKey(outlineImg.material))
                {
                    originalRenderQueues[outlineImg.material] = outlineImg.material.renderQueue;
                }
                outlineImg.material.renderQueue = outlineQueue;
                Debug.Log($"[RenderQueue] {grandChild.name} のアウトラインを {outlineQueue} に設定");
            }
        }
    }
}

// ★ RenderQueueを元に戻すメソッド
void RestoreRenderQueue()
{
    foreach (var kvp in originalRenderQueues)
    {
        if (kvp.Key != null)
        {
            kvp.Key.renderQueue = kvp.Value;
            Debug.Log($"[RenderQueue] マテリアルを元の {kvp.Value} に復元");
        }
    }
    originalRenderQueues.Clear();
}
}

public static class ShadowExtensions
{
    public static Tweener DOFade(this Shadow shadow, float endValue, float duration)
    {
        if (duration != 0f)
        {
            Color c = shadow.effectColor;
            return DOTween.To(() => c.a, x =>
            {
                c.a = x;
                shadow.effectColor = c;
            }, endValue, duration);
        }
        else
        {
            Color c = shadow.effectColor;
            c.a = endValue;
            shadow.effectColor = c;
            return null;
        }
    }
}