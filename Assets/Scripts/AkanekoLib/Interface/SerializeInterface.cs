using System;
using UnityEngine;

/// <summary>
/// インターフェースをインスペクターからアタッチするためのクラス
/// インターフェースラッパークラス
/// 
/// インターフェースをアタッチしたいクラスでは以下のように書く
/// [SerializeField] private SerializeInterface<Ihodge> _iHoge;
/// インターフェース内の関数やプロパティにアクセスするときは以下のように書く。(.Value を用いる)
/// _iHoge.Value.Hoge();
/// </summary>
/// <typeparam name="T"></typeparam>
[Serializable]
public class SerializeInterface<T> where T : class
{
    [SerializeField] private MonoBehaviour reference;
    
    private T cachedValue = null;

    // 基本的に使用する変数
    public T Value
    {
        get
        {
            if (cachedValue == null)
            {
                cachedValue = reference as T;
            }
            return cachedValue;
        }
    }
}
