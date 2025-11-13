using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;


public static class GameDataManager
{
    private static StageManager _currentStage = null;
    public static StageManager CurrentStage => _currentStage;
    public static void SetCurrentStage(StageManager stageManager) { _currentStage = stageManager; }

    public static bool IsClear = false;
    public static bool IsBlockTouch = false;
    public static float FixScale = 1f;
    public static float CanvasWidth = 1080f;


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

    // private static TargetInfoContainer _activeTargetInfoContainer = null;
    // public static TargetInfoContainer ActiveTargetInfoContainer => _activeTargetInfoContainer;
    // public static void SetActiveTargetInfoContainer(TargetInfoContainer targetInfoContainer)
    // {
    //     _activeTargetInfoContainer = targetInfoContainer;
    // }

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

}
