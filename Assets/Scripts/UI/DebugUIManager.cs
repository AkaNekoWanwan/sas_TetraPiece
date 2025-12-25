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

    public List<int> _currentCommand = default;

    public event System.Action<bool> onDebugViewToggled;


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
    }

    public void OnSwitchCreativeMode()
    {
        bool isCreativeMode = !GameDataManager.IsCreativeMode;
        GameDataManager.IsCreativeMode = isCreativeMode;
        GameDataManager.IsDebugView = false;
        FadeManager.Instance.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name, 0.3f);
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
