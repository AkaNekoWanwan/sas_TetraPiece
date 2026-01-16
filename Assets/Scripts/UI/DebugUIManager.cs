using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;
using System.Collections.Generic;
using UnityEngine.UI;

public class DebugUIManager : MonoBehaviour
{
    public List<RectTransform> _debugButtons = default;
    public List<int> _switchViewCommand;
    public GameObject _view = default;

    public Text _buttonText = default;
    public InputField _inputField = default;
    public TMPro.TextMeshProUGUI _textFrameRate = null;
    public TMPro.TextMeshProUGUI _textAdsTimer = null;

    public List<int> _currentCommand = default;

    public event System.Action<bool> onDebugViewToggled;
    private bool _isFirstShow = false;
    
    [SerializeField, Tooltip("ABテストボタンリスト親")] private RectTransform _abTestButtonContents = default;
    [SerializeField, Tooltip("ABテストボタンプレハブ")] private DebugABTestButton _abTestButtonPrefab = default;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _currentCommand = new List<int>();
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if(TryTapCommandButton(_currentCommand.Count))
            {
                
            }
            else
            {
                _currentCommand.Clear();
                TryTapCommandButton(0);
            }
        }
         // フレームレート表示の更新
        float fps = 1.0f / Time.unscaledDeltaTime;
        if(fps < 50f)
            _textFrameRate.color = new Color32(128, 0, 0, 255);
        else
            _textFrameRate.color = Color.black;
        _textFrameRate.text = "FPS: " + fps.ToString("F2");
        _textAdsTimer.text = "AdsTimer: " + AdsTimerManager.instance.ElapsedTime.ToString("F2");
    }

    public void Initialize()
    {
        _view.SetActive(GameDataManager.IsDebugView);
        _textFrameRate.gameObject.SetActive(GameDataManager.IsShowKeepDebugText);
        _textAdsTimer.gameObject.SetActive(GameDataManager.IsShowKeepDebugText);

        SerializedDictionary<List<int>> userPropertyDic;
        // ABテストボタン初期化
        foreach (Transform child in _abTestButtonContents)
        {
            Destroy(child.gameObject);
        }
        var _userPropertyDic = UserSegment.instance.DebugGetUserPropertyDictionary();

        Debug.Log("ユーザープロパティの数:" + _userPropertyDic.Keys.Count);
        foreach( string key in _userPropertyDic.Keys )
        {
            List<int> paramList = _userPropertyDic[key];
            if( 1 <= paramList.Count )
            {
                DebugABTestButton button = Instantiate(_abTestButtonPrefab);
                button.transform.parent = _abTestButtonContents;
                button.transform.localScale = Vector3.one;
                button.transform.localPosition = Vector3.zero;
                button.Initialize(key, _userPropertyDic[key], ()=>{
                    // ユーザープロパティ更新後の処理
                    // _stageManager.ResetStageBundle();
                    // ResetStageSelectButtons();
                    // _inGameManager.UndoInGame();
                    // GameDataManager.UpdatekillShockStrength();
                    // UpdateText();
                    // PlayerPrefs.SetInt(key, randomValue);
                    GameDataManager.InvokeUpdateUserSegment();
                });
            }
        }
    }

    public void OnSwitchCreativeMode()
    {
        bool isCreativeMode = !GameDataManager.IsCreativeMode;
        GameDataManager.IsCreativeMode = isCreativeMode;
        GameDataManager.IsDebugView = false;
        FadeManager.Instance.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name, 0.3f);
    }

    public void ToggleFrameRateText(bool value)
    {
        GameDataManager.IsShowKeepDebugText = value;
        Debug.Log($"DebugUIManager: ToggleFrameRateText: {GameDataManager.IsShowKeepDebugText}");
        // _textFrameRate.gameObject.SetActive(GameDataManager.IsShowKeepDebugText);
    }

    // _inputFieldの値が変更されたときに呼ばれる
    public void OnInputFieldValueChanged()
    {
        _buttonText.text = "ステージ" + int.Parse(_inputField.text) + "をプレイ";
    }

    public void OnClose()
    {
        _view.SetActive(false);
        GameDataManager.IsDebugView = false;
        onDebugViewToggled?.Invoke(false);
        if(!GameDataManager.IsShowKeepDebugText)
        {
            _textFrameRate.gameObject.SetActive(false);
            _textAdsTimer.gameObject.SetActive(false);
        }
    }

    public void OnSetTotalLevel()
    {
        int inputLevel = int.Parse(_inputField.text);
        PlayerPrefs.SetInt("totalLevel", inputLevel);

        int stageLevel = inputLevel;
        if(504 < inputLevel)
        {
            inputLevel = (inputLevel - 504) % (504 - 25) + 25; // 25〜504の範囲に変換
        }
        PlayerPrefs.SetInt("Stage", inputLevel - 1);
        GameDataManager.InitMoveCount = -1;
        GameDataManager.DailyInitMoveCount = -1;
        GameDataManager.IsDebugView = false;
        FadeManager.Instance.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name, 0.3f);
    }

    // 指定したコマンドインデックスのボタンを押しているか
    private bool TryTapCommandButton(int commandIndex)
    {   
        Debug.Log($"DebugChk:1:{commandIndex}");
        int chkButtonIndex = _switchViewCommand[commandIndex];
        RectTransform chkButton = _debugButtons[chkButtonIndex];
        if(IsPointerOverPiece(chkButton))
        {
            _currentCommand.Add(chkButtonIndex);
            if(_currentCommand.Count == _switchViewCommand.Count)
            {
                bool isActive = !_view.activeSelf;
                _currentCommand.Clear();
                _view.SetActive(isActive);
                onDebugViewToggled?.Invoke(isActive);
                GameDataManager.IsDebugView = isActive;
                if(isActive)
                {
                    _textFrameRate.gameObject.SetActive(true);
                    _textAdsTimer.gameObject.SetActive(true);
                    if(!_isFirstShow)
                    {
                        _isFirstShow = true;
                        GameDataManager.IsShowKeepDebugText = true;
                    }
                }
                else
                {
                    if(!GameDataManager.IsShowKeepDebugText)
                    {
                        _textFrameRate.gameObject.SetActive(false);
                        _textAdsTimer.gameObject.SetActive(false);
                    }
                }
            }
            return true;
        }
        return false;
    }

    private bool IsPointerOverPiece(RectTransform button)
    {
        bool ret = RectTransformUtility.RectangleContainsScreenPoint(button, Input.mousePosition, Camera.main);
        Debug.Log($"DebugChk:2:{button.gameObject.name}, {ret}");
        return ret;
    }
}
