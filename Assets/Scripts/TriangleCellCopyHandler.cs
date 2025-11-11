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
    public float Scale = 1.1f;

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
        float posX = 0f;
        float posY = 0f;
        float posZ = 10f;

        // Debug.Log($"ワン：{this.gameObject.name}, {containsL}, {containsR}, {containsY}");

        // 周辺のセル数に応じてアウトラインのサイズ設定
        int containsNum = 0;
        if(containsL) containsNum++;
        if(containsR) containsNum++;
        if(containsY) containsNum++;

        if(containsNum == 0)
            Scale = 1.1f;
        if(containsNum == 1)
            Scale = 1.07f;
        if(containsNum == 2)
            Scale = 1.04f;
        // 周囲に他のセルがある -> アウトライン非表示
        if(containsNum == 3)
            Scale = 0f;

        // 周囲に何もない -> 全周にアウトライン表示
        if( !containsL && !containsR && !containsY )
        {
            posY = -4f;
        }
        // 左だけ他のセルがある
        if(containsL && !containsR && !containsY)
        {
            posX = 5f;
            posY = -2f;
        }
        // 右だけ他のセルがある
        if(!containsL && containsR && !containsY)
        {
            posX = -5f;
            posY = -2f;
        }
        // 下(上)だけ他のセルがある
        if(!containsL && !containsR && containsY)
        {
            posY = -10f;
        }
        // 左だけ他のセルがない
        if(!containsL && containsR && containsY)
        {
            posX = -6f;
            posY = -4f;
        }
        // 右だけ他のセルがない
        if(containsL && !containsR && containsY)
        {
            posX = 6f;
            posY = -4f;
        }
        // 下(上)だけ他のセルがない
        if(containsL && containsR && !containsY)
        {
            posY = 6f;
        }

        if(!IsUpSide)
            posY *= -1f;
        CellCopy.localScale = Vector3.one * Scale;
        CellCopy.localPosition = new Vector3(posX, posY, posZ);
    }
}
