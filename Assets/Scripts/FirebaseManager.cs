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
                          new Parameter("Stage", PlayerPrefs.GetInt("totalLevel", 1))

                            //  new Parameter("is_Pop", PlayerPrefs.GetInt("isPop",0)),
                            //     new Parameter("is_Slow", PlayerPrefs.GetInt("isSlow",0)),
                            //     new Parameter("is_Big", PlayerPrefs.GetInt("isBig",0)),
                            //     new Parameter("is_ForwardCheck", PlayerPrefs.GetInt("isForwardCheck",0)),
                            //      new Parameter("is_SwithQuick", PlayerPrefs.GetInt("isSwithQuick",0)),

                            //                                    new Parameter("Slow_Function_Count", PlayerPrefs.GetInt("SlowFunction", 0))
                            );

    }
    public void StageStart(string stageName)
    {


        //GoalControllerに入れる　→　スキンに入れちゃうとバナナマンとスティックマンの解除のタイミングでおかしくなりかねない
        Firebase.Analytics.FirebaseAnalytics.LogEvent("Stage_Start",
                         new Parameter("Stage", PlayerPrefs.GetInt("totalLevel", 1)),
                            new Parameter("StageName", stageName)

                         );
    }

  public void StageClear(string stageName, float clearTime)
    {

        Debug.Log("FirebaseManager StageClear" + stageName + " " + clearTime);

        Firebase.Analytics.FirebaseAnalytics.LogEvent("Stage_Clear",
                         new Parameter("Stage", PlayerPrefs.GetInt("totalLevel", 1)),
                            new Parameter("StageName", stageName),
                            new Parameter("ClearTime", clearTime)

                         );
    }
    public void StageFail(string stageName)
    {
   
        Firebase.Analytics.FirebaseAnalytics.LogEvent("Stage_Fail",
                         new Parameter("Stage", PlayerPrefs.GetInt("totalLevel", 1)),
                            new Parameter("StageName", stageName)
                         );
    }
    public void StageRestart(string stageName)
    {
        
        Firebase.Analytics.FirebaseAnalytics.LogEvent("Stage_Restart",
                         new Parameter("Stage", PlayerPrefs.GetInt("totalLevel", 1)),
                            new Parameter("StageName", stageName)
                         );
    }
    public void TapCount(string stageName, bool isTouch)
    {
        Firebase.Analytics.FirebaseAnalytics.LogEvent("Tap_Count",
                         new Parameter("Stage", PlayerPrefs.GetInt("totalLevel", 1)),
                            new Parameter("StageName", stageName),
                            new Parameter("isTouch", isTouch.ToString())
                         );
    }
    public void RewindMove(string stageName)
    {
        Firebase.Analytics.FirebaseAnalytics.LogEvent("Rewind_Move",
                         new Parameter("Stage", PlayerPrefs.GetInt("totalLevel", 1)),
                            new Parameter("StageName", stageName)
                         );
    }
  public void Withdrwal(float pureElapsedTime)
    {
        Debug.Log("FirebaseManager Withdrwal");
        Firebase.Analytics.FirebaseAnalytics.LogEvent("Withdrawal",
                         new Parameter("Stage", PlayerPrefs.GetInt("totalLevel", 1)),
                            new Parameter("engagement_time", pureElapsedTime) // リバイブしたかどうか
                         );
    }

    public void EventWatchInste(bool isWatch)
    {
        int watchInsteCount = -1;
        if(isWatch)
        {
            watchInsteCount = PlayerPrefs.GetInt("WatchInsteCount", 1);
        }

        FirebaseAnalytics.LogEvent("Watch_Inste", 
                            new Parameter("Stage", PlayerPrefs.GetInt("totalLevel", 1)),
                            new Parameter("CanWatch", isWatch.ToString()),
                            new Parameter("WatchInsteCount", watchInsteCount));
        // Debug.Log("isWatch:" + isWatch + ", " + watchInsteCount);
        if(isWatch)
        {
            watchInsteCount++;
            PlayerPrefs.SetInt("WatchInsteCount", watchInsteCount);
        }
    }
    public void EventWatchBanner(bool isWatch)
    {
        FirebaseAnalytics.LogEvent("Watch_Banner", 
                            new Parameter("Stage", PlayerPrefs.GetInt("totalLevel", 1)),
                            new Parameter("CanWatch", isWatch.ToString()));
    }
    public void EventWatchReward(bool isWatch)
    {
        int watchRewardCount = -1;
        if(isWatch)
        {
            watchRewardCount = PlayerPrefs.GetInt("WatchRewardCount", 1);
        }

        FirebaseAnalytics.LogEvent("Watch_Reward", 
                            new Parameter("Stage", PlayerPrefs.GetInt("totalLevel", 1)),
                            new Parameter("CanWatch", isWatch.ToString()),
                            new Parameter("WatchRewardCount", watchRewardCount));
        // Debug.Log("isWatchReward:" + isWatch + ", " + watchRewardCount);
        if(isWatch)
        {
            watchRewardCount++;
            PlayerPrefs.SetInt("WatchRewardCount", watchRewardCount);
        }
    }
}
