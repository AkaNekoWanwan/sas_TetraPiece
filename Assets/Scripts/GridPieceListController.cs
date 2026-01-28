using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DG.Tweening;
using UnityEditor;


public class GridPieceListController : MonoBehaviour
{
    [Header("Layout Settings")]
    public float baseX = -7f;
    public float spacing = 7f;
    public float baseY = -22f;
    public int pieceNum = -1;
    public float shiftTime = 0.25f;
    public bool isCreative = false;
    public bool isOverrayPieceNum = false; // pieceNumの値をオブジェクト数で上書きするか
    public string pieceSeed = "";
    public bool isOverraySeed = true;   // ピースのランダム配置のシード値を更新するか
    public bool isSkip = false;   // 全更新時にスキップするか
    public bool isOrderSort = false;   // ピースの並びを手動で指定するか
    public string backUpPieceCreateSeed = ""; // ピース作成のシード値のバックアップ
    public int randomSeed = 0;
    public List<string> avoidPatternSeeds = default;

    public ShapeType ShapeType = default;
    public bool IsSetShapeType = false;

    public Transform gridParent = null;
    
    [Header("Hidden Pieces")]
    [Tooltip("4つ目以降を配置する画面外のX座標")]
    public float hiddenX = 1000;

    [Header("Rule")]
    [Tooltip("左から何個まで選択可能か")]
    public int selectableCount = 3;

    [Header("Shake Settings")]
    [Tooltip("戻ってくる時のシェイクの強さ")]
    public float shakeStrength = 10f;
    [Tooltip("シェイクの振動数")]
    public int shakeVibrato = 10;
    [Tooltip("シェイクの時間")]
    public float shakeDuration = 0.3f;
    public Transform _boardTransform = null;
    public RectTransform _rectTransform = null;
    public List<int> _randomIndexs = default;
    // パーツリストのスケール
    [Tooltip("ピースリストのサイズ")]
    public float _PieceDragControllersScale = -1f;

    public List<PieceDragController> _queue;
    private readonly List<PieceDragController> queue = new();
    private Sequence _alignSequence = null; // ★ 追加：進行中の整列アニメーションを管理

    private bool randomRock = true; // ランダム順ピースの補充をロック
    private int _initQueueCount = 0;

#if UNITY_EDITOR
    private void OnValidate() {
        return;
        if(UnityEditor.EditorApplication.isPlaying)
            return;
        baseX = -8f;
        spacing = 8f;
        baseY = -18f;
        hiddenX = 100;
        selectableCount = 3;
        if(_rectTransform == null)
            _rectTransform = GetComponent<RectTransform>();
        UpdateSizeDelta();
    }
#endif

    private void UpdateSizeDelta()
    {
        if(_boardTransform == null)
            return;
        List<PieceDragController> pcs;
#if UNITY_EDITOR
        if(UnityEditor.EditorApplication.isPlaying)
            pcs = queue;
        else
#endif
            pcs = GetComponentsInChildren<PieceDragController>(false).ToList();

        Debug.Log($"queue:{queue}, {queue.Count}");
    }

    void Awake()
    {
        shakeStrength = 2f;
        shakeVibrato = 25;
        shakeDuration = 0.2f;
        var pcs = GetComponentsInChildren<PieceDragController>(false);
        // _targetSizeDelta = _rectTransform.sizeDelta;
        Debug.Log($"GridPieceListController:Awake:{pcs.Length}");
        // queue.AddRange(pcs.OrderBy(p => p.transform.position.x));
        if(!isOrderSort)
        {
            queue.AddRange(PieceSorter.SortBySeededAlternatingDirections(pcs.ToList(), randomSeed)); 
        }
        else
        {
            queue.AddRange(pcs.ToList());
        }
        
        _queue = queue;
        _initQueueCount = _queue.Count;

        // 初期配置（即座に実行）
        AlignAll(withDelay: false);
        UpdateSelectability();

        if(!IsSetShapeType)
        {
            // 同じ階層のGridPieceListControllerを取得
            AbstractGridImageSplitter gridImageSplitter = this.transform.parent.gameObject.GetComponentInChildren<AbstractGridImageSplitter>();
            ShapeType = gridImageSplitter.GetShapeType();
            IsSetShapeType = true;   
        }

        _randomIndexs = new List<int>();
        for(int i = 0; i < pcs.Length; i++)
        {
            PieceDragController ps = pcs[i];
            if(i == 0)
            {
                RectTransform rect = ps.transform.GetChild(0).gameObject.GetComponent<RectTransform>();
                SetCellScale(rect.sizeDelta.x, ShapeType);
                // Debug.Log($"aaaaaa:{this.gameObject.name},{ps.gameObject.name}, {rect.gameObject.name}, {rect.sizeDelta.x}, {_PieceDragControllersScale}, {ShapeType}");
            }
            ps.transform.localScale = Vector3.one * _PieceDragControllersScale;
            ps.OriginalScale = Vector3.one * _PieceDragControllersScale;

            // ランダムピース補充用のインデックス
            if(i <= pcs.Length / 3)
            {
                int index = ((randomSeed + i) * (i + 1)) % 3 + i * 3 + 1;
                // Debug.Log($"ランダムインデックスセット:{i}, {index}");
                _randomIndexs.Add(index);
            }
        }
    }

    private void Update()
    {
        // if(_targetSizeDelta != _rectTransform.sizeDelta)
        // {
        //     bool withDelay = false;
        //     Vector2 setSize = _rectTransform.sizeDelta;
        //     if(Mathf.Abs(_rectTransform.sizeDelta.x - _targetSizeDelta.x) <= 1.2f)
        //     {
        //         setSize.x = _targetSizeDelta.x;
        //         withDelay = true;
        //     }
        //     if(_rectTransform.sizeDelta.x <= _targetSizeDelta.x)
        //     {
        //         setSize.x += 0.5f;
        //     }
        //     else
        //     {
        //         setSize.x -= 0.5f;
        //     }
        //     if(Mathf.Abs(_rectTransform.sizeDelta.x - _targetSizeDelta.x) <= 1.2f)
        //     {
        //         setSize.x = _targetSizeDelta.x;
        //         withDelay = true;
        //     }
        //     _rectTransform.sizeDelta = setSize;
            // AlignAll(withDelay, onComplete: () => {
            //     UpdateSelectability();
            // });
        // }
    }

    public void SetCellScale(float size, ShapeType shapeType)
    {
        _PieceDragControllersScale = 0.67f * 185f / size;
        if(shapeType == ShapeType.Square)
            _PieceDragControllersScale *= 0.75f;
        else
            _PieceDragControllersScale *= 1f;
    }

    // 選択可能ピースの更新。isAddRandomがtrueならランダム順のピースを補充する時がある
    void UpdateSelectability()
    {
        if(isCreative)
            return;
        for (int i = 0; i < queue.Count; i++)
        {
            bool can = i < selectableCount;
            queue[i].enabled = can;
        }
        _queue = queue;
    }

    // ピースが盤面に置かれた時の整列処理
    void AlignAll(bool withDelay, System.Action onComplete = null)
    {
        // Delegate to the unified animator, pass through completion callback
        AnimateQueuePositions(null, false, withDelay, onComplete);
    }

    // 共通: queue の位置をアニメーション/設定するヘルパー
    // focusedIndex: 任意で注目ピースのインデックス（シェイク等の特殊処理を行う）
    // shakeFocused: 注目ピースを戻す際にシェイクするか
    // withDelay: false の場合は即時位置設定
    private Sequence AnimateQueuePositions(int? focusedIndex = null, bool shakeFocused = false, bool withDelay = true, System.Action onAllCompleted = null)
    {
        if (isCreative)
            return null;

        // 既存アニメーションを確定
        _alignSequence?.Kill(complete: true);
        _alignSequence = null;

        Sequence masterSeq = DOTween.Sequence();
        masterSeq.SetLink(this.gameObject);

        if (!withDelay)
        {
            for (int i = 0; i < queue.Count; i++)
            {
                var rt0 = queue[i].GetComponent<RectTransform>();
                if (rt0 == null) continue;
                float tx0 = i >= selectableCount ? hiddenX : baseX + spacing * i;
                rt0.position = new Vector3(tx0, baseY, 0);
            }
            onAllCompleted?.Invoke();
            return masterSeq;
        }

        for (int i = 0; i < queue.Count; i++)
        {
            var pc = queue[i];
            var rt = pc.GetComponent<RectTransform>();
            if (rt == null) continue;

            float tx, ty;
            bool isHidden = i >= selectableCount;

            if (isHidden)
            {
                tx = hiddenX;
                ty = baseY;
            }
            else
            {
                tx = baseX + spacing * i;
                ty = baseY;
            }

            Vector3 target = new Vector3(tx, ty, 0);

            bool wasHidden = rt.position.x > baseX + spacing * (selectableCount - 1) + 0.1f
                             || rt.position.x >= hiddenX - 10f;

            // 既存のアニメーションを強制完了して位置を確定
            DOTween.Kill(rt, complete: true);

            // 注目ピースの特殊処理
            if (focusedIndex.HasValue && i == focusedIndex.Value)
            {
                bool isReturningToVisibleSlot = i < selectableCount;
                if (isReturningToVisibleSlot && shakeFocused)
                {
                    Sequence seq = DOTween.Sequence();
                    seq.SetLink(this.gameObject);
                    seq.Append(rt.DOShakePosition(shakeDuration, new Vector3(shakeStrength, 0, 0), shakeVibrato, 90, false, true));
                    seq.Append(rt.DOMove(target, shiftTime).SetEase(Ease.OutQuad));
                    seq.Join(pc.ReturnToList());
                    masterSeq.Join(seq);
                }
                else
                {
                    Tween t = rt.DOMove(target, shiftTime).SetEase(Ease.OutQuad).SetLink(rt.gameObject);
                    rt.DOScale(Vector3.one * _PieceDragControllersScale, 0.15f).SetEase(Ease.OutBack).SetLink(rt.gameObject);
                    masterSeq.Join(t);
                }
            }
            else
            {
                // 通常移動
                if (!isHidden && wasHidden)
                {
                    rt.position = new Vector3(hiddenX * 0.5f, baseY, 0);
                    Tween t = rt.DOMove(target, shiftTime * 1.5f).SetEase(Ease.OutQuad).SetLink(rt.gameObject);
                    masterSeq.Join(t);
                }
                else if ((rt.position - target).sqrMagnitude > 0.001f)
                {
                    Tween t = rt.DOMove(target, shiftTime).SetEase(Ease.OutQuad).SetLink(rt.gameObject);
                    masterSeq.Join(t);
                }
            }
        }

        // マスターシーケンスの完了で最終整合チェックを実行
        masterSeq.OnComplete(() =>
        {
            // 最終位置を厳密に補正して内部状態と見た目を一致させる
            for (int j = 0; j < queue.Count; j++)
            {
                var r0 = queue[j].GetComponent<RectTransform>();
                if (r0 == null) continue;
                float tx0 = j >= selectableCount ? hiddenX : baseX + spacing * j;
                Vector3 final = new Vector3(tx0, baseY, 0);
                if ((r0.position - final).sqrMagnitude > 0.01f)
                {
                    r0.position = final;
                }
            }
            onAllCompleted?.Invoke();
        });

        // 保持
        _alignSequence = masterSeq;
        return masterSeq;
    }


    /// <summary>
    /// ピースがステージに置かれたとき呼ぶ
    /// </summary>
    public void NotifySnapped(PieceDragController snapped)
    {
        // ピースをキューから削除
        queue.Remove(snapped);
        _queue = queue;

        if(_boardTransform != null)
        {
            snapped.transform.parent = _boardTransform;
            UpdateSizeDelta();
        }
        
        bool isAddRandom = true;
        // ランダムピース挿入をするほど残りのピースが多く残っているか
        if( queue.Count < selectableCount + 1)
            isAddRandom = false;
        // 選択肢にランダムピースがあるかの確認。ないならランダム補充アリ
        if(isAddRandom)
        {
            for (int i = 0; i < queue.Count; i++)
            {
                if( selectableCount <= i )
                    break;
                if(queue[i].IsRandomPiece)
                {
                    isAddRandom = false;
                    break;
                }
            }
        }
        // 盤面に置いたピース数が指定ならランダム補充する
        if(isAddRandom)
        {
            int putPieceNum = _initQueueCount - queue.Count;
            // Debug.Log($"ランダムインデックスチェック:{putPieceNum}"); 
            if(!_randomIndexs.Contains(putPieceNum))
                isAddRandom = false;   
        }
        // ランダム補充実行
        if(isAddRandom)
        {
            // Debug.Log("ランダム挿入！！");
            int insertTargetIndex = queue.Count - 1;
            int insertIndex = 2;
            PieceDragController piece = queue[insertTargetIndex];
            piece.IsRandomPiece = true;
            queue.RemoveAt(insertTargetIndex); 
            queue.Insert(insertIndex, piece);
        }
        
        // ★ 状態を即座に更新（アニメーション完了を待たない）
        UpdateSelectability();
        
        // アニメーションは並行実行（他のピースの操作をブロックしない）
        AlignAll(withDelay: true);
    }

    /// <summary>
    /// ピースが戻ったとき呼ぶ（シェイク付き）
    /// </summary>
    public void NotifyReturned(PieceDragController piece, bool shouldShake = true)
    {
        // バイブレーション
        VibratorManager.Vibrate(70, 40);
        
        // ★ 戻ってきたピースの占有を解除
        piece.ReleaseOccupiedCells();
        
        // ★ queueに既にある場合は削除（重複防止）
        queue.Remove(piece);
        
        // ★ シンプルな挿入ロジック：現在のX座標に基づいて適切な位置に挿入
        float currentPieceX = piece.transform.position.x;
        int insertIndex = 0;
        
        // 他のピースのX座標と比較して挿入位置を決定
        for (int i = 0; i < queue.Count; i++)
        {
            if (currentPieceX > queue[i].transform.position.x)
            {
                insertIndex = i + 1;
            }
            else
            {
                break;
            }
        }
        
        // リストに挿入
        queue.Insert(insertIndex, piece);
        _queue = queue;
        
        // ★ 状態を即座に更新（アニメーション完了を待たない）
        UpdateSelectability();
        
        // ★ アニメーションは共通ヘルパーで実行（完了時に最終整合チェックを行う）
        AnimateQueuePositions(insertIndex, shouldShake, true, () => {
            // 最終確認: 選択性を再適用して内部状態を安定化
            UpdateSelectability();
        });
    }

    public bool IsSelectable(PieceDragController pc)
    {
        int idx = queue.IndexOf(pc);
        _queue = queue;
        return idx >= 0 && idx < selectableCount;
    }


    // ピースリストセットアップの前準備
    // shouldClearCells: trueの場合は既存セルを削除、falseの場合は既存セルを保持（再利用）
    public void PreSetPieceDragControllers(bool shouldClearCells = true)
    {
        // ピースリスト群をリセット
        // 子オブジェクト全削除（shouldClearCells=falseの場合はセルは残す）
        // スケールを1に戻す
        // ピース数の更新 ( pieceNumの値に合わせる or pieceNumの値を合わせる )
        List<PieceDragController> childPieceList = this.gameObject.GetComponentsInChildren<PieceDragController>().ToList();
        if(isOverrayPieceNum || pieceNum <= 0)
            pieceNum = childPieceList.Count;
        for(int i = childPieceList.Count - 1; i >= 0; i--)
        {
            if(!isOverrayPieceNum && pieceNum <= i)
            {
                DestroyImmediate(childPieceList[i].gameObject, true);
                continue;
            }
            PieceDragController childPiece = childPieceList[i];
            
            // shouldClearCells=trueの場合のみセルを削除
            if(shouldClearCells)
            {
                for (int j = childPiece.transform.childCount - 1; j >= 0; j--)
                {
                    Transform child = childPiece.transform.GetChild(j);
                    if (child != null)
                    {
                        DestroyImmediate(child.gameObject, true);
                    }
                }
            }

            if(_PieceDragControllersScale == -1f)
                _PieceDragControllersScale = childPiece.transform.localScale.x;
            childPiece.transform.localScale = Vector3.one;
        }
        for(int i = childPieceList.Count; i < pieceNum; i++)
        {
            GameObject answerObj = new GameObject($"piece ({i})", typeof(RectTransform), typeof(PieceDragController));
            answerObj.transform.parent = this.transform;
            PieceDragController controller = answerObj.gameObject.GetComponent<PieceDragController>();
            controller.gridParent = gridParent;
        }
    }

    // ピースリスト群のセットアップ処理を実行
    public void SetUpChildrenPieceDragController()
    {
        List<PieceDragController> childPieceList = this.gameObject.GetComponentsInChildren<PieceDragController>().ToList();
        for(int i = 0; i < childPieceList.Count; i++)
        {
            PieceDragController childPiece = childPieceList[i];
            childPiece.gridParent = gridParent;
            childPiece.RecenterParentToChildren(isCreative);
            childPiece.transform.localScale = Vector3.one * _PieceDragControllersScale;
            childPiece.isCreative = isCreative;
        }
        AbstractGridImageSplitter gridImageSplitter = this.transform.parent.gameObject.GetComponentInChildren<AbstractGridImageSplitter>();
        if(gridImageSplitter != null)
            randomSeed = gridImageSplitter.uniqueId;
        else
            randomSeed = 0;
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(GridPieceListController))]
public class GridPieceListControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        if(UnityEditor.EditorApplication.isPlaying)
            return;
        DrawDefaultInspector();
        GridPieceListController script = (GridPieceListController)target;
        GUILayout.Space(10);
        if (GUILayout.Button("PreSet"))
        {
            script.PreSetPieceDragControllers();
        }
        if (GUILayout.Button("SetUp"))
        {
            script.SetUpChildrenPieceDragController();
        }
    }
}
#endif