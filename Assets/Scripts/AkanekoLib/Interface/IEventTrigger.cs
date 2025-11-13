using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 擬似多重クラス継承できるインターフェース : 初期化
/// </summary>
public interface IEventTrigger
{
    //　必須枠　------------------------------
    // 擬似多重継承できるクラスの本体
    public EventTriggerBase eventTriggerBase{ get; }

    //自由枠------------------------------
    // プロパティ（継承先で書き換える気がなければvirtualじゃなくてもいい）.
    // public virtual float HP
    // {
    //      get { return Hoge.HP; }
    // }

    public UnityEvent OnEventTrigger{ get { return eventTriggerBase.OnEventTrigger; } }

    // メソッド
    public void AddOnEventTrigger( UnityAction callback)
    {
        eventTriggerBase.AddOnEventTrigger(callback);
    }
}