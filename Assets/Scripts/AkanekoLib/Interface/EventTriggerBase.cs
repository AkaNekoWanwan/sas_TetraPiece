using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 擬似多重クラス継承できるインターフェース用のクラス : 初期化
/// </summary>
[System.Serializable]
public class EventTriggerBase : IEventTrigger
{
    //　必須枠　------------------------------
    // ないといけないが実際はアクセスしない.
    // 一応自分自身を返すようにするがアクセスしたら例外を出してもいいと思う.
    public EventTriggerBase eventTriggerBase
    {
        get { return this; }
    }
    private MonoBehaviour MB { get; set; }
    public IEventTrigger IEventTrigger { get; set; }

    public void Init(MonoBehaviour mb, IEventTrigger iEventTrigger)
    {
        this.MB = mb;
        this.IEventTrigger = iEventTrigger;
    }


    //　自由枠　------------------------------
    public UnityEvent OnEventTrigger = null;
    // メソッド
    public void AddOnEventTrigger( UnityAction callBack)
    {
        if(OnEventTrigger == null)
            OnEventTrigger = new UnityEvent();
        OnEventTrigger.AddListener(callBack);
    }

}
