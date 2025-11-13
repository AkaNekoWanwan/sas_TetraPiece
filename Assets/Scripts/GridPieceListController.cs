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
    public string PieceCreateSeed = ""; // ピース作成のシード値
    public string backUpPieceCreateSeed = ""; // ピース作成のシード値のバックアップ
    public List<string> avoidPatternSeeds = default;

    public ShapeType ShapeType = default;
    public bool IsSetShapeType = false;

    public Transform gridParent = null;
    
    [Header("Hidden Pieces")]
    [Tooltip("4つ目以降を配置する画面外のX座標")]
    public float hiddenX = 1000f;

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

    // パーツリストのスケール
    [Tooltip("ピースリストのサイズ")]
    public float _PieceDragControllersScale = -1f;


    private readonly List<PieceDragController> queue = new();
    private Sequence _alignSequence = null; // ★ 追加：進行中の整列アニメーションを管理

    void Awake()
    {
        shakeStrength = 2f;
        shakeVibrato = 25;
        shakeDuration = 0.2f;
        var pcs = GetComponentsInChildren<PieceDragController>(false);
        queue.AddRange(pcs.OrderBy(p => p.transform.position.x));

        // AlignAll(withDelay: false);
        AlignAll(withDelay: false, onComplete: () => {
            UpdateSelectability();
        });
        // UpdateSelectability();

        if(!IsSetShapeType)
        {
            // 同じ階層のGridPieceListControllerを取得
            AbstractGridImageSplitter gridImageSplitter = this.transform.parent.gameObject.GetComponentInChildren<AbstractGridImageSplitter>();
            ShapeType = gridImageSplitter.GetShapeType();
            IsSetShapeType = true;   
        }
    }

    void UpdateSelectability()
    {
        if(isCreative)
            return;
        for (int i = 0; i < queue.Count; i++)
        {
            bool can = i < selectableCount;
            queue[i].enabled = can;
        }
    }

   void AlignAll(bool withDelay, System.Action onComplete = null) // ★ onComplete パラメータを追加
    {
        if(isCreative)
        {
            onComplete?.Invoke(); // クリエイティブモードでは即時完了
            return;
        }

        // 既存のシーケンスがあれば、念のため終了させる
        _alignSequence?.Kill(complete: false);
        _alignSequence = DOTween.Sequence(); // 新しいシーケンスを作成
        
        // ★ ピース移動処理をシーケンスに追加
        for (int i = 0; i < queue.Count; i++)
        {
            var rt = queue[i].GetComponent<RectTransform>();
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

            if (withDelay)
            {
                // ★ 変更1: 連続操作時の不整合を防ぐため、ピースごとの遅延 (0.1f * i) を廃止または調整
                // ピースが抜けた直後の整列では、全てのピースが同時に動き始める方が安全かつ自然
                float delay = 0f; // ピースごとの遅延を削除
                float duration = shiftTime;
                
                // ★ 変更2: 既存のアニメーションを強制的に完了/停止させる (位置を確定させる)
                // 完全にKillすることで、以前のトゥイーンの影響を排除します
                DOTween.Kill(rt, complete: true); 

                if (!isHidden && wasHidden)
                {
                    // 今回画面内に入ってくるピース（ワープ→DO）
                    rt.position = new Vector3(hiddenX*0.5f, baseY, 0);
                    duration = shiftTime * 1.5f;

                    // MoveアニメーションをシーケンスにJoin
                    _alignSequence.Join(rt.DOMove(target, duration)
                        .SetDelay(delay) // delayは 0f のまま
                        .SetEase(Ease.OutQuad));
                }
                else if ((rt.position - target).sqrMagnitude > 0.001f)
                {
                    // 通常移動
                    _alignSequence.Join(rt.DOMove(target, duration)
                        .SetDelay(delay) // delayは 0f のまま
                        .SetEase(Ease.OutQuad));
                }
            }
            else
            {
                // withDelay=false の場合は即時位置設定
                rt.position = target;
            }
        }
        
        // ★ シーケンスが完了したら、外部から渡されたコールバックを実行
        // ... (OnComplete のロジックはそのまま) ...
        if (withDelay)
        {
            _alignSequence.OnComplete(() =>
            {
                onComplete?.Invoke();
                _alignSequence = null;
            });
        }
        else
        {
            onComplete?.Invoke();
        }
    }


    /// <summary>
    /// ピースがステージに置かれたとき呼ぶ
    /// </summary>
    public void NotifySnapped(PieceDragController snapped)
    {
        // ピースをキューから削除
        queue.Remove(snapped);
        
        // ★ AlignAllを実行し、そのアニメーションが完了した後に選択性を更新する
        AlignAll(withDelay: true, onComplete: () => {
            UpdateSelectability();
        });
        
        // 旧: UpdateSelectability(); // アニメーションと同時に実行されていた
    }

    // GridPieceListController.cs

    /// <summary>
    /// ピースが戻ったとき呼ぶ（シェイク付き）
    /// </summary>
    public void NotifyReturned(PieceDragController piece)
    {
        // ★ 念のため、戻ってきたピースの占有を解除
        piece.ReleaseOccupiedCells();
        
        if (!queue.Contains(piece))
        {
            queue.Add(piece);
        }

        // --- ★ 修正箇所: ソートロジックの再調整 ★ ---
        
        // 1. 戻ってきたピース（D）を除く、リスト内の他のピース（A, B, C）を取得し、X座標でソート
        //    (この時点でのソートは、A, B, C がリストから抜けたピース D の穴を埋めるために
        //     左にシフトした状態で並んでいることを確認するため)
        // var otherPieces = queue.Where(p => p != piece).OrderBy(p => p.transform.position.x).ToList();
        var otherPieces = queue.Where(p => p != piece).ToList();
        
        // 2. 戻ってきたピース D の挿入位置を決定
        
        // D がリストの可視領域（selectableCount = 3）に戻るかどうかを、そのX座標で判断する。
        // 可視領域の最も右端のデフォルト位置（インデックス2）のX座標
        float visibleRightX = baseX + spacing * (selectableCount - 1);
        
        // 戻ってきたピースの現在のX位置
        float currentPieceX = piece.transform.position.x;
        
        // ★ リストの再構築
        queue.Clear();
        
        if (otherPieces.Count < selectableCount)
        {
            // (A, B) のようにリストに空きがある場合:
            // 戻ってきたピース (D) は、他のピースの X 座標の間に挿入されようとするが、
            // PieceSorter の順序を維持するため、現在の X 座標に最も近いインデックスに挿入する。

            // 戻ってきたピースの X 座標に最も近い他のピースのインデックスを見つける
            int nearestIndex = -1;
            float minDistance = float.MaxValue;
            
            for (int i = 0; i < otherPieces.Count; i++)
            {
                float dist = Mathf.Abs(otherPieces[i].transform.position.x - piece.transform.position.x);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    nearestIndex = i;
                }
            }
            
            // 挿入位置を決定（最も近いピースの右側または左側）
            int insertIndex = (nearestIndex == -1 || piece.transform.position.x > otherPieces[nearestIndex].transform.position.x)
                            ? otherPieces.Count
                            : nearestIndex;

            // 挿入して queue を再構築
            otherPieces.Insert(insertIndex, piece);
            queue.AddRange(otherPieces);
        }
        else
        {
            // (A, B, C) のようにリストが埋まっている場合 (前回の修正ロジックを維持)
            // ... (前回の修正コードの通り、画面外へ押し出すロジックをそのまま使用) ...
            
            if (currentPieceX > visibleRightX + 0.1f)
            {
                queue.AddRange(otherPieces.Take(selectableCount - 1));
                queue.Add(piece);
                queue.Add(otherPieces[selectableCount - 1]);
            }
            else if (currentPieceX < baseX - 0.1f)
            {
                queue.Add(piece);
                queue.AddRange(otherPieces);
            }
            else
            {
                // X 座標の順序を尊重しつつ、最も右のピース C を画面外へ押し出す
                var visiblePieces = otherPieces.Take(selectableCount - 1).ToList();
                var tempQueue = new List<PieceDragController>(visiblePieces);
                tempQueue.Add(piece);
                // ★ X座標順ソートを維持: PieceSorterの順序を維持するロジックに変更する必要があるが、
                //    このケースではピースを押し出すことが目的なので、現状のX座標ソートを維持する。
                //    → ここが PieceSorter の意図を破壊する最後の場所となるが、
                //       ユーザー体験上の「戻したピースが適切な位置に入る」ことを優先します。
                queue.AddRange(tempQueue.OrderBy(p => p.transform.position.x));
                queue.Add(otherPieces[selectableCount - 1]);
            }
        }
        
        // --- 修正箇所ここまで ---


        // ★ 戻ってきたピースのインデックスを取得
        int returnedIdx = queue.IndexOf(piece);

        // ★ 戻ってきたピースのターゲット位置
        float targetX = baseX + spacing * returnedIdx;
        float targetY = baseY;
        Vector3 targetPos = new Vector3(targetX, targetY, 0);

        // ★ 画面内（3番目まで）に戻るかどうかの判定
        bool isReturningToVisibleSlot = returnedIdx < selectableCount; // Dはインデックス2なので true


        // 1. 全ピースの移動処理（戻ってきたピースを除く）
        for (int i = 0; i < queue.Count; i++)
        {
            // 戻ってきたピースは次のステップで個別に処理するためスキップ
            if (i == returnedIdx) continue; 

            var pc = queue[i]; // C, B, A のいずれか
            var rt = pc.GetComponent<RectTransform>();
            if (rt == null) continue;

            float tx, ty;
            bool isHidden = i >= selectableCount; // C は i=3 なので isHidden=true

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

            // ピース D の移動によって C が画面外に移動したり、A, B が左に詰めるアニメーションを実行
            bool wasHidden = rt.position.x > baseX + spacing * (selectableCount - 1) + 0.1f
                             || rt.position.x >= hiddenX - 10f;


            // DOTweenアニメーションを強制停止してから再開
            DOTween.Kill(rt, complete: false);

            if (!isHidden && wasHidden)
            {
                // 画面内に入ってくるピースは、一瞬ワープしてからDO
                rt.position = new Vector3(hiddenX * 0.5f, baseY, 0); 
                rt.DOMove(target, shiftTime * 1.5f)
                    .SetDelay(0.1f * i) 
                    .SetEase(Ease.OutQuad);
            }
            else if ((rt.position - target).sqrMagnitude > 0.001f)
            {
                // 通常移動（C が画面外へ、A, B はそのまま、または左にシフトする場合など）
                rt.DOMove(target, shiftTime)
                    .SetDelay(0.1f * i) 
                    .SetEase(Ease.OutQuad);
            }
        }


        // 2. 戻ってきたピースの特殊処理（Shakeと移動）
        var returnedRt = piece.GetComponent<RectTransform>();
        if (returnedRt != null)
        {
            DOTween.Kill(returnedRt, complete: false); 

            if (isReturningToVisibleSlot)
            {
                // 画面内に戻る場合 (D が C の位置に戻る): Shake → ターゲット位置へ
                Sequence seq = DOTween.Sequence();
                seq.Append(returnedRt.DOShakePosition(shakeDuration, new Vector3(shakeStrength, 0, 0), shakeVibrato, 90, false, true));
                seq.Append(returnedRt.DOMove(targetPos, shiftTime).SetEase(Ease.OutQuad));
                seq.Join(piece.ReturnToList());
            }
            else
            {
                // 画面外に戻る場合 (このロジックでは D は画面内に戻るため、通常は実行されない)
                returnedRt.DOMove(targetPos, shiftTime).SetEase(Ease.OutQuad);
                // originalScale を直接使用
                returnedRt.DOScale(Vector3.one * _PieceDragControllersScale, 0.15f).SetEase(Ease.OutBack);
            }
        }

        UpdateSelectability();
    }



    public void RescanAndAlign()
    {
        queue.Clear();
        
        // ★ 変更: FindObjectsOfType<PieceDragController>() の結果を PieceSorter でソート
        var unsortedPieces = FindObjectsOfType<PieceDragController>().ToList();
        
        // ピースを PieceSorter で並び替える
        // この並び替えによって、queue の順番が「外周から順番」になる
        var sortedPieces = PieceSorter.SortBySeededAlternatingDirections(unsortedPieces, PieceCreateSeed); 

        // 既存のリストにあるピースのみを queue に追加 (シーン内に残っているピースのみ)
        foreach (var piece in sortedPieces)
        {
            if (piece.GetComponentInParent<GridPieceListController>() == this)
            {
                // リストコンポーネントの子であるピースのみを追加する想定
                queue.Add(piece);
            }
        }
        
        // ★ 以前の修正と同様に AlignAll でアニメーションを完了保証する
        AlignAll(withDelay: true, onComplete: () => {
            UpdateSelectability();
        });
    }

    public bool IsSelectable(PieceDragController pc)
    {
        int idx = queue.IndexOf(pc);

        return idx >= 0 && idx < selectableCount;
    }


    // ピースリストセットアップの前準備
    public void PreSetPieceDragControllers()
    {
        // ピースリスト群をリセット
        // 子オブジェクト全削除
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
            for (int j = childPiece.transform.childCount - 1; j >= 0; j--)
            {
                Transform child = childPiece.transform.GetChild(j);
                if (child != null)
                {
                    DestroyImmediate(child.gameObject, true);
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
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(GridPieceListController))]
public class GridPieceListControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
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