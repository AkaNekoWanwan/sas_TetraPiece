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

    void Awake()
    {
        shakeStrength = 2f;
        shakeVibrato = 25;
        shakeDuration = 0.2f;
        var pcs = GetComponentsInChildren<PieceDragController>(false);
        queue.AddRange(pcs.OrderBy(p => p.transform.position.x));

        AlignAll(withDelay: false);
        UpdateSelectability();
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

   void AlignAll(bool withDelay)
{
    if(isCreative)
    {
        return;
    }

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

        // ★ 判定: いま hiddenX 側にいる or 画面外にいたピース
        bool wasHidden = rt.position.x > baseX + spacing * (selectableCount - 1) + 0.1f
                         || rt.position.x >= hiddenX - 10f;

        if (withDelay)
        {
            float delay = 0.1f * i;

            // ★ 今回画面内に入ってくるピース（wasHidden → !isHidden）なら一瞬ワープ→DO
            if (!isHidden && wasHidden)
            {
                rt.position = new Vector3(hiddenX*0.5f, baseY, 0); // 画面外に一瞬ワープ
                rt.DOMove(target, shiftTime*1.5f)
                    .SetDelay(delay)
                    .SetEase(Ease.OutQuad);
            }
            else if ((rt.position - target).sqrMagnitude > 0.001f)
            {
                // 通常移動
                rt.DOMove(target, shiftTime)
                    .SetDelay(delay)
                    .SetEase(Ease.OutQuad);
            }
        }
        else
        {
            rt.position = target;
        }
    }
}


    /// <summary>
    /// ピースがステージに置かれたとき呼ぶ
    /// </summary>
    public void NotifySnapped(PieceDragController snapped)
    {
        queue.Remove(snapped);
        AlignAll(withDelay: true);
        UpdateSelectability();
    }

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

        // X位置順に並べ直して整列
        var ordered = queue.OrderBy(p => p.transform.position.x).ToList();
        queue.Clear();
        queue.AddRange(ordered);

        // ★ まず全体を AlignAll（4つ目以降を画面外に）
        AlignAll(withDelay: true);

        // ★ 戻ってきたピースだけ Shake
        var rt = piece.GetComponent<RectTransform>();
        if (rt != null)
        {
            DOTween.Kill(rt);

            int idx = queue.IndexOf(piece);
            bool isHidden = idx >= selectableCount;

            if (isHidden)
            {
                float tx = hiddenX;
                float ty = baseY;
                rt.DOMove(new Vector3(tx, ty, 0), shiftTime).SetEase(Ease.OutQuad);
            }
            else
            {
                Sequence seq = DOTween.Sequence();
                seq.Append(rt.DOShakePosition(shakeDuration, new Vector3(shakeStrength, 0, 0), shakeVibrato, 90, false, true));
                seq.Append(rt.DOMove(new Vector3(baseX + spacing * idx, baseY, 0), shiftTime).SetEase(Ease.OutQuad));
                seq.Join(piece.ReturnToList());
            }
        }

        UpdateSelectability();
    }



    public void RescanAndAlign()
    {
        queue.Clear();
        var pcs = GetComponentsInChildren<PieceDragController>(false)
                  .OrderBy(p => p.transform.position.x);
        queue.AddRange(pcs);

        AlignAll(withDelay: true);
        UpdateSelectability();
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