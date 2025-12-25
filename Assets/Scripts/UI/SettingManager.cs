using UnityEngine;
using DG.Tweening;
using AkanekoLib;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.SceneManagement;

public class SettingManager : MonoBehaviour
{
    public Transform _settingView = default;
    public CustomButton _closeButton = default;
    public CustomButton _openButton = default;
    public CustomButton _backButton = default;

    public Transform _stageUI = default;
    public Transform _homeUI = default;

    public AudioSource audioSource;
    public AudioSource bgmAudioSource;

    public List<OptionButton> _optionButtions = default;

    public bool IsOpen { get { return _settingView.localScale == Vector3.one; } }

    private Tween _viewTween = null; 


    // private void OnValidate() {
    //     _optionButtions = new List<OptionButton>();
    //     _optionButtions = GetComponentsInChildren<OptionButton>().ToList();
    // }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if(AudioManager.Instance == null)
        {
            SceneManager.LoadScene ("FirstLoadScene");
            return;
        }

        _settingView.gameObject.SetActive(true);
        _settingView.localScale = Vector3.zero;
        _closeButton.onClick += CloseView;
        _openButton.onClick += OpenView;

        audioSource = AudioManager.Instance.audioSource;
        bgmAudioSource = AudioManager.Instance.bgmAudioSource;
        
        // オプションボタンが押された時のコールバック設定　余裕かあったらこのクラスをインスタンス化してここに処理をコールバックも描きたい
        GameDataManager.onOptionVibChanged += ChangeVib;
        GameDataManager.onOptionSeChanged += ChangeSe;
        GameDataManager.onOptionBgmChanged += ChangeBgm;

        _backButton.onClick += () =>
        {
            // ホームに戻る
            GameDataManager.IsHome = true;
            PlayerPrefs.SetInt("FirstLoadScene", -1);
            PlayerPrefs.SetInt("DailyStage", -1);
            FadeManager.Instance.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name, 0.5f);
        };
    }
    private void OnDisable() {
        GameDataManager.onOptionVibChanged -= ChangeVib;
        GameDataManager.onOptionSeChanged -= ChangeSe;
        GameDataManager.onOptionBgmChanged -= ChangeBgm;
    }
    private void ChangeVib(bool isOn)
    {
        if (isOn)
        {
            // バイブレーション
            VibratorManager.Vibrate(70, 40);
        }
    }
    private void ChangeSe(bool isOn)
    {
        audioSource.mute = !isOn;
    }
    private void ChangeBgm(bool isOn)
    {
        bgmAudioSource.mute = !isOn;
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
        _stageUI.gameObject.SetActive(!GameDataManager.IsHome);
        _homeUI.gameObject.SetActive(GameDataManager.IsHome);

        for(int i = 0; i < _optionButtions.Count; i++)
        {
            _optionButtions[i].UpdateView();
        }
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
