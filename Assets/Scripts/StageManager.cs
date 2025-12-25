using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Globalization;
using UnityEditor;
using System.Linq;
using AkanekoLib;
using System.Threading.Tasks;

public class StageManager : MonoBehaviour
{
    public bool isStart;
    public bool isClear;
    public bool isDoClearGame = false;
    public bool isGameOver;
    public bool isPause;
    public GameObject[] _stages;
    public GameObject[] _dailyStages;
    public bool _isStageLoadFromScene = true;
    public StageInfo[] _stagePrefabs;
    public StageInfo[] _dailyStagePrefabs;
    public Transform stageParent;
    public int _currentStage;
    public bool isRestart;
    public bool isTest;
    public Image reloadButtonImage;
    public FirebaseManager _firebaseManager; // FirebaseManagerの参照
    public string stageName;
    public int clearBuffer;
    public int startBuffer;
    public int picCount;
    public int _moveCount;
    public int goalPicCount;
    public ParticleSystem ps;
    public Text levelText;
    public Text _moveCountText;
    public Text _moveText;
    public Image _imageLevelBack;
    public HardEfffectManager _hardEfffectManager;
    public float pureElapsedTime; // 純粋な経過時間
    private Coroutine autoSaveRoutine;
    private const string ELAPSED_TIME_KEY = "StageElapsedTime";
    public ClearViewManager _clearViewManager = default;
    public GameObject _defaultCanvas = default;
    public DebugUIManager _debugUIManager = default;
    public GameObject _creativeCanvas = default;
    public CustomButton _clearNextButton = default;

    [Header("Addressable Stage Prefabs Settings")]
    public int _AddressableMaxStageCount = 504;
    public int _AddressableMaxDailyStageCount = 31;

    private const string STAGE_PREFABS_PATH = "Assets/Prefabs/Stages/";
    private const string DAILY_STAGE_PREFABS_PATH = "Assets/Prefabs/DailyStages/";

    private int _dailyStage = -1;
    private int _MoveCount {get=>_moveCount;
        set
        {
            _moveCount = value;
            if(_moveCountText != null)
            {
                _moveCountText.text = "" + value;
                if(_moveCount <= 2)
                {
                    _moveCountText.color = new Color32( 200, 10, 10, 255 );
                    _moveText.color = new Color32( 200, 10, 10, 255 );
                }
            }
        }}

#if UNITY_EDITOR
    private void OnValidate()
    {
        return;
        // ゲーム実行中は実行しない
        if (EditorApplication.isPlaying) 
            return;

        if(_isStageLoadFromScene)
        {
            _stagePrefabs = new StageInfo[0];
            _dailyStagePrefabs = new StageInfo[0];
            return;
        }

        SetPrefabs(STAGE_PREFABS_PATH, ref _stagePrefabs);
        SetPrefabs(DAILY_STAGE_PREFABS_PATH, ref _dailyStagePrefabs);
    }

    private void SetPrefabs(string path, ref StageInfo[] prefabArray)
    {

        // 一時的に結果を格納するためのList
        List<StageInfo> loadedPrefabs = new List<StageInfo>();

        // 1. 指定されたフォルダ内の全アセットのGUIDを取得
        // string[] guids = AssetDatabase.FindAssets("t:StageInfo", new[] { path });
        string[] guids = AssetDatabase.FindAssets("t:GameObject", new[] { path });
        
        // 2. GUIDをパスに変換し、プレハブとしてロード
        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);

            // フォルダ内のアセットがプレハブであることを確認してロード
            StageInfo prefab = AssetDatabase.LoadAssetAtPath<StageInfo>(assetPath);
            
            // PrefabUtility.GetPrefabAssetType で通常のプレハブか確認
            if (prefab != null && PrefabUtility.GetPrefabAssetType(prefab) == PrefabAssetType.Regular)
            {
                loadedPrefabs.Add(prefab);
            }
        }
        
        // （オプション）リストを名前順などでソート
        // OrderByを使用しない場合はこの行は不要
        loadedPrefabs = loadedPrefabs.OrderBy(p => p.name).ToList();

        // 🟨 変更点2: Listを配列に変換してpublic変数に格納
        prefabArray = loadedPrefabs.ToArray();

        // リストに更新があったことをエディターに通知し、Inspectorを再描画
        EditorUtility.SetDirty(this);
    }
#endif


    public void Awake()
    {
        Application.targetFrameRate = 60; // フレームレートを60に設定
        if(!Debug.isDebugBuild)
        {
            isTest = false;
        }
        else if(GameDataManager.IsCreativeMode)
        {
            // isTest = true;
        }
    }
    void Start()
    {
        // PlayerPrefs.SetInt("totalLevel", 306); // デバッグ用に総レベル数を306に設定
        // PlayerPrefs.SetInt("Stage", 305); // デバッグ用に総レベル数を306に設定
        InitlializeColoutine();
    }

    private async Task InitlializeColoutine()
    {
        reloadButtonImage.gameObject.SetActive(!GameConst.IsScreenShotMode());

        isClear = false;
        _firebaseManager = GameObject.Find("FirebaseManager").GetComponent<FirebaseManager>();
        // _currentStage = PlayerPrefs.GetInt("Stage", 0); // PlayerPrefsから現在のステージを取得
        _currentStage = GetStage(PlayerPrefs.GetInt("totalLevel", 1));

        _dailyStage = PlayerPrefs.GetInt("DailyStage", -1);
        if(_dailyStage == -1)
            levelText.text = "Level " + (PlayerPrefs.GetInt("totalLevel", 1)).ToString();
        else
        {
            // 日付テキストを設定　例：June 5
            levelText.text = System.DateTime.Now.ToString("MMM", new CultureInfo("en-US")) + " " + _dailyStage.ToString();
        }

        bool isHard = false;
        
        if(!GameDataManager.IsInit)
        {
            GameDataManager.IsDebugView = _debugUIManager._view.activeSelf && Debug.isDebugBuild && !GameDataManager.IsCreativeMode && !GameConst.IsScreenShotMode();
            GameDataManager.Initialize();
            GameDataManager.IsDebugView = _debugUIManager._view.activeSelf;
        }
        else
        {
            _debugUIManager._view.SetActive(GameDataManager.IsDebugView);
        }
        // _creativeCanvas.SetActive(Debug.isDebugBuild && GameDataManager.IsCreativeMode);
        _creativeCanvas.SetActive(GameDataManager.IsCreativeMode);
        _defaultCanvas.SetActive(!Debug.isDebugBuild || !GameDataManager.IsCreativeMode);

        if(GameDataManager.IsCreativeMode)
        {
            _debugUIManager.onDebugViewToggled += (bool isActive)=>{
                _defaultCanvas.SetActive(isActive);
            };
            _defaultCanvas.SetActive(GameDataManager.IsDebugView);
        }

        if(GameDataManager.IsHome)
        await Task.Yield();

        // 🔸ステージに応じてアクティブ設定
        if(_isStageLoadFromScene)
        {
            if (!isTest)
            {
                for (int i = 0; i < _stages.Length; i++)
                {
                    bool isActive = (i == _currentStage && _dailyStage == -1);
                    _stages[i].SetActive(isActive);
                    if(isActive)
                        isHard = _stages[i].GetComponent<StageInfo>().isHard;
                }
                for(int i = 0; i < _dailyStages.Length; i++)
                {
                    _dailyStages[i].SetActive(i + 1 == _dailyStage);
                }
            }
        }
        else if((_stagePrefabs.Length > 0 && _dailyStage == -1) 
        || (_dailyStagePrefabs.Length > 0 && _dailyStage != -1))
        {
            Debug.Log($"ステージロード：{_currentStage}, {PlayerPrefs.GetInt("Stage", 0)}, {PlayerPrefs.GetInt("totalLevel", 1)}");
            StageInfo stage = null;
            if(_dailyStage == -1)
                stage = Instantiate(_stagePrefabs[_currentStage]);
            else
                stage = Instantiate(_dailyStagePrefabs[_dailyStage - 1]);
            if( stageParent != null)
                stage.transform.parent = stageParent;
            stage.transform.localScale = Vector3.one;
            stage.transform.localPosition = Vector3.zero;
            stage.gameObject.SetActive(true);
            isHard = stage.isHard;
        }
        // Addressableからステージを読み込む場合
        else if(!_isStageLoadFromScene)
        {
            Debug.Log($"Addressableステージロード：{_currentStage}, {PlayerPrefs.GetInt("Stage", 0)}, {PlayerPrefs.GetInt("totalLevel", 1)}");
            StageInfo stage = null;
            if(_dailyStage == -1)
                stage = await AddressableManager.InstantiateStageAsync(_currentStage, _AddressableMaxStageCount, STAGE_PREFABS_PATH);
            else
                stage = await AddressableManager.InstantiateStageAsync(_dailyStage, _AddressableMaxDailyStageCount, DAILY_STAGE_PREFABS_PATH, true);
            if( stageParent != null)
                stage.transform.parent = stageParent;
            stage.transform.localScale = Vector3.one;
            stage.transform.localPosition = Vector3.zero;
            stage.gameObject.SetActive(true);
            isHard = stage.isHard;
        }
        else
        {
            
        }

        if(isHard)
            _imageLevelBack.color = new Color32(187, 3, 3, 255);
        
        //answerPosGrindの数をpicCountに代入
        picCount = FindAnyObjectByType<GridPieceListController>().gameObject.transform.childCount;


        if(_dailyStage < 0)
        {
            if(GameDataManager.InitMoveCount <= 0)
            {
                _MoveCount = CreateMoveCount(picCount, AddCount(PlayerPrefs.GetInt("totalLevel", 1)));
                GameDataManager.InitMoveCount = _MoveCount;
                Debug.Log($"_MoveCountセット：{picCount}->{_MoveCount}");
            }
            else
                _MoveCount = GameDataManager.InitMoveCount;
        }
        else
        {
            if(GameDataManager.DailyInitMoveCount <= 0)
            {
                _MoveCount = CreateMoveCount(picCount, 0);
                GameDataManager.DailyInitMoveCount = _MoveCount;
                Debug.Log($"_MoveCountセット：{picCount}->{_MoveCount}");
            }
            else
                _MoveCount = GameDataManager.DailyInitMoveCount;
        }

        // 🔸前回の経過時間を読み込み
        string key = GetElapsedTimeKey();
        pureElapsedTime = PlayerPrefs.GetFloat(key, 0f);

        Debug.Log($"▶ ステージ {_currentStage} 開始。前回経過時間 {pureElapsedTime:F2} 秒から再開");

        // 🔸5秒ごとに経過時間を保存
        autoSaveRoutine = StartCoroutine(AutoSaveElapsedTime());

        GameDataManager.IsStageStarted = false;
        if(!GameDataManager.IsHome)
        {
            _hardEfffectManager.PlayHardAnimation(isHard);
            FirebaseManager.instance.StageStart();
            GameDataManager.IsStageStarted = true;
        }

        GameDataManager.IsHard = isHard;

        TryRequestReview();

        _clearNextButton.transform.localScale = Vector3.zero;
        _clearNextButton.onClick += OnClearNext;

        await Task.Yield();
        // 初回ステージならステージの読み込みを待ってからフェードアウト
        if(PlayerPrefs.GetInt("totalLevel", 1) == 1 || GameDataManager.IsCreativeMode)
        {
            FadeManager.Instance.FadeOut(0.5f);
            if(Guidance.Instance != null && GameDataManager.IsCreativeMode == false)
                Guidance.Instance.ShowGuidance();   // ガイダンスの表示
            FirebaseManager.instance.StageStart();
            GameDataManager.IsStageStarted = true;
        }
    }

    private int CreateMoveCount(int picCount, int addCount)
    {
        int moveCount = Mathf.Min( picCount * 2, picCount + 16 );
        moveCount += UnityEngine.Random.Range(0, 2) + addCount;
        return moveCount;
    }

    private int AddCount(int totalLevel)
    {
        if(totalLevel == 1)
            return 4;
        if(totalLevel == 2)
            return 6;
        if(totalLevel == 3)
            return 10;
        if(totalLevel == 4)
            return 3;
        if(totalLevel == 5)
            return 6;

        if(totalLevel == 6)
            return 11;
        if(totalLevel == 7)
            return 5;
        if(totalLevel == 8)
            return 6;
        if(totalLevel == 9)
            return 10;
        if(totalLevel == 10)
            return 5;

        if(totalLevel == 11)
            return 5;
        if(totalLevel == 12)
            return 11;
        if(totalLevel == 13)
            return 5;
        if(totalLevel == 14)
            return 5;
        if(totalLevel == 15)
            return 11;

        if(totalLevel == 16)
            return 5;
        if(totalLevel == 17)
            return 5;
        if(totalLevel == 18)
            return 10;
        if(totalLevel == 19)
            return 4;
        if(totalLevel == 20)
            return 6;

        if(totalLevel == 21)
            return 8;
        if(totalLevel == 22)
            return 4;
        if(totalLevel == 23)
            return 5;
        if(totalLevel == 24)
            return 9;
        if(totalLevel == 25)
            return 3;

        if(totalLevel == 26)
            return 5;
        if(totalLevel == 27)
            return 8;
        if(totalLevel == 28)
            return 3;
        if(totalLevel == 29)
            return 3;
        if(totalLevel == 30)    
            return 3;

        if(totalLevel == 31)
            return 2;
        if(totalLevel == 32)
            return 3;
        if(totalLevel == 33)
            return 4;
        if(totalLevel == 34)
            return 2;
        if(totalLevel == 35)
            return 3;
        if(totalLevel == 36)
            return 4;

        return 0;
    }

    private string GetElapsedTimeKey()
    {
        string ret = $"{ELAPSED_TIME_KEY}_{_currentStage}";
        if(_dailyStage != -1)
        {
            ret = $"{ELAPSED_TIME_KEY}_Daily_{_dailyStage}";
        }
        return ret;
    }

    private IEnumerator AutoSaveElapsedTime()
    {
        string key = GetElapsedTimeKey();

        while (true)
        {
            yield return new WaitForSeconds(5f);

            if (!isClear) // クリア中は保存しない
            {
                PlayerPrefs.SetFloat(key, pureElapsedTime);
                PlayerPrefs.Save();
                Debug.Log($"💾 自動保存: ステージ{_currentStage} 経過時間 {pureElapsedTime:F1}秒");
            }
        }
    }

    void Update()
    {
        if ( !isClear)
        {
            pureElapsedTime += Time.deltaTime;
        }
        GameDataManager.UpdateTouchInfo();
    }
    public void FixedUpdate()
    {
        if (isClear)
        {
            if(!isDoClearGame)
            {
                clearBuffer++;
                if (clearBuffer == 60)
                {
                    isDoClearGame = true;
                    if(GameDataManager.IsCreativeMode)
                        ClearGame(true);
                    clearBuffer = 0;
                    if(!GameDataManager.IsCreativeMode)
                        _clearNextButton.transform.DOScale(Vector3.one, 0.25f).SetEase(Ease.OutBack).SetLink(_clearNextButton.gameObject);
                }
            }
        }
    }

    public void CountDownPic()
    {
        picCount--;
        if (picCount == 0 && !isClear)
        {
            ClearTrigger();
        }
    }
    public void CountDownMove()
    {
        if(_moveCountText == null)
            return;
        if(!GameDataManager.IsCreativeMode)
            _MoveCount--;
        if (_MoveCount <= 0 && !isClear && !isGameOver)
        {
            isGameOver = true;
            Sequence seq = DOTween.Sequence();
            seq.SetLink(this.gameObject);
            seq.Append(_moveCountText.transform.parent.DOScale(Vector3.one * 1.05f, 0.3f).SetEase(Ease.OutCubic).SetLink(_moveCountText.gameObject));
            seq.Append(_moveCountText.transform.parent.DOScale(Vector3.one, 0.32f).SetEase(Ease.OutCubic).SetLink(_moveCountText.gameObject));
            seq.AppendCallback(()=>{ if(!isClear)RestartGame(); });
            _firebaseManager.StageFailure(_MoveCount); // Firebaseにステージ失敗を通知
        }
    }
    public void RestartGame()
    {
        if (!isRestart && !isClear)
        {
            if (autoSaveRoutine != null)
                StopCoroutine(autoSaveRoutine);

            if(!isGameOver)
                _firebaseManager.StageUndo(_MoveCount);

            string key = $"{ELAPSED_TIME_KEY}_{_currentStage}";
            PlayerPrefs.SetFloat(key, pureElapsedTime);
            PlayerPrefs.Save();

            // FadeManager.Instance.LoadScene(SceneManager.GetActiveScene().name, 0.2f);
            isRestart = true;
            
            float rotateZ = reloadButtonImage.transform.localEulerAngles.z - 360f;
            reloadButtonImage.transform.DORotate(new Vector3(0, 0, rotateZ), 0.5f, RotateMode.FastBeyond360).SetLink(reloadButtonImage.gameObject);
            ClearGame(false);
        }
    }

    public void ClearGame(bool isGoHome)
    {
        GameDataManager.IsHome = isGoHome;
        if(isGoHome)
            GameDataManager.isPlayHomePieceAnimation = true; // ホームのステージ進行アニメーション実行
        // 広告再生の判定
        Debug.Log($"AdsCheck:Timer:{ AdsTimerManager.instance.ElapsedTime }, stage:{ PlayerPrefs.GetInt("totalLevel", 1) }");
        if( 60 <= AdsTimerManager.instance.ElapsedTime && 4 <= PlayerPrefs.GetInt("totalLevel", 1) && !GameDataManager.IsCreativeMode)
        {
            AdsTimerManager.instance.ElapsedTime = 0f;
            AdsTimerManager.instance.IsCounter = false;
            AdsManager.instance.OnInterstitialHidden += OnInterstitialHidden;
            FadeManager.Instance.FadeIn(()=>{
                string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                SceneManager.LoadScene (sceneName); 
                AdsManager.instance.ShowAd();
            }, 0.5f, false);
        }
        else
        {
            ReLoadScene(0.25f);
            // FadeManager.Instance.FadeIn(()=>{
            //     OnInterstitialHidden();
            // }, 0.5f, false);
        }  
        // FadeManager.Instance.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name, 0.5f);
        
    }

    private void OnClearNext()
    {
        ClearGame(true);
    }

    private void OnDisable() {
        AdsManager.instance.OnInterstitialHidden -= OnInterstitialHidden;
    }
    private void OnInterstitialHidden()
    {
        FadeManager.Instance.FadeOut(0.25f); 
        AdsTimerManager.instance.IsCounter = true;
    }

    public void ClearTrigger()
    {
        if (isClear == false)
        {
            GameDataManager.InitMoveCount = -1;
            GameDataManager.DailyInitMoveCount = -1;
            _firebaseManager.StageClear(_MoveCount, pureElapsedTime); // Firebaseにステージクリアを通知
            isClear = true;
            AudioManager.Instance.PlayClearSound();
            Debug.Log("🎉 ゲームクリア！:1");
            
            int _dailyStage = PlayerPrefs.GetInt("DailyStage", -1);

            if(_dailyStage == -1)
            {
                int currentTotalLevel = PlayerPrefs.GetInt("totalLevel", 1);

                // 31ステージをクリアしたらレビュー促進ポップアップを表示
                if(currentTotalLevel == 31)
                {
                    PlayerPrefs.SetInt("RequestReview", 1);
                }

                PlayerPrefs.SetInt("Stage", _currentStage + 1); // 次のステージを保存
                PlayerPrefs.SetInt("totalLevel", currentTotalLevel + 1); // 全ステージ数を保存
                if (_currentStage + 1 >= GetStageLength())
                {
                    // 25ステージ目からループさせる
                    PlayerPrefs.SetInt("Stage", 25); // 最後のステージをクリアしたら最初のステージに戻す
                }
            }
            else
            {
                // デイリーステージクリア時の処理
                PlayerPrefs.SetInt("DailyStage", -1); // デイリーステージモードを解除
                int clearSaveData = PlayerPrefs.GetInt("DailyClearData", 0);
                clearSaveData |= (1 << (_dailyStage - 1));
                PlayerPrefs.SetInt("DailyClearData", clearSaveData);
                PlayerPrefs.SetInt("beforeDailyClear", 1);
            }
            PlayerPrefs.Save();
            if(!GameDataManager.IsCreativeMode)
                _clearViewManager.PosText();
         
            reloadButtonImage.DOFade(0f, 0.5f).SetEase(Ease.InOutSine).SetLink(reloadButtonImage.gameObject);
            reloadButtonImage.transform.DOScale(Vector3.zero, 0.5f).SetLink(reloadButtonImage.gameObject);
            isClear = true;
            // ★ カメラ移動アニメーション
            Camera cam = Camera.main;
            if (cam != null )
            {
                if(!GameDataManager.IsCreativeMode)
                {
                    // Y座標 +2.5f に移動
                    cam.DOOrthoSize(cam.orthographicSize+1.5f, 0.8f)
                        .SetEase(Ease.InOutSine).SetDelay(0.1f).SetLink(cam.gameObject);
                    cam.transform.DOMoveY(cam.transform.position.y - 1.5f, 0.7f)
                        .SetEase(Ease.InOutSine).OnComplete(() =>
                        {    // Orthographic Size を 17 に
                
                            // パーティクル再生
                            if (ps != null)
                            {
                                ps.Play();
                            }
                        }).SetLink(cam.gameObject);
                }
                else
                {
                    cam.DOOrthoSize(cam.orthographicSize-1.5f, 0.8f)
                        .SetEase(Ease.InOutSine).SetDelay(0.1f).SetLink(cam.gameObject);
                    cam.transform.DOMoveY(cam.transform.position.y + 1.5f, 0.7f)
                        .SetEase(Ease.InOutSine).SetLink(cam.gameObject);
                }
            }
        }
    }

    public void NextStage()
    {
        Debug.Log($"デバッグ：ステージを進める:{!isClear}");
        if (isClear == false)
        {
            isClear = true;
            Debug.Log($"ステージリロード：{_currentStage}, {PlayerPrefs.GetInt("Stage", 0)}");
            int totalLevel = PlayerPrefs.GetInt("totalLevel", 1);
            totalLevel++;
            int setStage = GetStage(totalLevel);
            PlayerPrefs.SetInt("totalLevel", totalLevel); // 全ステージ数を保存
            PlayerPrefs.SetInt("Stage", setStage);
            PlayerPrefs.Save();
            ReLoadScene(0.0f); 
            GameDataManager.InitMoveCount = -1;
            GameDataManager.DailyInitMoveCount = -1;
            // SceneManager.LoadScene (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }
    }

    private void ReLoadScene(float duration = 0.25f)
    {
        FadeManager.Instance.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name, duration);
    }

    public void BackGame()
    {
        Debug.Log($"デバッグ：ステージ戻す:{!isClear}");
        if (isClear == false)
        {
            isClear = true;

            int totalLevel = PlayerPrefs.GetInt("totalLevel", 1);
            totalLevel--;
            if(totalLevel < 1)
                totalLevel = 1;
            PlayerPrefs.SetInt("totalLevel", totalLevel); // 全ステージ数を保存
            PlayerPrefs.SetInt("Stage", GetStage(totalLevel)); // 最後のステージをクリアしたら最初のステージに戻す
            PlayerPrefs.Save();
            ReLoadScene(0.0f); 
            GameDataManager.InitMoveCount = -1;
            GameDataManager.DailyInitMoveCount = -1;
            // SceneManager.LoadScene (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }
    }

    private int GetStage(int totalLevel)
    {
        int stage = totalLevel - 1;
        if( GetStageLength() <= stage)
            stage  = (stage - GetStageLength()) % (GetStageLength() - 25) + 25; // 25〜504の範囲に変換
        return stage;
    }

    private int GetStageLength()
    {
        if(!_isStageLoadFromScene && _stagePrefabs.Length <= 0)
        {
            return _AddressableMaxStageCount;
        }

        if(_isStageLoadFromScene)
            return _stages.Length;
        return _stagePrefabs.Length;
    }


    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            string key = $"{ELAPSED_TIME_KEY}_{_currentStage}";
            PlayerPrefs.SetFloat(key, pureElapsedTime);
            PlayerPrefs.Save();

            Debug.Log($"⏸ 中断。ステージ{_currentStage} 経過時間 {pureElapsedTime:F2}秒 保存");
            _firebaseManager.Withdrwal(pureElapsedTime);
        }
        else
        {
            Debug.Log("▶ アプリが再開されました。計測再開します。");
        }
    }

    // レビュー促進ポップアップ表示を試行
    private void TryRequestReview()
    {
        if(this != null && this.gameObject.activeSelf)
            // if(30 <= PlayerPrefs.GetInt("Stage", 0))
            if(PlayerPrefs.GetInt("RequestReview", 0) == 1)
            {
                StartCoroutine(InAppReviewManager.RequestReview());
                PlayerPrefs.SetInt("RequestReview", 0);
            }
    }

}
