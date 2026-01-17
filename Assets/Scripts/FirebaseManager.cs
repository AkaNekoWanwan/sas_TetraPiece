using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Firebase.Analytics;

public class FirebaseManager : MonoBehaviour
{
    public int isB;
    public float ssa = 0.5f;
    public int isInit;
    public double rot;
    public int isGimmick;
    public int attackBuffer;

    //DontDestroyにする
    public static FirebaseManager instance;
    public void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(this.gameObject);
        }
        else
        {
            Destroy(this.gameObject);
        }
    }
    // Start is called before the first frame update
    void Start()
    {
        if (isInit == 0)
        {
            AddOpen();
            isInit = 1;
        }

        //RCountはステージ内で何回指を離したか、ステージごとにリセットされる
        //SRCountはステージ内で何回タップし続け破壊を発生させてしまったか
        //FailCountはテージクリアまでにFailになった回数
        //StanCountはテージクリアまでにパトカーと当たったになった回数
    }
    public void FixedUpdate()
    {
        if (attackBuffer > 0)
        {
            attackBuffer++;
            if (attackBuffer > 20)
            {
                attackBuffer = 0;
            }
        }
    }
    //PlayerPrefs.GetInt("Skin", 0)
    public void AddOpen()
    {
        Firebase.Analytics.FirebaseAnalytics.LogEvent("App_Open",
                          new Parameter("Stage", GetCurrentStage())
                        );
    }
    public void StageStart()
    {
        int Current_Attempt = PlayerPrefs.GetInt(IsDailyPrefsSTR() + "Current_Attempt", 1);
        int Failure_count = PlayerPrefs.GetInt(IsDailyPrefsSTR() + "Failure_count", 0);
        int Undo_Count = PlayerPrefs.GetInt(IsDailyPrefsSTR() + "Undo_Count", 0);

        //GoalControllerに入れる　→　スキンに入れちゃうとバナナマンとスティックマンの解除のタイミングでおかしくなりかねない
        LogEventWithUserSegments("Stage_Start",
                            new Parameter("Stage", GetCurrentStage()),
                            new Parameter("Move_Limit", GetMove_Limit()),
                            new Parameter("Move_Count", 0),
                            new Parameter("Current_Attempt", Current_Attempt),
                            new Parameter("Failure_count", Failure_count),
                            new Parameter("IsDaily", IsDailyStage() ? "TRUE" : "FALSE"),
                            new Parameter("Undo_Count", Undo_Count)
                        );
        // 試行回数カウントアップ
        PlayerPrefs.SetInt(IsDailyPrefsSTR() + "Current_Attempt", Current_Attempt + 1);

        // Debug.Log($"FirebaseManager StageStart, Stage: {GetCurrentStage()}, Move_Limit: {GetMove_Limit()}, Move_Count: 0, Current_Attempt: {Current_Attempt}, Failure_count: {Failure_count}, IsDaily: {(IsDailyStage() ? "TRUE" : "FALSE")}, Undo_Count: {Undo_Count}");
    }

    public void StageClear(int Move_Count, float ClearTime)
    {
        int Current_Attempt = PlayerPrefs.GetInt(IsDailyPrefsSTR() + "Current_Attempt", 1);
        int Failure_count = PlayerPrefs.GetInt(IsDailyPrefsSTR() + "Failure_count", 0);
        int Undo_Count = PlayerPrefs.GetInt(IsDailyPrefsSTR() + "Undo_Count", 0);

        //GoalControllerに入れる　→　スキンに入れちゃうとバナナマンとスティックマンの解除のタイミングでおかしくなりかねない
        LogEventWithUserSegments("Stage_Clear",
                            new Parameter("Stage", GetCurrentStage()),
                            new Parameter("Move_Limit", GetMove_Limit()),
                            new Parameter("Move_Count", GetMove_Limit() - Move_Count),
                            new Parameter("Current_Attempt", Current_Attempt),
                            new Parameter("Failure_count", Failure_count),
                            new Parameter("IsDaily", IsDailyStage() ? "TRUE" : "FALSE"),
                            new Parameter("Undo_Count", Undo_Count),
                            new Parameter("ClearTime", ClearTime)   // クリアタイム(これのみ命名に_を使用しないので注意)
                        );
        // 試行回数リセット
        PlayerPrefs.SetInt(IsDailyPrefsSTR() + "Current_Attempt", 1);
        PlayerPrefs.SetInt(IsDailyPrefsSTR() + "Failure_count", 0);
        PlayerPrefs.SetInt(IsDailyPrefsSTR() + "Undo_Count", 0);

        // Debug.Log($"FirebaseManager StageClear, Stage: {GetCurrentStage()}, Move_Limit: {GetMove_Limit()}, Move_Count: {GetMove_Limit() - Move_Count}, Current_Attempt: {Current_Attempt}, Failure_count: {Failure_count}, IsDaily: {(IsDailyStage() ? "TRUE" : "FALSE")}, Undo_Count: {Undo_Count}");
    }
    public void StageFailure(int Move_Count)
    {
        int Current_Attempt = PlayerPrefs.GetInt(IsDailyPrefsSTR() + "Current_Attempt", 1);
        int Failure_count = PlayerPrefs.GetInt(IsDailyPrefsSTR() + "Failure_count", 0);
        Failure_count++;
        PlayerPrefs.SetInt(IsDailyPrefsSTR() + "Failure_count", Failure_count);
        int Undo_Count = PlayerPrefs.GetInt(IsDailyPrefsSTR() + "Undo_Count", 0);
        //GoalControllerに入れる　→　スキンに入れちゃうとバナナマンとスティックマンの解除のタイミングでおかしくなりかねない
        LogEventWithUserSegments("Stage_Failure",
                            new Parameter("Stage", GetCurrentStage()),
                            new Parameter("Move_Limit", GetMove_Limit()),
                            new Parameter("Move_Count", GetMove_Limit() - Move_Count),
                            new Parameter("Current_Attempt", Current_Attempt),
                            new Parameter("Failure_count", Failure_count),
                            new Parameter("IsDaily", IsDailyStage() ? "TRUE" : "FALSE"),
                            new Parameter("Undo_Count", Undo_Count)
                        );
        // Debug.Log($"FirebaseManager Stage_Failure, Stage: {GetCurrentStage()}, Move_Limit: {GetMove_Limit()}, Move_Count: {GetMove_Limit() - Move_Count}, Current_Attempt: {Current_Attempt}, Failure_count: {Failure_count}, IsDaily: {(IsDailyStage() ? "TRUE" : "FALSE")}, Undo_Count: {Undo_Count}");
    }
    public void StageUndo(int Move_Count)
    {
        int Current_Attempt = PlayerPrefs.GetInt(IsDailyPrefsSTR() + "Current_Attempt", 1);
        int Failure_count = PlayerPrefs.GetInt(IsDailyPrefsSTR() + "Failure_count", 0);
        int Undo_Count = PlayerPrefs.GetInt(IsDailyPrefsSTR() + "Undo_Count", 0);
        Undo_Count++;
        PlayerPrefs.SetInt(IsDailyPrefsSTR() + "Undo_Count", Undo_Count);
        LogEventWithUserSegments("Stage_Undo",
                            new Parameter("Stage", GetCurrentStage()),
                            new Parameter("Move_Limit", GetMove_Limit()),
                            new Parameter("Move_Count", GetMove_Limit() - Move_Count),
                            new Parameter("Current_Attempt", Current_Attempt),
                            new Parameter("Failure_count", Failure_count),
                            new Parameter("IsDaily", IsDailyStage() ? "TRUE" : "FALSE"),
                            new Parameter("Undo_Count", Undo_Count)
                        );

        // Debug.Log($"FirebaseManager StageUndo, Stage: {GetCurrentStage()}, Move_Limit: {GetMove_Limit()}, Move_Count: {GetMove_Limit() - Move_Count}, Current_Attempt: {Current_Attempt}, Failure_count: {Failure_count}, IsDaily: {(IsDailyStage() ? "TRUE" : "FALSE")}, Undo_Count: {Undo_Count}");
    }
    public void TapCount(string stageName, bool isTouch)
    {
        LogEventWithUserSegments("Tap_Count",
                            new Parameter("Stage", GetCurrentStage()),
                            new Parameter("IsDaily", IsDailyStage() ? "TRUE" : "FALSE"),
                            new Parameter("StageName", stageName),
                            new Parameter("isTouch", isTouch.ToString())
                        );
    }
    public void RewindMove(string stageName)
    {
        LogEventWithUserSegments("Rewind_Move",
                            new Parameter("Stage", GetCurrentStage()),
                            new Parameter("IsDaily", IsDailyStage() ? "TRUE" : "FALSE"),
                            new Parameter("StageName", stageName)
                        );
    }
  public void Withdrwal(float pureElapsedTime)
    {
        Debug.Log("FirebaseManager Withdrwal");
        LogEventWithUserSegments("Withdrawal",
                         new Parameter("Stage", GetCurrentStage()),
                         new Parameter("IsDaily", IsDailyStage() ? "TRUE" : "FALSE"),
                            new Parameter("engagement_time", pureElapsedTime) // リバイブしたかどうか
                        );
    }

    public void WatchInste(bool CanWatch, string stageName, double eCPM = 0f)
    {
        string canWatch = CanWatch ? "True" : "False";
        Debug.Log("FirebaseManager WatchInsta" + canWatch + " eCPM: " + eCPM);
        LogEventWithUserSegments("Watch_Inste",
                            new Parameter("Stage", GetCurrentStage() - 1),
                            new Parameter("IsDaily", IsDailyStage() ? "TRUE" : "FALSE"),
                            new Parameter("Stage_Name", stageName),
                            new Parameter("eCPM", eCPM),
                            new Parameter("CanWatch", canWatch)
                         );
    }

    public void EventWatchBanner(bool isWatch)
    {
        LogEventWithUserSegments("Watch_Banner", 
                            new Parameter("Stage", GetCurrentStage()),
                            new Parameter("IsDaily", IsDailyStage() ? "TRUE" : "FALSE"),
                            new Parameter("CanWatch", isWatch.ToString()));
    }
    public void EventWatchReward(bool isWatch)
    {
        int watchRewardCount = -1;
        if(isWatch)
        {
            watchRewardCount = PlayerPrefs.GetInt("WatchRewardCount", 1);
        }

        LogEventWithUserSegments("Watch_Reward", 
                            new Parameter("Stage", GetCurrentStage()),
                            new Parameter("IsDaily", IsDailyStage() ? "TRUE" : "FALSE"),
                            new Parameter("CanWatch", isWatch.ToString()),
                            new Parameter("WatchRewardCount", watchRewardCount));
        if(isWatch)
        {
            watchRewardCount++;
            PlayerPrefs.SetInt("WatchRewardCount", watchRewardCount);
        }
    }
    public void RevenueBanner(float eCPM)
    {
        Debug.Log("FirebaseManager RevenueBanner"+ eCPM);
            LogEventWithUserSegments("Watch_Banner",
                            new Parameter("Stage", GetCurrentStage()),
                            new Parameter("IsDaily", IsDailyStage() ? "TRUE" : "FALSE"),
                            new Parameter("eCPM", eCPM));

    }

    private int GetCurrentStage()
    {
        int stage = PlayerPrefs.GetInt("totalLevel", 1);
        if(IsDailyStage())
        {
            stage = PlayerPrefs.GetInt("DailyStage", -1) + 1;
        }
        return stage;
    }
    private int GetMove_Limit()
    {
        int Move_Limit = GameDataManager.InitMoveCount;
        if(IsDailyStage())
        {
            Move_Limit = GameDataManager.DailyInitMoveCount;
        }
        return Move_Limit;
    }
    private bool IsDailyStage()
    {
        return 0 <= PlayerPrefs.GetInt("DailyStage", -1);
    }
    private string IsDailyPrefsSTR()
    {
        return IsDailyStage() ? "DAILY" : "NORMAL";
    }

    /// <summary>
    /// ユーザーセグメントパラメータを自動追加してFirebaseイベントをログ
    /// </summary>
    /// <param name="eventName">イベント名</param>
    /// <param name="baseParameters">基本パラメータ</param>
    private void LogEventWithUserSegments(string eventName, params Parameter[] baseParameters)
    {
        var parameterList = new List<Parameter>(baseParameters);
        
        // UserSegmentがinstance化されていればABテストパラメータを追加
        if (UserSegment.instance != null)
        {
            parameterList.AddRange(UserSegment.instance.GetABTestParameters());
        }

        string debugParamStr = "";
        // foreach(var param in parameterList)
        // {
        //     debugParamStr += $"{param.name}:{param.value}, ";
        // }
        // Debug.Log($"FirebaseManager LogEventWithUserSegments: {eventName}, ParamsCount: {parameterList.Count}, Params: {debugParamStr}");
        Firebase.Analytics.FirebaseAnalytics.LogEvent(eventName, parameterList.ToArray());
    }
}
