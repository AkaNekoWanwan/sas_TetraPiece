using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System;
using UnityEngine.Events;
using System.Linq;

/// <summary>
/// ステージ管理
/// </summary>
// ステージデータ
[System.Serializable]
public class LevelRandomParametor
{
    [SerializeField]
    private List<Vector2Int> _limitTimeRanges;
    [SerializeField]
    private List<Vector2Int> _stageTypeRanges;
    // [SerializeField]
    // private List<EvenOdd> _evenOddList;
    // [SerializeField]
    // private List<NumberType> _numberTypeList;
    [SerializeField]
    private List<SerializeList<Vector2Int>> _targetMatchNumRanges;
    [SerializeField]
    private List<SerializeList<Vector2Int>> _targetMergePointRanges;

    // プロパティは、呼び出されるたびにランダムな値を生成
    public int RandomLimitTime => GetRandomValueFromRanges(_limitTimeRanges);
    public int RandomStageType => GetRandomValueFromRanges(_stageTypeRanges);
    // public EvenOdd RandomEvenOdd => _evenOddList[UnityEngine.Random.Range(0, _evenOddList.Count)];
    // public NumberType RandomNumberType => _numberTypeList[UnityEngine.Random.Range(0, _numberTypeList.Count)];

    public List<int> RandomTargetMatchNumList
    {
        get
        {
            HashSet<int> result = new HashSet<int>();
            foreach (var rangeList in _targetMatchNumRanges)
            {
                if (rangeList.list.Count > 0)
                {
                    Vector2Int selectedRange = rangeList.list[UnityEngine.Random.Range(0, rangeList.list.Count)];
                    int num = UnityEngine.Random.Range(selectedRange.x, selectedRange.y + 1);
                    if( 0 < num)
                        result.Add(num);
                }
            }
            return result.OrderBy(x => x).ToList();
        }
    }

    public List<int> RandomTargetMergePointList
    {
        get
        {
            List<int> result = new List<int>();
            foreach (var rangeList in _targetMergePointRanges)
            {
                if (rangeList.list.Count > 0)
                {
                    Vector2Int selectedRange = rangeList.list[UnityEngine.Random.Range(0, rangeList.list.Count)];
                    int num = UnityEngine.Random.Range(selectedRange.x, selectedRange.y + 1);
                    if( 0 < num)
                        result.Add(num);
                }
            }
            return result;
        }
    }

    // コンストラクタ
    public LevelRandomParametor(
        string timeString, string stageTypeString, string evenOddString, string numberTypeString,
        string targetData)
    {
        _limitTimeRanges = ParseRanges(timeString);
        _stageTypeRanges = ParseRanges(stageTypeString);
        // _evenOddList = ParseEnums<EvenOdd>(evenOddString);
        // _numberTypeList = ParseEnums<NumberType>(numberTypeString);
        
        ParseTargetData(targetData, out _targetMatchNumRanges, out _targetMergePointRanges);
    }
    
    // 文字列を解析し、Vector2Intのリストに変換するヘルパーメソッド
    private List<Vector2Int> ParseRanges(string data)
    {
        List<Vector2Int> result = new List<Vector2Int>();
        string[] elements = data.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var element in elements)
        {
            string[] parts = element.Split(new[] { '~' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 && int.TryParse(parts[0], out int start) && int.TryParse(parts[1], out int end))
            {
                result.Add(new Vector2Int(start, end));
            }
            else if (int.TryParse(element, out int singleValue))
            {
                result.Add(new Vector2Int(singleValue, singleValue));
            }
            else
            {
                Debug.LogWarning($"範囲 '{element}' の形式が不正です。スキップします。");
            }
        }
        return result;
    }
    
    // ヘルパーメソッド: 範囲リストからランダムな値を取得
    private int GetRandomValueFromRanges(List<Vector2Int> ranges)
    {
        if (ranges == null || ranges.Count == 0) return 0;
        var selectedRange = ranges[UnityEngine.Random.Range(0, ranges.Count)];
        return UnityEngine.Random.Range(selectedRange.x, selectedRange.y + 1);
    }
    
    // 文字列からEnumリストに変換
    private List<T> ParseEnums<T>(string data) where T : struct, IConvertible
    {
        List<T> result = new List<T>();
        string[] elements = data.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string element in elements)
        {
            if (Enum.TryParse(element, true, out T enumValue))
            {
                result.Add(enumValue);
            }
            else
            {
                Debug.LogWarning($"'{element}' は型 {typeof(T)} に変換できません。スキップします。");
            }
        }
        return result;
    }

    // targetDataを解析
    private void ParseTargetData(string data, out List<SerializeList<Vector2Int>> targetMatchNumRanges, out List<SerializeList<Vector2Int>> targetMergePointRanges)
    {
        targetMatchNumRanges = new List<SerializeList<Vector2Int>>();
        targetMergePointRanges = new List<SerializeList<Vector2Int>>();

        string[] segments = data.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string segment in segments)
        {
            string[] parts = segment.Split(new[] { '-' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2)
            {
                Debug.LogWarning($"セグメント '{segment}' の形式が不正です。スキップします。");
                continue;
            }

            SerializeList<Vector2Int> matchRanges = new SerializeList<Vector2Int>();
            matchRanges.list = ParseSubRanges(parts[0]);
            targetMatchNumRanges.Add(matchRanges);
            
            SerializeList<Vector2Int> mergeRanges = new SerializeList<Vector2Int>();
            mergeRanges.list = ParseSubRanges(parts[1]);
            targetMergePointRanges.Add(mergeRanges);
        }
    }
    
    // `~`と`:`を処理するヘルパーメソッド
    private List<Vector2Int> ParseSubRanges(string data)
    {
        List<Vector2Int> result = new List<Vector2Int>();
        string[] elements = data.Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var element in elements)
        {
            string[] parts = element.Split(new[] { '~' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 && int.TryParse(parts[0], out int start) && int.TryParse(parts[1], out int end))
            {
                result.Add(new Vector2Int(start, end));
            }
            else if (int.TryParse(element, out int singleValue))
            {
                result.Add(new Vector2Int(singleValue, singleValue));
            }
            else
            {
                Debug.LogWarning($"サブ範囲 '{element}' の形式が不正です。スキップします。");
            }
        }
        return result;
    }
}
public class LevelParamMemory
{
    public int LimitTIme = default;
    public List<int> TargetMatchNumList = default;
    public List<int> TargetMergePointList = default;
    public int StageType = default;
    // public EvenOdd EvenOdd = default;
    // public NumberType NumberType = default;
    public List<int> BlockNumList = default;
}
public enum LoopType
{
    Loop,
    LastLoop,
}

public class LevelManager : MonoBehaviour, IInitializer
{
    // ---------- 定数宣言 ----------
    // ---------- ゲームオブジェクト参照変数宣言 ----------
    // ---------- プレハブ ----------
    [SerializeField, Tooltip("テストレベル")] private int _testLevel;
    [SerializeField, Tooltip("イニシャライザー")] private SerializeInterface<IInitializer> _iInitializer;
    [SerializeField, Tooltip("親")] Transform _parent = default;
    [SerializeField, Tooltip("ステージ")] List<SerializeList<StageManager>> _levelBundleList = default;
    [SerializeField, Tooltip("ステージ")] List<SerializeList<LevelRandomParametor>> _levelParamBundleList2 = default;
    [SerializeField, Tooltip("ステージ")] private LoopType _loopType = default;
    // ---------- プロパティ ----------
    [field: SerializeField] public InitializerBase InitializerBaseClass { get; set; }
    private StageManager _currentStage = null;
    private UnityEvent _onCliearCallback = default;
    public event UnityAction OnClear
    {
        add
        {
            if (_onCliearCallback == null)
                _onCliearCallback = new UnityEvent();
            _onCliearCallback.AddListener(value);
        }
        remove
        {
            _onCliearCallback.RemoveListener(value);
        }
    }
    private UnityEvent _onStageLoad = default;
    public event UnityAction OnStageLoad
    {
        add
        {
            if (_onStageLoad == null)
                _onStageLoad = new UnityEvent();
            _onStageLoad.AddListener(value);
        }
        remove
        {
            _onStageLoad.RemoveListener(value);
        }
    }
    private List<StageManager> _useGameStageList = default;
    private List<LevelRandomParametor> _useStageParamList = default;

    private LevelParamMemory _levelParamMemory = default;
    // ---------- クラス変数宣言 ----------
    // ---------- インスタンス変数宣言 ----------
    // ---------- Unity組込関数 ----------
    private void Awake()
    {
        InitializerBaseClass.Init(this, this);
        _iInitializer.Value.OnInitialize += InitializerBaseClass.Initialize;
    }
    // ---------- Public関数 ----------
    public void InitializeUnique()
    {
        // Debug.Log("レベルマネージャー初期化");
        ResetStageBundle();
        StartCoroutine(StageLoadLater());
    }

    public void ResetStageBundle()
    {
        // レベルバンドルリセット。どちらのステージ順を用いるかABで分ける
        _useGameStageList = GetEnableGameStageList();
        _useStageParamList = GetEnableLevelParamList2();
    }
    IEnumerator StageLoadLater()
    {
        // yield return new WaitForSeconds(1f); // フレーム待つ
        yield return null;
        StageLoad();
        _onStageLoad?.Invoke();
    }
    // ステージ生成
    public void StageLoad()
    {
        Transform parent = this.transform;
        if (parent != null)
        {
            parent = _parent;
            // if(GameConst.IS_CREATIVE)
            // {
            //     Vector3 setPos = parent.transform.position;
            //     setPos.z -= 3f;
            //     parent.transform.position = setPos;
            // }
        }

        int currentParamIndex = SaveDataManager.Level;
        // int currentParamIndex = 1;
#if UNITY_EDITOR
        if (-1 < _testLevel)
            currentParamIndex = _testLevel;
#endif
        if (_useStageParamList.Count <= currentParamIndex)
        {
            switch(_loopType)
            {
                case LoopType.Loop:
                default:
                    currentParamIndex %= _useStageParamList.Count;
                    break;
                case LoopType.LastLoop:
                    currentParamIndex = _useStageParamList.Count - 1;
                    break;
            }
        }
            StageManager currentStagePrefab = default;

        int stageType = _useStageParamList[currentParamIndex].RandomStageType - 1; // 0始まりにする

        Debug.Log($"StageLoad each List Size: {_useStageParamList.Count}, {currentParamIndex} : {_useGameStageList.Count}, level:{SaveDataManager.Level}, {PlayerPrefs.GetInt("Level")}, stageType: {stageType}");

        if (stageType < 0) stageType = 0;

        Debug.Log($"StageLoad Debug: {1}");
        // _useStageParamList[currentParamIndex].DebugLog();

        stageType %= _useGameStageList.Count;
        if (GameDataManager.IsRestart)
            stageType = GameDataManager.LevelParam.StageType;
        currentStagePrefab = _useGameStageList[stageType];
        Debug.Log($"StageLoad stageType: {stageType}");

        _currentStage = Instantiate(currentStagePrefab, Vector3.zero, Quaternion.identity);
        Transform stageTransform = _currentStage.transform;

        stageTransform.parent = parent;
        stageTransform.localScale = Vector3.one;
        stageTransform.localPosition = Vector3.zero;
        // _currentStage.OnClear += OnClearCallback;

        Debug.Log($"StageLoad Debug: {2}");

        if (!GameDataManager.IsRestart)
        {
            int limitTime = 0;
            int targetMatchNum = 0;
            int targetMergePoint = 0;
            List<int> targetMatchNumList = new List<int>();
            List<int> targetMergePointList = new List<int>();

            if (_useStageParamList.Count <= currentParamIndex)
                currentParamIndex = _useStageParamList.Count - 1;

            Debug.Log($"StageLoad Debug: {3}");

            limitTime = _useStageParamList[currentParamIndex].RandomLimitTime;
            Debug.Log($"StageLoad Debug: {4}");
            targetMatchNumList = _useStageParamList[currentParamIndex].RandomTargetMatchNumList;
            Debug.Log($"StageLoad Debug: {5}");
            targetMergePointList = _useStageParamList[currentParamIndex].RandomTargetMergePointList;
            Debug.Log($"StageLoad Debug: {6}");

            // Debug.Log("Level:" + currentParamIndex);
            // _currentStage.LimitTIme = limitTime;
            // _currentStage.TargetMatchNumList = targetMatchNumList;
            // _currentStage.TargetMergePointList = targetMergePointList;
            // _currentStage.EvenOdd = _useStageParamList[currentParamIndex].RandomEvenOdd;
            // _currentStage.NumberType = _useStageParamList[currentParamIndex].RandomNumberType;
            // _currentStage.StageType = _useStageParamList[currentParamIndex].RandomStageType;

            _levelParamMemory = new LevelParamMemory();
            // _levelParamMemory.LimitTIme = _currentStage.LimitTIme;
            // _levelParamMemory.TargetMatchNumList = new List<int>(_currentStage.TargetMatchNumList);
            // _levelParamMemory.TargetMergePointList = new List<int>(_currentStage.TargetMergePointList);
            // _levelParamMemory.EvenOdd = _currentStage.EvenOdd;
            // _levelParamMemory.NumberType = _currentStage.NumberType;
            _levelParamMemory.StageType = stageType;
            GameDataManager.SetLevelParamMemory(_levelParamMemory);
        }
        else
        {
            // _currentStage.LimitTIme = GameDataManager.LevelParam.LimitTIme;
            // _currentStage.TargetMatchNumList = new List<int>(GameDataManager.LevelParam.TargetMatchNumList);
            // _currentStage.TargetMergePointList = new List<int>(GameDataManager.LevelParam.TargetMergePointList);
            // _currentStage.EvenOdd = GameDataManager.LevelParam.EvenOdd;
            // _currentStage.NumberType = GameDataManager.LevelParam.NumberType;
            // _currentStage.StageType = stageType;
        }


        Debug.Log($"StageLoad Debug: {7}");
        // Debug.Log("StageType:" + _currentStage.StageType);

        // _currentStage.InitializerBaseClass.Initialize();

        GameDataManager.SetCurrentStage(_currentStage);
        GameDataManager.SetIsRestart(true);
    }
    // ステージ削除
    public void DeleteStage()
    {
        if (_currentStage != null)
        {
            // GameDataManager.InGameMainEvent.BeforeObjDestroy(_currentStage.transform, _currentStage.gameObject.tag);
            Destroy(_currentStage.gameObject);
        }
        // EffectManager.instance.StopAllEffect();
    }
    public List<StageManager> GetEnableGameStageList()
    {
        // int level_Bundle = PlayerPrefs.GetInt("Level_Bundle");
        int level_Bundle = 0;
        // if(GameConst.IS_CREATIVE)
        //     level_Bundle = 1;

        // レベルバンドル。どちらのステージ順を用いるかABで分ける
        if (level_Bundle < _levelBundleList.Count)
            return _levelBundleList[level_Bundle].list;

        // Debug.Log("レベルバンドルの値が異常かもです。：" + level_Bundle + ", " + _levelBundleList.Count);
        return _levelBundleList[0].list;
    }
    public List<LevelRandomParametor> GetEnableLevelParamList2()
    {
        int level_Bundle = PlayerPrefs.GetInt("Level_Bundle");

        // レベルバンドル。どちらのステージ順を用いるかABで分ける
        if (level_Bundle < _levelParamBundleList2.Count)
            return _levelParamBundleList2[level_Bundle].list;

        // Debug.Log("レベルバンドルの値が異常かもです。：" + level_Bundle + ", " + _levelParamBundleList2.Count);
        return _levelParamBundleList2[0].list;
    }

    // レベルパラメータ設定
    public void SetStagePatamList(int bundle, List<LevelRandomParametor> paramList)
    {
        _levelParamBundleList2[bundle].list = paramList;
    }
    // ---------- Private関数 ----------
    private void OnClearCallback()
    {
        _onCliearCallback?.Invoke();
    }
}
