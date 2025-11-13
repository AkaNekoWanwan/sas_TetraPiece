/// <summary>
/// 拡張性に優れるカスタムボタン
/// </summary>
using DG.Tweening;  
using UnityEngine;  
using UnityEngine.EventSystems;  
using UnityEngine.Events;

namespace AkanekoLib
{
    public class CustomButton : MonoBehaviour,  
        IPointerClickHandler,  
        IPointerDownHandler,  
        IPointerUpHandler,
        IPointerEnterHandler
    {
        // ---------- 定数宣言 ----------------------------
        // ---------- ゲームオブジェクト参照変数宣言 ----------
        // ---------- プレハブ ----------------------------
        // ---------- プロパティ --------------------------
        // ---------- クラス変数宣言 -----------------------
        // ---------- インスタンス変数宣言 ------------------
        // ---------- Unity組込関数 -----------------------
        // ---------- Public関数 -------------------------
        public event UnityAction onClick;  
        public event UnityAction onPointerUp;  
        public event UnityAction onPointerDown;  
        public event UnityAction onPointerEnter;  
        public bool IsCommonAnimation = true;
        public bool IsEnable = true;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if(!IsEnable)
                return;
            onPointerEnter?.Invoke();
        }

        public void OnPointerClick(PointerEventData eventData)  
        {
            if(!IsEnable)
                return;
            onClick?.Invoke();  
        }

        public void OnPointerDown(PointerEventData eventData)  
        {
            if(!IsEnable)
                return;
            if(IsCommonAnimation)
                CommonPointerDownAnimation();  
            onPointerDown?.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData)  
        {
            if(!IsEnable)
                return;
            if(IsCommonAnimation)
                CommonPointerUpAnimation();
            onPointerUp?.Invoke();
        }
        public void CommonPointerDownAnimation()
        {
            if(!IsEnable)
                return;
            transform.DOScale(0.95f, 0.2f).SetEase(Ease.OutBack).SetLink(this.gameObject);  
            transform.localScale = Vector3.one * 0.95f;
        }
        public void CommonPointerUpAnimation()
        {
            if(!IsEnable)
                return;
            transform.DOScale(1f, 0.2f).SetEase(Ease.OutBack).SetLink(this.gameObject);  
            transform.localScale = Vector3.one;
        }

        public void ClearAllCallback()
        {
            onClick = null;
            onPointerUp = null;  
            onPointerDown = null;  
            onPointerEnter = null;  
        }
        // ---------- Private関数 ------------------------
    }
}