using System.Collections;
using System.Collections.Generic;
using UnityEngine;
// using Firebase.Analytics;

// ユーザーセグメントキーのEnum
public enum UserSegmentKey
{
    A_B,         // A or Bグループ
    AdInterval,  // 広告表示間隔
    IsMove       // 移動回数制限の有無
}

// ユーザーの組み分け(ABテストフラグ設定)クラス。
public class UserSegment : MonoBehaviour
{
    [SerializeField] private SerializeInterface<IInitializer> _iInitializer;
    [SerializeField, Tooltip("ユーザープロパティランダム")] private SerializedDictionary<List<int>> _userPropertyDic = default;
    [SerializeField] private bool _isResetUserPropertyDic;

    // ABTestパラメータのキャッシュ
    private List<Firebase.Analytics.Parameter> _cachedABTestParameters = null;

    public static UserSegment instance { get; private set; }

    // ---------- Unity組込関数 -----------------------
    private void OnValidate() {
        ResetUserPropertyDictionary();
    }

    private void Awake()
    {
        if(instance == null)
        {
            DontDestroyOnLoad(this.gameObject);
            instance = this;
        }
        else
        {
            Destroy(this.gameObject);
            return;
        }
        
        ResetUserPropertyDictionary();
        // GameMainManager.Instance.IInitializer.OnInitialize += Initialize;
        Initialize();
    }
    // ---------- Public関数 ----------
    private void Initialize()
    {
        // ユーザープロパティ(ABテストフラグ)設定
        foreach( string key in _userPropertyDic.Keys )
        {
            int currentSetValue = PlayerPrefs.GetInt(key, -1);
            List<int> valueList = _userPropertyDic[key];

            if(valueList.Count <= 0)
                Debug.LogError("値が未設定のABテストフラグがあります!! :" + key);

            // ユーザープロパティが未設定or現バージョンに存在しないプロパティになっていたら、ランダムに振り分け直してセーブする
            // このユーザープロパティが設定済みなら何もしない
            if(!valueList.Contains(currentSetValue))
            {
                int randomIndex = Random.Range(0, valueList.Count);
                int randomValue = valueList[randomIndex];
            
                PlayerPrefs.SetInt(key, randomValue);
            }

            // フラグが１種類じゃない（検証中のフラグ）であればFirebaseにSetUserPropertyする
            if( 2 <= valueList.Count )
            {
                Firebase.Analytics.FirebaseAnalytics.SetUserProperty(key,PlayerPrefs.GetInt(key).ToString());
                // // Debug.Log("ユーザープロパティ設定！：" + key + ", " + PlayerPrefs.GetInt(key));
            }
        }
        
        // ★ ABTestパラメータをキャッシュ作成（パフォーマンス最適化）
        BuildABTestParametersCache();
    }

    // デバッグマネージャー用：ユーザープロパティのディクショナリーを返す
    public SerializedDictionary<List<int>> DebugGetUserPropertyDictionary(){ return _userPropertyDic; }

    /// <summary>
    /// UserSegmentKeyをstring（PlayerPrefsのキー名）に変換
    /// </summary>
    public static string GetKeyString(UserSegmentKey key)
    {
        switch (key)
        {
            case UserSegmentKey.A_B:
                return "A_B";
            case UserSegmentKey.AdInterval:
                return "Ad_Interval";
            case UserSegmentKey.IsMove:
                return "IsMove";
            default:
                // defaultの場合はenumの変数名をそのまま返す
                return key.ToString();
        }
    }

    /// <summary>
    /// ABテスト中（valueListのCountが2以上）のユーザーセグメントパラメータを取得
    /// Firebase Analyticsのイベントに自動追加するために使用
    /// </summary>
    /// <returns>ABテスト中のパラメータのリスト（キャッシュ済み）</returns>
    public List<Firebase.Analytics.Parameter> GetABTestParameters()
    {
        // キャッシュがない場合は作成
        if (_cachedABTestParameters == null)
        {
            BuildABTestParametersCache();
        }
        
        return _cachedABTestParameters;
    }

    /// <summary>
    /// ABTestパラメータのキャッシュを作成（Initialize時やパラメータ変更時に呼び出す）
    /// </summary>
    private void BuildABTestParametersCache()
    {
        _cachedABTestParameters = new List<Firebase.Analytics.Parameter>();

        string value = ((byte)GetValue<int>(UserSegmentKey.AdInterval)).ToString();
        _cachedABTestParameters.Add(new Firebase.Analytics.Parameter(GetKeyString(UserSegmentKey.AdInterval), value));
        value = ((byte)GetValue<int>(UserSegmentKey.IsMove)).ToString();
        _cachedABTestParameters.Add(new Firebase.Analytics.Parameter(GetKeyString(UserSegmentKey.IsMove), value));
        // foreach (UserSegmentKey key in System.Enum.GetValues(typeof(UserSegmentKey)))
        // {
        //     string keyString = GetKeyString(key);
            
        //     // ABテスト中（valueListのCountが2以上）のキーのみ追加
        //     if (_userPropertyDic.ContainsKey(keyString))
        //     {
        //         List<int> valueList = _userPropertyDic[keyString];
        //         if (valueList.Count >= 2)
        //         {
        //             int value = PlayerPrefs.GetInt(keyString, -1);
        //             _cachedABTestParameters.Add(new Firebase.Analytics.Parameter(keyString, value));
        //         }
        //     }
        // }
    }

    /// <summary>
    /// ユーザーセグメントの値を指定した型で取得する統合メソッド（enum版）
    /// </summary>
    /// <typeparam name="T">取得したい型 (float, bool, string)</typeparam>
    /// <param name="key">プロパティキー（UserSegmentKey.AdInterval, UserSegmentKey.IsMove など）</param>
    /// <returns>指定された型に変換された値</returns>
    /// <example>
    /// float interval = UserSegment.instance.GetValue<float>(UserSegmentKey.AdInterval); // 60f or 90f
    /// bool isMove = UserSegment.instance.GetValue<bool>(UserSegmentKey.IsMove); // true or false
    /// string text = UserSegment.instance.GetValue<string>(UserSegmentKey.AdInterval); // "60秒" or "90秒"
    /// </example>
    public T GetValue<T>(UserSegmentKey key, int value = -1)
    {
        return GetValue<T>(GetKeyString(key), value);
    }

    /// <summary>
    /// ユーザーセグメントの値を指定した型で取得する統合メソッド（string版・後方互換性用）
    /// </summary>
    public T GetValue<T>(string propertyName, int value = -1)
    {
        if (value == -1)
        {
            value = PlayerPrefs.GetInt(GetKeyString(UserSegmentKey.A_B), -1);
        }
        
        // if (!_userPropertyDic.ContainsKey(propertyName))
        // {
        //     Debug.LogError($"未設定のプロパティ: {propertyName}");
        //     return default(T);
        // }

        switch (propertyName)
        {
            case "A_B":  // UserSegmentKey.A_B
                string ret = "A";
                if(value == 1)
                    ret = "B";

                if (typeof(T) == typeof(string))
                {
                    return (T)(object)ret;
                }
                break;
            case "Ad_Interval":  // UserSegmentKey.AdInterval
                float fret = 60f;
                if(value == 1)
                    fret = 90f;

                if (typeof(T) == typeof(float))
                {
                    // 0 → 60秒, 1 → 90秒
                    return (T)(object)fret;
                }
                else if (typeof(T) == typeof(string))
                {
                    return (T)(object)(fret + "秒");
                }
                else if (typeof(T) == typeof(int))
                {
                    return (T)(object)((int)fret);
                }
                break;
            
            case "IsMove":  // UserSegmentKey.IsMove
                bool boolValue = (value == 0);  // 0 → 移動制限有り(true), 1 → 移動制限無し(false)

                if (typeof(T) == typeof(bool))
                {
                    // 0 → false(無), 1 → true(有)
                    return (T)(object)boolValue;
                }
                else if (typeof(T) == typeof(string))
                {
                    return (T)(object)GetValueStringToBool(boolValue);
                }
                else if (typeof(T) == typeof(int))
                {
                    return (T)(object)(value);
                }
                break;
        }
        
        Debug.LogError($"サポートされていない型またはプロパティの組み合わせ: {propertyName}, {typeof(T)}");
        return default(T);
    }


    // ---------- Private関数 ----------
    private void ResetUserPropertyDictionary()
    {
        if(_isResetUserPropertyDic)
        {
            _userPropertyDic = new SerializedDictionary<List<int>>();
            // A_Bグループ
            _userPropertyDic.Add(GetKeyString(UserSegmentKey.A_B), new List<int>{ 0, 1}); // 0:Aグループ, 1:Bグループ
            // 広告表示間隔
            // _userPropertyDic.Add(GetKeyString(UserSegmentKey.AdInterval), new List<int>{ 0, 1}); // 0:短い, 1:長い
            // // 移動アニメーションの有無
            // _userPropertyDic.Add(GetKeyString(UserSegmentKey.IsMove), new List<int>{ 0, 1}); // 0:無し, 1:有り

            _isResetUserPropertyDic = false;
        }
    }

    private string GetValueStringToBool(bool value)
    {
        if(value)
            return "有";
        return "無";
    }
}
