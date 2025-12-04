using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using System.IO;
using System.Collections;
using System.Collections.Generic;

// 隣接するセルの有無情報
public struct ContainsCellInfo
{
    public bool containsL;
    public bool containsR;
    public bool containsY;
}

// 隣接するセルの有無に応じて三角形セルのセルコピー(アウトライン)の位置やスケールの調整をするクラス
public class TriangleCellCopyHandler : MonoBehaviour
{
    public Transform CellCopy;
    public bool IsUpSide; // trueで辺が上を向く(頂点が下を向く)
    public Vector2Int CellPos = default;
    public float Scale = 1f;

    // 他のセルたちと比較
    public ContainsCellInfo ContainsCell(List<TriangleCellCopyHandler> others, ContainsCellInfo info)
    {
        for(int i = 0; i < others.Count; i++)
        {
            info = ContainsCell(others[i], info);
        }
        return info;
    }
    // 他のセルと比較
    public ContainsCellInfo ContainsCell(TriangleCellCopyHandler other, ContainsCellInfo info)
    {
        if(other == this)
            return info;
        if(CellPos.y == other.CellPos.y)
        {
            // 右にある
            if(CellPos.x - other.CellPos.x == 1 )
                info.containsL = true;
            // 左にある
            if(CellPos.x - other.CellPos.x == -1 )
                info.containsR = true;
        }
        if(CellPos.x == other.CellPos.x)
        {
            // 上(下)にあるか。このセルが上向きなら下にあるか、このセルが下向きなら上にあるかの判定
            if(!IsUpSide && CellPos.y - other.CellPos.y == 1)
                info.containsY = true;
            if(IsUpSide && CellPos.y - other.CellPos.y == -1)
                info.containsY = true;
        }
        return info;
    }
    
    public IEnumerator UpdateAllCellCopyTransformCoroutine(List<TriangleCellCopyHandler> Handlers)
    {
        for(int i = 0; i < Handlers.Count; i++)
        {
            Handlers[i].UpdateCellCopyTransform(Handlers);
            yield return null;
        }
    }
    public void UpdateAllCellCopyTransform(List<TriangleCellCopyHandler> Handlers)
    {
        for(int i = 0; i < Handlers.Count; i++)
        {
            Handlers[i].UpdateCellCopyTransform(Handlers);
        }
    }

    // 隣接するセルに応じてセルコピー(アウトライン)の位置やスケールの調整
    public void UpdateCellCopyTransform(List<TriangleCellCopyHandler> others)
    {
        ContainsCellInfo info;
        info.containsL = false;
        info.containsR = false;
        info.containsY = false;

        info = ContainsCell(others, info);
        UpdateCellCopyTransform(info);
    }
    // 隣接するセルに応じてセルコピー(アウトライン)の位置やスケールの調整
    public void UpdateCellCopyTransform(ContainsCellInfo containsInfo)
    {
        UpdateCellCopyTransform(containsInfo.containsL, containsInfo.containsR, containsInfo.containsY);
    }
    public void UpdateCellCopyTransform(bool containsL, bool containsR, bool containsY)
    {
        RectTransform rect = GetComponent<RectTransform>();
        RectTransform outlineRect = CellCopy.GetComponent<RectTransform>();

        float posX = 0f;
        float posY = 0f;
        float posZ = 1f;
        float addSize = 23f;

        Vector2 sizeDelta = rect.sizeDelta;
        Vector2 outlineSizeDelta = outlineRect.sizeDelta;

        // Debug.Log($"ワン：{this.gameObject.name}, {containsL}, {containsR}, {containsY}");

        // 周辺のセル数に応じてアウトラインのサイズ設定
        int containsNum = 0;
        if(containsL) containsNum++;
        if(containsR) containsNum++;
        if(containsY) containsNum++;

        // if(containsNum == 0)
        //     addSize = 24f;
            // Scale = 1.3f;
        if(containsNum == 1)
            addSize /= 1.7320508f;
            // Scale = 1.07f;
        if(containsNum == 2)
            addSize /= 3f;
            // Scale = 1.04f;
        // 周囲に他のセルがある -> アウトライン非表示
        if(containsNum == 3)
        {
            addSize = 0f;
            Scale = 0f;
        }

        // addSize *= Scale;
        outlineSizeDelta.x = sizeDelta.x + addSize;
        outlineSizeDelta.y = sizeDelta.y + addSize;
        outlineRect.sizeDelta = outlineSizeDelta;
        Vector2 setPivot = new Vector2();

        // float collect = sizeDelta.x / outlineSizeDelta.x / Scale;
        float collect = 1;
        if( Scale <= 0f)
            collect = 0f;

        // 周囲に何もない -> 全周にアウトライン表示
        if( !containsL && !containsR && !containsY )
        {
            setPivot = Vector2.one * 0.5f;
            outlineRect.pivot = setPivot;
            outlineRect.anchorMax = setPivot;
            outlineRect.anchorMin = setPivot;
            posY = -addSize / 6f;
            if(!IsUpSide)
                posY = -posY;
            // Debug.Log($"setY:1:{outlineSizeDelta.x}, {sizeDelta.x}, {Scale}, {collect}, {posY}");
        }
        // 左だけ他のセルがある
        if(containsL && !containsR && !containsY)
        {
            // 頂点が下向きなら基準点を右下に。
            setPivot = new Vector2( 0f, 0f );
            posX = -addSize / 4f;
            posY = -addSize / 2f;
            if(!IsUpSide)
            {
                setPivot.y = 1f; 
                posY *= -1f;
            }
        }
        // 右だけ他のセルがある
        if(!containsL && containsR && !containsY)
        {
            // 頂点が下向きなら基準点を左下に。
            setPivot = new Vector2( 1f, 0f );
            posX = addSize / 4f;
            posY = -addSize / 2f;
            if(!IsUpSide)
            {
                setPivot.y = 1f; 
                posY *= -1f;
            }
        }
        // 下(上)だけ他のセルがある
        if(!containsL && !containsR && containsY)
        {
            setPivot = new Vector2( 0.5f, 1f );
            if(!IsUpSide)
                setPivot.y = 0f; 
        }
        // 左だけ他のセルがない
        if(!containsL && containsR && containsY)
        {
            // 頂点が下向きなら基準点を右上に。
            setPivot = new Vector2( 1f, 1f );
            if(!IsUpSide)
                setPivot.y = 0f; 
        }
        // 右だけ他のセルがない
        if(containsL && !containsR && containsY)
        {
            // 頂点が下向きなら基準点を左上に。
            setPivot = new Vector2( 0f, 1f );
            if(!IsUpSide)
                setPivot.y = 0f; 
        }
        // 下(上)だけ他のセルがない
        if(containsL && containsR && !containsY)
        {
            setPivot = new Vector2( 0.5f, 0f );
            if(!IsUpSide)
                setPivot.y = 1f; 
        }
        outlineRect.pivot = setPivot;
        outlineRect.anchorMax = setPivot;
        outlineRect.anchorMin = setPivot;
        // CellCopy.localScale = Vector3.one * Scale;
        CellCopy.localScale = Vector3.one;
        CellCopy.localPosition = new Vector3(posX, posY, posZ);
        outlineRect.anchoredPosition = new Vector3(posX, posY, posZ);
    }
}
