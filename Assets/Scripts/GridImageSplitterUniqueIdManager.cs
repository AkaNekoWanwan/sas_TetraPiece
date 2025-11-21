using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class GridImageSplitterUniqueIdManager : MonoBehaviour
{
#if UNITY_EDITOR
    public List<AbstractGridImageSplitter> _splitters = new List<AbstractGridImageSplitter>();
    
    // エディター上でGridImageSplitterのuniqueIdを管理するためのクラス
    // GridImageSplitterのSplitImage()から呼び出されて、uniqueIdを割り当てる
    public void AssignUniqueIds(AbstractGridImageSplitter splitter )
    {
        if(!_splitters.Contains(splitter) || IsUniqueIdUsed(splitter))
        {
            if(!_splitters.Contains(splitter))
            {
                splitter.uniqueId = 99999999; // 一時的にユニークIDを外す
                _splitters.Add(splitter);
            }
            splitter.uniqueId = GetNewUniqueId();
            Debug.Log("Added Splitter. UniqueId: " + splitter.uniqueId);
        }
    }
    
    // すでに同じuniqueIdが使われているかチェック
    private bool IsUniqueIdUsed(AbstractGridImageSplitter targetSplitter)
    {
        foreach(var splitter in _splitters)
        {
            if( targetSplitter != splitter && splitter.uniqueId == targetSplitter.uniqueId)
            {
                return true;
            }
        }
        return false;
    }

    // 無くなったSplitterをリストから削除する
    private void UpdateSplitterList()
    {
        for(int i = _splitters.Count -1; i >= 0; i--)
        {
            if(_splitters[i] == null)
            {
                _splitters.RemoveAt(i);
            }
        }
    }

    // Splitterが削除されることも考慮して、今ない最小のuniqueIdを取得する
    private int GetNewUniqueId()
    {
        UpdateSplitterList();
        // uniqueIdの昇順にソート
        _splitters = _splitters.OrderBy(s => s.uniqueId).ToList();

        int newId = 1;
        for(int i = 0; i < _splitters.Count; i++)
        {
            AbstractGridImageSplitter splitter = _splitters[i];
            if(splitter.uniqueId <= newId)
            {
                newId++;
            }
            else
            {
                break;
            }
        }
        return newId;
    }
#endif
}
