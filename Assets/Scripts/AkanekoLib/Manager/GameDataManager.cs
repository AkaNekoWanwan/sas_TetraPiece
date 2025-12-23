using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;


public static class GameDataManager
{
    private static StageManager _currentStage = null;
    public static StageManager CurrentStage => _currentStage;
    public static void SetCurrentStage(StageManager stageManager) { _currentStage = stageManager; }

    public static int InitMoveCount = -1;
    public static int DailyInitMoveCount = -1;

    public static bool IsClear = false;
    public static bool IsBlockTouch = false;
    public static float FixScale = 1f;
    public static float CanvasWidth = 1080f;
    public static Color32 GridOutLineColor = default;

    private static bool _isInit = false;
    public static bool IsInit => _isInit;
    public static bool IsHome = true;
    public static bool IsHard = false;

    public static bool IsDebugView = false;

    public static event UnityAction<bool> onOptionVibChanged;
    public static event UnityAction<bool> onOptionSeChanged;
    public static event UnityAction<bool> onOptionBgmChanged;

    public static void Initialize()
    {
        if(_isInit)
            return;
        _isInit = true;
    } 


    // ステージスタート処理の設定
    private static UnityEvent _onStageStart = null;
    public static void AddOnStageStart(UnityAction onStageStart)
    {
        if (_onStageStart == null)
            _onStageStart = new UnityEvent();
        _onStageStart.AddListener(onStageStart);
    }
    private static bool _waitEventStageStart = false;   // ステージスタートイベント待機フラグ。ステージをクリアor失敗したらOnになる。インステを見終わるか、インステが流れなかったらOffにしてステージスタートイベントを発火させるようにする
    public static void OnWaitEventStageStart()
    {
        _waitEventStageStart = true;
    }
    public static void TryEventStageStart()
    {
        if (!_waitEventStageStart)
            return;
        _onStageStart?.Invoke();
        _onStageStart = new UnityEvent();
        _waitEventStageStart = false;
    }

    private static bool _isRestart = false;
    public static bool IsRestart => _isRestart;
    public static void SetIsRestart(bool isRestart)
    {
        _isRestart = isRestart;
    }

    private static LevelParamMemory _levelParam;
    public static LevelParamMemory LevelParam => _levelParam;
    public static void SetLevelParamMemory(LevelParamMemory levelParam)
    {
        _levelParam = levelParam;
    }

    private static float _pureElapsedTime;
    public static float PureElapsedTime{ get => _pureElapsedTime; set{ _pureElapsedTime = value; } }

    private static bool _isCreativeHandWait = false;
    public static bool IsCreativeHandWait { get => _isCreativeHandWait; set { _isCreativeHandWait = value; } }

    private static Vector2 _creativeLastCursorPos = Vector2.zero;
    public static Vector2 CreativeLastCursorPos { get => _creativeLastCursorPos; set { _creativeLastCursorPos = value; } }

    public static bool isPlayHomePieceAnimation = false;

    public static void OnChangeOption(string optionType, bool isOn)
    {
        switch(optionType)
        {
            case "IsVib":
                onOptionVibChanged?.Invoke(isOn);
                break;
            case "IsSe":
                onOptionSeChanged?.Invoke(isOn);
                break;
            case "IsMusic":
                onOptionBgmChanged?.Invoke(isOn);
                break;
        }
    }

    public static bool IsCreativeTwoPointTouch()
    {
        if(GameConst.IsCreativeMode() == false)
            return false;
        if(2 <= Input.touchCount)
            return true;
        if((Input.GetKey(KeyCode.LeftShift) || Input.GetMouseButton(1)) && Input.GetMouseButton(0))
            return true;
        return false;
    }


    // クリエイティブモードをゲーム中に切り替えられるようにするための処理
    private static bool IsCreativeMode = false;
    private static bool _onTouch = false;
    private static bool _onTouchDown = false;
    private static bool _onTouchUp = false;
    private static bool _beforeOnTouch = false;
    public static bool OnTouch => _onTouch;
    public static bool OnTouchDown => _onTouchDown;
    public static bool OnTouchUp => _onTouchUp;
    public static void UpdateTouchInfo()
    {
        if(!GameConst.IsCreativeMode())
        {
            if(Input.touchCount > 0 || Input.GetMouseButton(0))
            {
                _onTouch = true;
            }
            else
            {
                _onTouch = false;
            }
        }
        else
        {
            if(IsCreativeTwoPointTouch())
            {
                _onTouch = true;
            }
            else
            {
                _onTouch = false;
            }
        }

        if(_onTouch)
        {
            if(!_beforeOnTouch)
                _onTouchDown = true;
            else
                _onTouchDown = false;
            _onTouchUp = false;
            _beforeOnTouch = true;
        }
        else
        {
            if(_beforeOnTouch)
                _onTouchUp = true;
            else
                _onTouchUp = false;
            _onTouchDown = false;
            _beforeOnTouch = false;
        }
    }

    public static Vector2 GetMousePosition()
    {
        if(!GameConst.IsCreativeMode())
        {
            if(Input.touchCount > 0)
            {
                return Input.GetTouch(0).position;
            }
            else
            {
                return Input.mousePosition;
            }
        }
        else
        {
            if(Input.touchCount > 0)
            {
                return Input.GetTouch(0).position;
            }
            else
            {
                return Input.mousePosition;
            }
        }
    }
}
