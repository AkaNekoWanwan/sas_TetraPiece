using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DG.Tweening;

public class PicListController : MonoBehaviour
{
    [Header("Layout (legacy)")]
    public float baseX = -5.5f;
    public float spacing = 5.5f;
    public float shiftTime = 0.25f;

    [Header("Rule")]
    [Tooltip("常に左から何個まで選べるか")]
    public int selectableCount = 3;   // 常に左から3つ

    [Header("Layout (hand-cursor mode)")]
    [Tooltip("HandCursor がある時の 0,1,2 番目の固定X")]
    public Vector3 firstThreeX = new Vector3(-6.5f, 0f, 6.5f);
    [Tooltip("4個目以降の間隔（右方向）")]
    public float extraRightSpacing = 10f;

    readonly List<PicController> queue = new();
    public StageManager stageManager;
    public HandCursorController handCursorController;

    void Awake()
    {
        extraRightSpacing = 16f;
        if (!handCursorController)
        {
            handCursorController = FindAnyObjectByType<HandCursorController>();
        }

        var pcs = GetComponentsInChildren<PicController>(false);
        queue.AddRange(pcs.Where(p => !p.isSnapped)
                          .OrderBy(p => p.transform.position.x));

        foreach (var p in queue) p.listManager = this;

        // 起動時に兄弟順をX昇順(左→右)へ
        ReorderSiblingsByX();

        // 初期整列
        AlignAll(withDelay:true);

        UpdateSelectability();
    }

    public void Start()
    {
        stageManager = GameObject.Find("StageManager").GetComponent<StageManager>();
        stageManager.picCount = 0;
        stageManager.goalPicCount = queue.Count;
    }

    // 先頭n個のみ選択可能に
    void UpdateSelectability()
    {
        for (int i = 0; i < queue.Count; i++)
        {
            bool can = i < selectableCount;
            queue[i].SetSelectable(can);
        }
    }

    // === レイアウト規則：HandCursor がある時は特別配置 ===
    bool UseHandCursorLayout() => handCursorController != null;

    float GetTargetXForIndex(int i)
    {
        if (UseHandCursorLayout())
        {
            if (i == 0) return firstThreeX.x;
            if (i == 1) return firstThreeX.y;
            if (i == 2) return firstThreeX.z;

            // 4番目以降（i=3〜）：最後の固定位置(=firstThreeX.z)から右へ +10f ずつ
            return firstThreeX.z + extraRightSpacing * (i - 2);
        }
        else
        {
            return baseX + spacing * i;
        }
    }

    void AlignAll(bool withDelay)
    {
        for (int i = 0; i < queue.Count; i++)
        {
            float tx = GetTargetXForIndex(i);

            float delay = withDelay ? 0.1f * i : 0f;

            if (Mathf.Abs(queue[i].transform.position.x - tx) > 0.0001f)
            {
                queue[i].TweenToQueueX(tx, shiftTime, delay);
            }
        }
    }

    public void NotifySnapped(PicController snapped)
    {
        queue.Remove(snapped);

        // 残りを配置規則に沿って左詰め & 並び替え
        AlignAll(withDelay:true);

        ReorderSiblingsByX();
        UpdateSelectability();
    }

    public void RescanAndAlign()
    {
        queue.Clear();
        var pcs = GetComponentsInChildren<PicController>(false)
                  .Where(p => !p.isSnapped)
                  .OrderBy(p => p.transform.position.x);
        queue.AddRange(pcs);

        AlignAll(withDelay:true);

        ReorderSiblingsByX();
        UpdateSelectability();
    }

    public bool IsSelectable(PicController pc)
    {
        int idx = queue.IndexOf(pc);
        return idx >= 0 && idx < selectableCount;
    }

    // === 兄弟順を「Xが小さい→インデックス小」「Xが大きい→インデックス大」にする ===
    void ReorderSiblingsByX()
    {
        var ordered = queue.OrderBy(p => p.transform.position.x).ToList();
        for (int i = 0; i < ordered.Count; i++)
        {
            ordered[i].transform.SetSiblingIndex(i);
        }
    }
}
