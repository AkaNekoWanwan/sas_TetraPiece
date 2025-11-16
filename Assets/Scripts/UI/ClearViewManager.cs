using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using DG.Tweening;

public class ClearViewManager : MonoBehaviour
{
    [SerializeField, Tooltip("文字")] private Text _clearText = default;
    [SerializeField, Tooltip("文字影")] private Text _clearTextShadow = default;
    [SerializeField, Tooltip("文字親")] private Transform _textParent = default;
    [SerializeField, Tooltip("文字")] private List<string> _texts = default;
    
    void Awake()
    {
        int i = UnityEngine.Random.Range(0, _texts.Count);
        _clearText.text = _texts[i];
        _clearTextShadow.text = _texts[i]; 
        _textParent.localScale = Vector3.zero;
    }

    public void PosText()
    {
        _textParent.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack).SetLink(this.gameObject);
    }
}
