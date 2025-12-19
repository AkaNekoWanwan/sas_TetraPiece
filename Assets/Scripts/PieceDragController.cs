using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;
using System.Collections.Generic;
using UnityEngine.UI;
using System.Linq;
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
    public Vector3 originalScale;
    public bool isSetOriginalScale = false;
    public Vector3 OriginalScale{ get { return originalScale; } set { originalScale = value; isSetOriginalScale = true;} }

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
    
    // 現在のドラッグ中のマウス位置（スクリーン座標）
    private Vector2 currentDragScreenPosition;
    public Vector2 CurrentDragScreenPosition => currentDragScreenPosition;
    
    /// <summary>
    /// addYが0の場合に現在のピース位置にするために必要なマウスのスクリーン座標を取得
    /// </summary>
    public Vector2 GetRequiredMousePositionForNoShift()
    {
        // addY = 0 の条件: targetPosition.y == originalPos.y
        // targetPosition = worldPoint + dragOffset なので
        // originalPos.y = worldPoint.y + dragOffset.y
        // よって worldPoint.y = originalPos.y - dragOffset.y
        
        Vector3 requiredWorldPoint = new Vector3(
            rt.position.x - dragOffset.x,
            originalPos.y - dragOffset.y,
            rt.position.z - dragOffset.z
        );
        
        // ワールド座標をスクリーン座標に変換
        Camera camera = GetCanvasCamera();
        Vector2 screenPos;
        
        // if (RectTransformUtility.WorldPointToScreenPoint(camera, requiredWorldPoint, out screenPos))
        // {
        //     return screenPos;
        // }
        
        return currentDragScreenPosition; // フォールバック
    }
    
    /// <summary>
    /// RectTransformが属するCanvasのカメラを取得
    /// </summary>
    private Camera GetCanvasCamera()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                return null;
            }
            else if (canvas.worldCamera != null)
            {
                return canvas.worldCamera;
            }
        }
        return Camera.main;
    }
    
    public List<string> avoidPatternSeeds = default;
    public GridPieceListController _listCtrl = default;
    public List<AnswerGridPos> _answerGridPoses = default;

    public List<Vector2Int> _cellsPositions = null;
	public List<Vector2Int> _comparisonPositions = null;
    public bool IsRandomPiece = false;  // 選択肢３個のうち１つのランダム枠か
    private Vector2Int workPos = Vector2Int.zero;
    private Vector3 lastSnappedPos = Vector3.zero;
    private int addQueue = 0;
    private StageManager _stageManager = default;
    private bool _isMove = false;

    void Awake()
    {
        rt = GetComponent<RectTransform>();
        if(!isSetOriginalScale)
            originalScale = rt.localScale;
        originalPos = rt.position;
        initialScale = rt.localScale;
        initialZ = rt.position.z;

        CacheOriginalMaterials();
        _listCtrl = GetComponentInParent<GridPieceListController>();
        SetOutlineAlpha(1f, 0f, true);

        // AnswerGridPos[] cells = GetComponentsInChildren<AnswerGridPos>(false);
        _answerGridPoses = GetComponentsInChildren<AnswerGridPos>(false).ToList();
        for (int i = 0; i < _answerGridPoses.Count; i++)
		{
			workPos.x = _answerGridPoses[i].x;
			workPos.y = _answerGridPoses[i].y;
			_cellsPositions.Add(workPos);
            // if(_answerGridPoses[i].shadowTransform != null)
            //     _answerGridPoses[i].shadowTransform.gameObject.SetActive(false);
		}
        _stageManager = FindAnyObjectByType<StageManager>();
    }

    void OnDestroy()
    {
        // オブジェクト破棄時にドラッグ状態から削除
        DragStateManager.UnregisterDrag(this);
    }

    // private void OnValidate()
    // {
    //     if( 1 <= CellCopyHandlers.Count )
    //         CellCopyHandlers[0].UpdateAllCellCopyTransform(CellCopyHandlers);
    // }

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
        // foreach (AnswerGridPos child in _answerGridPoses)
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
                if(grandChild == child.GetComponent<AnswerGridPos>().shadowTransform)
                    continue;
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
        
        // ドラッグ状態を登録
        DragStateManager.RegisterDrag(this);
        
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
        
        // マウス位置を保存
        currentDragScreenPosition = eventData.position;
        
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
        // Debug.Log("サイズ不具合チェック：１");
        _moveTween?.Kill();
        if(!isCreative)
            _moveTween = rt.DOMove(FixZ(originalPos), 0.2f).SetEase(Ease.OutQuad);
        rt.DOScale(originalScale, 0.15f).SetEase(Ease.OutBack);
    }

    void ReturnToOriginWithOccupancy()
    {
        // Debug.Log("サイズ不具合チェック：２");
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
    _comparisonPositions = new List<Vector2Int>();

    foreach (Transform child in transform)
    {
        children.Add(child);
        
        // ★ 全てのanswerGridの中から最も近いものを探す
        GridCell nearestAnswerCell = FindNearestAnswerGrid(child.position, child);
        
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

        workPos.x = nearestAnswerCell.gridX;
		workPos.y = nearestAnswerCell.gridY;
        _comparisonPositions.Add(workPos);
    }
    if (!ShapeComparer.CheckShapeEquality(_cellsPositions, _comparisonPositions, _listCtrl.ShapeType))
    {
        Debug.LogWarning($"スナップ失敗: 形状の相対位置の不一致");
        return false;
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
    // ★ 追加: スナップ成功時の位置を記録
    if(lastSnappedPos == newParentCenter)
        _isMove = false;
    else
        _isMove = true;

    lastSnappedPos = newParentCenter;

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
        // バイブレーション
        VibratorManager.Vibrate(70, 40);
    });

    return true;
}

// ★ 全てのanswerGridの中から最も近いものを探す新しいメソッド
GridCell FindNearestAnswerGrid(Vector3 worldPos, Transform child)
{
    float minDist = float.MaxValue;
    GridCell nearest = null;
    AnswerGridPos agp = null;
    
    if(_listCtrl.ShapeType == ShapeType.Triangle)
        agp = child.gameObject.GetComponent<AnswerGridPos>();

    // gridParent配下の全GridCellをチェック
    foreach (Transform gridChild in gridParent)
    {
        GridCell gc = gridChild.GetComponent<GridCell>();
        if (gc != null)
        {
            // 三角形なら上下の向きも見る
            if(_listCtrl.ShapeType == ShapeType.Triangle)
            {
                if(agp != null)
                {
                    if(agp.isUpSide != gc.isUpSide)
                        continue;
                }
                else
                {
                    Debug.Log($"FindNearestAnswerGrid：三角形でAnswerGridPosが取得できなかったよ:{gridChild.name}");
                }
            }
            // ★ このGridCellが誰かのanswerGridかどうかをチェック
            // (gridParent内の全てのセルをanswerGridとして扱う想定)
            float dist = Vector2.Distance(worldPos, gridChild.position);
            if (dist < minDist )
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
        // Debug.Log("サイズ不具合チェック：３");
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

        // Debug.Log("サイズ不具合チェック：４");
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
        // Debug.Log("サイズ不具合チェック：５");
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
        // foreach (AnswerGridPos agp in _answerGridPoses)
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
        _stageManager.CountDownPic();
        return true;
    }

    void FadeOutAnswerOutline()
    {
        foreach (Transform child in transform)
        // foreach (AnswerGridPos agp in _answerGridPoses)
        {
            AnswerGridPos agp = child.GetComponent<AnswerGridPos>();
            if (agp != null && agp.answerGrid != null)
            {
                Transform answerGrid = agp.answerGrid.transform;
                var outlines = answerGrid.GetComponents<UnityEngine.UI.Outline>();
                for(int i = 0; i < outlines.Length; i++)
                {
                    outlines[i].DOFade(0f, 0.3f);
                }
            }
        }
    }

    void SnapChildrenZToAnswer()
    {
        foreach (Transform child in transform)
        // foreach (AnswerGridPos agp in _answerGridPoses)
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

    // void TryMergeNearbyPieces()
    // {
    //     var allPieces = FindObjectsOfType<PieceDragController>();
    //     foreach (var other in allPieces)
    //     {
    //         if (other == this) continue;
    //         if (other.isLocked || this.isLocked) continue;

    //         float dist = Vector3.Distance(rt.position, other.transform.position);
    //         if (dist < 20f && CanMerge(other))
    //         {
    //             DoMerge(other);
    //             break;
    //         }
    //     }
    // }

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

        // // ★ 1. GridCellコンポーネントの取得
        GridCell myCell = myAnsGrid.GetComponent<GridCell>();
        GridCell otherCell = otherAnsGrid.GetComponent<GridCell>();

        if (myCell == null || otherCell == null)
        {
             Debug.LogWarning("[CanMerge] 正解グリッドに GridCell コンポーネントがありません。");
             return false;
        }

        // ★ 2. 論理的な隣接チェック (ここではShapeType.Squareを仮定)
        //     もし実際の形状が異なる場合は、適切なShapeTypeを特定し、
        //     IsLogicalGridAdjacent(myCell, otherCell, 実際のShapeType) を呼び出す必要があります。
        // ShapeType currentShape = ShapeType.Square; // ← 実際の形状に応じて修正してください
        // bool isLogicallyAdjacent = IsLogicalGridAdjacent(myCell, otherCell, currentShape);

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
        if (distOK && dirOK && rotOK && minDist < 5f)
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
        Debug.Log($"OnEndDrag.DoMerge:配置チェック:{snapResult}");
        RemoveChildrenMaterials();
        _stageManager.CountDownPic();

        if(1 <= CellCopyHandlers.Count)
            StartCoroutine(CellCopyHandlers[0].UpdateAllCellCopyTransformCoroutine(CellCopyHandlers));


        _cellsPositions.Clear();
        AnswerGridPos[] cells = GetComponentsInChildren<AnswerGridPos>(false);
        for (int i = 0; i < cells.Length; i++)
        // for (int i = 0; i < _answerGridPoses.Count; i++)
		{
			workPos.x = cells[i].x;
			workPos.y = cells[i].y;
			_cellsPositions.Add(workPos);
		}
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

    void SetOutlineAlpha(float targetAlpha, float duration, bool isFirst = false)
    {
        // _answerGridPoses
        foreach (Transform child in transform)
        {
            TriangleCellCopyHandler triOutlineHandler = child.GetComponent<TriangleCellCopyHandler>();
            // float scale = 1.05f;

            float scale = 1.00f;
            if(isCreative || GameConst.IsCreativeMode())
                scale = 0f;
            // if(triOutlineHandler != null)
            //     scale = triOutlineHandler.Scale;
            RectTransform childRect = child.GetComponent<RectTransform>();
            AnswerGridPos gridPos = child.GetComponent<AnswerGridPos>();
            RectTransform outLineRect = gridPos.outLine;
            if(outLineRect != null)
            {
                UpdateOutLine(outLineRect, childRect, scale, targetAlpha, duration, isFirst);
            }
            else
            {
                foreach (Transform grandChild in child)
                {
                    if(grandChild == gridPos.shadowTransform)
                        continue;
                    outLineRect = grandChild.GetComponent<RectTransform>();
                    UpdateOutLine(outLineRect, childRect, scale, targetAlpha, duration, isFirst);
                }
            }
            // foreach (Transform grandChild in child)
            // {
            //     RectTransform grandChildRect = grandChild.GetComponent<RectTransform>();
            
            // }
        }
    }

    void UpdateOutLine(RectTransform outLine, RectTransform cellRT, float scale, float targetAlpha, float duration, bool isFirst)
    {
        // アウトラインの大きさ設定
        if(isFirst)
        {
            Vector2 setSize = cellRT.sizeDelta;
            if(_listCtrl.ShapeType == ShapeType.Hex)
            {
                setSize.x += 13f;
                setSize.y += 13f;
            }
            if(_listCtrl.ShapeType == ShapeType.Square)
            {
                setSize.x += 4.25f;
                setSize.y += 4.25f;
            }
            if(_listCtrl.ShapeType != ShapeType.Triangle)
                outLine.sizeDelta = setSize;
        }

        Image img = outLine.gameObject.GetComponent<Image>();
        if (targetAlpha == 1f)
        {
            outLine.localScale = Vector3.one * scale;
        }
        else
        {
            outLine.localScale = Vector3.one;
        }
        if (img != null)
        {
            // Debug.Log($"Setting outline alpha for {grandChild.name} to {targetAlpha} over {duration}s");
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
        if(!isSetOriginalScale)
            originalScale = initialScale;
        wasDragged = false;

        if(rt.localScale == Vector3.one)
        {
            rt.localScale = Vector3.one * 0.90f;
            rt.DOScale(Vector3.one, 0.25f).SetEase(Ease.OutBack);
        }
        else
            rt.DOScale(Vector3.one, 0.1f).SetDelay(0.06f).SetEase(Ease.OutBack);
        RestoreChildrenMaterials();
        SetOutlineAlpha(1f, 0f);

        // アウトラインを非表示にする
        SetOutlineAlpha(0f, 0.1f);

        // ★ RenderQueueを変更
        SetRenderQueue(3004 + addQueue, 3003 + addQueue);
        addQueue += 2;

        VibratorManager.Vibrate(70, 40);

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

        AudioManager.Instance.PlayHoldSound();
        SetActiveShadow(false);
    }

    public void SetActiveShadow(bool isActive)
    {
        foreach (AnswerGridPos agp in _answerGridPoses)
        {
            agp.shadowTransform.gameObject.SetActive(isActive);
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (isLocked) return;

        // ドラッグ状態を解除
        DragStateManager.UnregisterDrag(this);

        // rt.transform.localScale = Vector3.one;
        rt.DOScale(Vector3.one, 0.1f).SetDelay(0.06f).SetEase(Ease.OutBack);
        AudioManager.Instance.PlayPlaceSound();
        isDragging = false;

        bool snapStarted = SnapChildrenToGridsAndRecenterParent();
        Debug.Log($"OnEndDrag.snapStarted:配置チェック:{snapStarted}");
        if (!snapStarted)
        {
            ReleaseOccupiedCells();
            RestoreRenderQueue();
            SetOutlineAlpha(1f, 0.2f);
            
            // ★ 分岐ロジック: 最後にスナップされた位置があるか？
            if (lastSnappedPos != Vector3.zero) 
            {
                // 1. 盤面に一度置かれたことがある場合
                ReturnToLastSnappedPosition(); // 盤面の最後位置に戻る (シェイクあり)
            }
            else
            {
                // 2. リストから初めてドラッグされた場合
                // リストに戻す (シェイクなし)
                if (_listCtrl != null) 
                {
                    // ★ 変更箇所: shouldShake: false を渡してシェイクを抑制
                    _listCtrl.NotifyReturned(this, shouldShake: false); 
                }
                // Debug.Log("サイズ不具合チェック：５");
                rt.DOScale(originalScale, 0.15f).SetEase(Ease.OutBack);
                SetActiveShadow(true);
            }
            
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

                Sequence seq = DOTween.Sequence();
                foreach (Transform child in transform)
                // foreach (AnswerGridPos agp in _answerGridPoses)
                {
                    AnswerGridPos agp = child.GetComponent<AnswerGridPos>();
                    if (agp != null && agp.answerGrid != null)
                    {
                        agp.answerGrid.SetActive(false);
                    }
                }
                AudioManager.Instance.PlayMergeSound();
                var iniscax = this.gameObject.GetComponent<RectTransform>().localScale;
                seq.Append(this.gameObject.GetComponent<RectTransform>().DOScale(iniscax * 1.03f, 0.12f).SetEase(Ease.Linear));
                seq.Append(this.gameObject.GetComponent<RectTransform>().DOScale(iniscax, 0.15f).SetEase(Ease.Linear));
            }
            else
            {
                RestoreChildrenMaterials();
                SetOutlineAlpha(1f, 0f);
            }

            // TryMergeNearbyPieces();
        });
        if(_isMove)
            _stageManager.CountDownMove();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!wasDragged && !isLocked)
        {
            isDragging = false;
            var hand = FindAnyObjectByType<HandCursorController>();
            
            // ★ RenderQueueを元に戻す
            RestoreRenderQueue();
            
            // ReturnToOrigin();
        }
        if(lastSnappedPos == Vector3.zero && !isDragging)
        {
            rt.DOScale(originalScale, 0.15f).SetEase(Ease.OutBack);
        }
    }

    // ★ RenderQueueを設定する新しいメソッド
    void SetRenderQueue(int cellQueue, int outlineQueue)
    {
        originalRenderQueues.Clear();
        
        // foreach (Transform child in transform)
        foreach(AnswerGridPos agp in _answerGridPoses)
        {
            // 子セル（cell）のマテリアル
            Image cellImg = agp.transform.GetComponent<Image>();
            if (cellImg != null && cellImg.material != null)
            {
                // 元のRenderQueueを保存
                if (!originalRenderQueues.ContainsKey(cellImg.material))
                {
                    originalRenderQueues[cellImg.material] = cellImg.material.renderQueue;
                }
                cellImg.material.renderQueue = cellQueue;
                // Debug.Log($"[RenderQueue] {child.name} のセルを {cellQueue} に設定");
            }

            // Outlineのマテリアル
            foreach (Transform grandChild in agp.transform)
            {
                Image outlineImg = grandChild.GetComponent<Image>();
                if (outlineImg != null && outlineImg.material != null)
                {
                    // 元のRenderQueueを保存
                    if (!originalRenderQueues.ContainsKey(outlineImg.material))
                    {
                        originalRenderQueues[outlineImg.material] = outlineImg.material.renderQueue;
                    }
                    if(grandChild != agp.shadowTransform)
                        outlineImg.material.renderQueue = outlineQueue;
                    // else
                    //     outlineImg.material.renderQueue = outlineQueue - 1;
                    // Debug.Log($"[RenderQueue] {grandChild.name} のアウトラインを {outlineQueue} に設定");
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

    // PieceDragController クラス内に追加
    /// <summary>
    /// ピースを最後にスナップされた盤面上の位置に戻す
    /// </summary>
    public void ReturnToLastSnappedPosition()
    {
        // 戻る位置が設定されていない場合（初回など）はリストに戻すなど代替処理を検討するが、
        // ここでは単純に設定されていると仮定する。

        // バイブレーション (NotifyReturnedから流用)
        VibratorManager.Vibrate(70, 40);

        // 既存のアニメーションを停止
        _moveTween?.Kill();
        
        // ターゲット位置
        Vector3 targetPos = FixZ(lastSnappedPos);

        // 1. シェイクアニメーション
        // 2. ターゲット位置への移動アニメーション

        var returnedRt = GetComponent<RectTransform>();
        if (returnedRt != null)
        {
            DOTween.Kill(returnedRt, complete: false); 

            // ★ シェイク付きアニメーション (GridPieceListControllerの Shake Settings を利用)
            Sequence seq = DOTween.Sequence();
            seq.Append(returnedRt.DOShakePosition(_listCtrl.shakeDuration, new Vector3(_listCtrl.shakeStrength, 0, 0), _listCtrl.shakeVibrato, 90, false, true));
            seq.Append(returnedRt.DOMove(targetPos, _listCtrl.shiftTime).SetEase(Ease.OutQuad));
            // seq.Join(ReturnToList()); 
            seq.Join(returnedRt.DOScale(Vector3.one, 0.15f).SetEase(Ease.OutBack)); 
        
            // ★ 修正箇所: アニメーション完了後にセルを再占有
            seq.OnComplete(() =>
            {
                // lastOccupiedMapの情報を使ってセルを再占有する
                RestoreOccupiedCellsFromMap(); // 新しく作成するヘルパーメソッド
                Debug.Log($"[ReturnToLastSnappedPosition] ピースが {lastSnappedPos} に戻り、セルを再占有しました。");
            });
        }
    }

    // PieceDragController クラスに追加: lastOccupiedMap を使ってセルを復元するメソッド
    /// <summary>
    /// lastOccupiedMap に基づいてセルを占有状態に戻す
    /// </summary>
    private void RestoreOccupiedCellsFromMap()
    {
        foreach (var pair in lastOccupiedMap)
        {
            Transform child = pair.Key;
            GridCell cell = pair.Value;

            if (child != null && cell != null)
            {
                // このセルをこのピースの子で占有する
                cell.isOccupied = true;
                cell.occupiedByChild = child;
            }
        }
        // lastMarkedCells の復元が必要な場合はここで行う（ただし RestoreOccupiedCells() がその役割を持つべき）
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