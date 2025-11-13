using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using UnityEngine.Events;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Experimental.Rendering;

///　シーン切り替え時の演出管理クラス。DontDestroyOnLoad化推奨
public class SceneTransition : MonoBehaviour
{
    #region Fields and Properties
    [SerializeField, Tooltip("イニシャライザー")] private SerializeInterface<IInitializer> _iInitializer;
    Sequence _sequence = null;
    
    #endregion

    #region BuiltIn Methods
    private void Awake()
    {
        _iInitializer.Value.OnInitialize += Initialize;
    }
    #endregion

    #region Custom public Methods
    #endregion

    #region Custom private Methods
    private void Initialize()
    {
        // GameMainManager.Instance.OnReloadScene += PlaySceneTransitionStartAnimation;
        // GameMainManager.Instance.OnReloadSceneEnd += PlaySceneTransitionEndAnimation;
        InitAnimation();
    }

    private void PlaySceneTransitionStartAnimation(UnityAction onSceneTransition)
    {
        if(_sequence != null)
        {
            _sequence.Kill();
        }
        _sequence = DOTween.Sequence();

        // ここにシーン切り替え前演出を入れる（フェードインなど）
        _sequence.AppendCallback(()=>{
            onSceneTransition?.Invoke();
        });
    }
    private void PlaySceneTransitionEndAnimation()
    {
        if(_sequence != null)
        {
            _sequence.Kill();
        }
        _sequence = DOTween.Sequence();

        // ここにシーン切り替え終了演出を入れる（フェードアウトなど）

        // Debug.Log("シーンリロード終了アニメ-ション開始！");
    }

    // 演出初期化
    private void InitAnimation()
    {
        UpdateAnimation();
    }
    // 演出更新
    private void UpdateAnimation()
    {

    }
    #endregion
}