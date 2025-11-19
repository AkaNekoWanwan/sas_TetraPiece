using UnityEngine;
using DG.Tweening;
using AkanekoLib;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;

public class SettingManager : MonoBehaviour
{
    public Transform _settingView = default;
    public CustomButton _closeButton = default;
    public CustomButton _openButton = default;

    public OptionButton _buttonSound;
    public OptionButton _buttonBgm;
    public OptionButton _buttonVib;
    public AudioSource audioSource;
    public AudioSource bgmAudioSource;

    public bool IsOpen { get { return _settingView.localScale == Vector3.one; } }

    private Tween _viewTween = null; 


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _settingView.gameObject.SetActive(true);
        _settingView.localScale = Vector3.zero;
        _closeButton.onClick += CloseView;
        _openButton.onClick += OpenView;

        _buttonVib.onOptionChanged += (isOn) =>
        {
            if (isOn)
            {
                // バイブレーション
                VibratorManager.Vibrate(70, 40);
            }
        };
        _buttonSound.onOptionChanged += (isOn) =>
        {
            audioSource.mute = !isOn;
        };
        _buttonBgm.onOptionChanged += (isOn) =>
        {
            bgmAudioSource.mute = !isOn;
        };
    }
    void Update()
    {
        if (Input.GetMouseButtonDown(0) && IsOpen) // 画面のどこでもタッチ（クリック）を検知
        {
            // ターゲット画像以外のUI、またはUIのない空間にタッチが落ちたかを判定
            if (!IsPointerOverUI(_settingView.gameObject))
            {
                CloseView(); // 設定画面を閉じる
            }
        }
    }

    // タッチ位置が特定のターゲットUIの上にあるかを判定するヘルパー関数
    bool IsPointerOverUI(GameObject targetObject)
    {
        if (EventSystem.current == null) return false;

        // PointerEventDataを作成し、現在のポインタ位置でRaycastを実行
        PointerEventData eventData = new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition // タッチ/マウスの位置
        };

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        foreach (RaycastResult result in results)
        {
            if (result.gameObject == targetObject || 
                result.gameObject.transform.IsChildOf(targetObject.transform))
            {
                return true; // ターゲットにヒットした
            }
        }
        return false; // ターゲットにヒットしなかった
    }

    public void OpenView()
    {
        VibratorManager.Vibrate(70, 40);
        Debug.Log("SettingManager:Open Setting View");
        _settingView.gameObject.SetActive(true);
        _viewTween?.Kill();
        _viewTween = _settingView.DOScale(Vector3.one, 0.2f).SetEase(Ease.OutBack).SetLink(this.gameObject);
    }

    public void CloseView()
    {
        VibratorManager.Vibrate(70, 40);
        _viewTween?.Kill();
        _viewTween = _settingView.DOScale(Vector3.zero, 0.2f).SetEase(Ease.InBack).OnComplete(() =>
        {
            _settingView.gameObject.SetActive(false);
        }).SetLink(this.gameObject);
    }
}
