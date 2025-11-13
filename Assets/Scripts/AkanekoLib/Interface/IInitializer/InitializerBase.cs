using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 擬似多重クラス継承できるインターフェース用のクラス : 初期化
/// </summary>
[System.Serializable]
public class InitializerBase : IInitializer
{
    //　必須枠　------------------------------
    // ないといけないが実際はアクセスしない.
    // 一応自分自身を返すようにするがアクセスしたら例外を出してもいいと思う.
    public InitializerBase InitializerBaseClass
    {
        get { return this; }
    }
    private MonoBehaviour MB { get; set; }
    // public IInitializer Iinitializer { get; set; }
    public IInitializer Iinitializer { get; set; }
    // private GameObject GO { get { return MB.gameObject; } }
    // private CancellationToken token { get { return MB.destroyCancellationToken; } }

    public void Init(MonoBehaviour mb, IInitializer initializer)
    {
        this.MB = mb;
        this.Iinitializer = initializer;
    }


    //　自由枠　------------------------------
    private UnityEvent _onInitialize = null;
    public event UnityAction OnInitialize
    {
        add
        {
            if(!_isInitialize)
            {
                if(_onInitialize == null)
                    _onInitialize = new UnityEvent();
                _onInitialize.AddListener(value);
            }
            else
            {
                value?.Invoke();
            }
        }
        remove
        {
            _onInitialize.RemoveListener(value);
        }
    }
    private bool _isInitialize = false;
    public bool IsInitialize{ get{ return _isInitialize; } }


    public void Initialize()
    {
        Iinitializer.InitializeUnique();
        InvokeInitialize();
    }

    public void InvokeInitialize()
    {
        _isInitialize = true;
        _onInitialize?.Invoke();
        _onInitialize?.RemoveAllListeners();
    }
}
