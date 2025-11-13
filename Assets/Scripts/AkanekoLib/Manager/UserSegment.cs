using System.Collections;
using System.Collections.Generic;
using UnityEngine;
// using Firebase.Analytics;

// ユーザーの組み分け(ABテストフラグ設定)クラス。
public class UserSegment : MonoBehaviour
{
    [SerializeField] private SerializeInterface<IInitializer> _iInitializer;
    [SerializeField, Tooltip("ユーザープロパティランダム")] private SerializedDictionary<List<int>> _userPropertyDic = default;
    // ---------- Unity組込関数 -----------------------
    private void Awake()
    {
        // GameMainManager.Instance.IInitializer.OnInitialize += Initialize;
    }
    // ---------- Public関数 ----------
    private void Initialize()
    {
        // // ユーザープロパティ(ABテストフラグ)設定
        // foreach( string key in _userPropertyDic.Keys )
        // {
        //     int currentSetValue = PlayerPrefs.GetInt(key, -1);
        //     List<int> valueList = _userPropertyDic[key];

        //     if(valueList.Count <= 0)
        //         Debug.LogError("値が未設定のABテストフラグがあります!! :" + key);

        //     // ユーザープロパティが未設定or現バージョンに存在しないプロパティになっていたら、ランダムに振り分け直してセーブする
        //     // このユーザープロパティが設定済みなら何もしない
        //     if(!valueList.Contains(currentSetValue))
        //     {
        //         int randomIndex = Random.Range(0, valueList.Count);
        //         int randomValue = valueList[randomIndex];
            
        //         PlayerPrefs.SetInt(key, randomValue);
        //     }

        //     // フラグが１種類じゃない（検証中のフラグ）であればFirebaseにSetUserPropertyする
        //     if( 2 <= valueList.Count )
        //     {
        //         Firebase.Analytics.FirebaseAnalytics.SetUserProperty(key,PlayerPrefs.GetInt(key).ToString());
        //         // // Debug.Log("ユーザープロパティ設定！：" + key + ", " + PlayerPrefs.GetInt(key));
        //     }
        // }
    }

    // デバッグマネージャー用：ユーザープロパティのディクショナリーを返す
    public SerializedDictionary<List<int>> DebugGetUserPropertyDictionary(){ return _userPropertyDic; }
    // ---------- Private関数 ----------
}
