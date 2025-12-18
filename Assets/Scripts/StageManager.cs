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

public class StageManager : MonoBehaviour
{
    public bool isStart;
    public bool isClear;
    public bool isDoClearGame = false;
    public bool isGameOver;
    public bool isPause;
    public GameObject[] stages;
    public GameObject[] dailyStages;
    public bool _isStageLoadFromScene = true;
    public StageInfo[] _stagePrefabs;
    public StageInfo[] _dailyStagePrefabs;
    public Transform stageParent;
    public int isNowStage;
    public bool isRestart;
    public bool isTest;
    public Image reloadButtonImage;
    public FirebaseManager firebaseManager; // FirebaseManagerの参照
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
        else if(GameConst.IsCreativeMode())
        {
            // isTest = true;
        }
    }
    void Start()
    {
        StartCoroutine(InitlializeColoutine());
    }

    private IEnumerator InitlializeColoutine()
    {
        yield return null;
        isClear = false;
        firebaseManager = GameObject.Find("FirebaseManager").GetComponent<FirebaseManager>();
        isNowStage = PlayerPrefs.GetInt("Stage", 0); // PlayerPrefsから現在のステージを取得

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
            GameDataManager.IsDebugView = _debugUIManager._view.activeSelf && Debug.isDebugBuild && !GameConst.IsCreativeMode() && !GameConst.IsScreenShotMode();
            GameDataManager.Initialize();
            GameDataManager.IsDebugView = _debugUIManager._view.activeSelf;
        }
        else
        {
            _debugUIManager._view.SetActive(GameDataManager.IsDebugView);
        }
        _creativeCanvas.SetActive(Debug.isDebugBuild && GameConst.IsCreativeMode());
        _defaultCanvas.SetActive(!Debug.isDebugBuild || !GameConst.IsCreativeMode());

        if(GameConst.IsCreativeMode())
        {
            _debugUIManager.onDebugViewToggled += (bool isActive)=>{
                _defaultCanvas.SetActive(isActive);
            };
            _defaultCanvas.SetActive(GameDataManager.IsDebugView);
        }

        if(GameDataManager.IsHome)
        yield return new WaitForSeconds(0.5f);

        // 🔸ステージに応じてアクティブ設定
        if(_isStageLoadFromScene)
        {
            if (!isTest)
            {
                for (int i = 0; i < stages.Length; i++)
                {
                    bool isActive = (i == isNowStage && _dailyStage == -1);
                    stages[i].SetActive(isActive);
                    if(isActive)
                        isHard = stages[i].GetComponent<StageInfo>().isHard;
                }
                for(int i = 0; i < dailyStages.Length; i++)
                {
                    dailyStages[i].SetActive(i + 1 == _dailyStage);
                }
            }
        }
        else
        {
            Debug.Log($"ステージロード：{isNowStage}, {PlayerPrefs.GetInt("Stage", 0)}, {PlayerPrefs.GetInt("totalLevel", 1)}");
            StageInfo stage = null;
            if(_dailyStage == -1)
                stage = Instantiate(_stagePrefabs[isNowStage]);
            else
                stage = Instantiate(_dailyStagePrefabs[_dailyStage - 1]);
            if( stageParent != null)
                stage.transform.parent = stageParent;
            stage.transform.localScale = Vector3.one;
            stage.transform.localPosition = Vector3.zero;
            stage.gameObject.SetActive(true);
            isHard = stage.isHard;
        }

        if(isHard)
            _imageLevelBack.color = new Color32(187, 3, 3, 255);
        
        //answerPosGrindの数をpicCountに代入
        picCount = FindAnyObjectByType<GridPieceListController>().gameObject.transform.childCount;


        if(_dailyStage == -1)
        {
            if(GameDataManager.InitMoveCount <= 0)
            {
                _MoveCount = Mathf.Min( picCount * 2, picCount + 12 );
                if(13 <= _MoveCount)
                {
                    _MoveCount += UnityEngine.Random.Range(-1, 2);
                }
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
                _MoveCount = Mathf.Min( picCount * 2, picCount + 12 );
                if(13 <= _MoveCount)
                {
                    _MoveCount += UnityEngine.Random.Range(-1, 2);
                }
                GameDataManager.DailyInitMoveCount = _MoveCount;
                Debug.Log($"_MoveCountセット：{picCount}->{_MoveCount}");
            }
            else
                _MoveCount = GameDataManager.DailyInitMoveCount;
        }

        // 🔸前回の経過時間を読み込み
        string key = GetElapsedTimeKey();
        pureElapsedTime = PlayerPrefs.GetFloat(key, 0f);

        firebaseManager.StageStart("");

        Debug.Log($"▶ ステージ {isNowStage} 開始。前回経過時間 {pureElapsedTime:F2} 秒から再開");

        // 🔸5秒ごとに経過時間を保存
        autoSaveRoutine = StartCoroutine(AutoSaveElapsedTime());

        if(!GameDataManager.IsHome)
            _hardEfffectManager.PlayHardAnimation(isHard);

        GameDataManager.IsHard = isHard;

        TryRequestReview();

        _clearNextButton.transform.localScale = Vector3.zero;
        _clearNextButton.onClick += OnClearNext;
    }

    private string GetElapsedTimeKey()
    {
        string ret = $"{ELAPSED_TIME_KEY}_{isNowStage}";
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
                Debug.Log($"💾 自動保存: ステージ{isNowStage} 経過時間 {pureElapsedTime:F1}秒");
            }
        }
    }

    void Update()
    {
        if ( !isClear)
        {
            pureElapsedTime += Time.deltaTime;
        }
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
                    // ClearGame(true);
                    clearBuffer = 0;
                    _clearNextButton.transform.DOScale(Vector3.one, 0.25f).SetEase(Ease.OutBack).SetLink(_clearNextButton.gameObject);
                }
            }
        }
    }

    // if (!isClear && !isGameOver)
    // {
    //     timeRemaining = 0f; // ゲームオーバー時だけ0に固定
    //     UpdateTimerText();  // 0:00 表示を反映
    //     GameOver();
    //     Debug.Log("⏰ タイムアップ！");
    // }

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
        _MoveCount--;
        if (_MoveCount == 0 && !isClear)
        {
            Sequence seq = DOTween.Sequence();
            seq.Append(_moveCountText.transform.parent.DOScale(Vector3.one * 1.05f, 0.3f).SetEase(Ease.OutCubic).SetLink(_moveCountText.gameObject));
            seq.Append(_moveCountText.transform.parent.DOScale(Vector3.one, 0.32f).SetEase(Ease.OutCubic).SetLink(_moveCountText.gameObject));
            seq.AppendCallback(()=>{ if(!isClear)RestartGame(); });
        }
    }
    public void RestartGame()
    {
        if (!isRestart && !isClear)
        {
            if (autoSaveRoutine != null)
                StopCoroutine(autoSaveRoutine);

            firebaseManager.StageRestart(stageName);

            string key = $"{ELAPSED_TIME_KEY}_{isNowStage}";
            PlayerPrefs.SetFloat(key, pureElapsedTime);
            PlayerPrefs.Save();

            // FadeManager.Instance.LoadScene(SceneManager.GetActiveScene().name, 0.2f);
            isRestart = true;
            
            float rotateZ = reloadButtonImage.transform.localEulerAngles.z - 360f;
            reloadButtonImage.transform.DORotate(new Vector3(0, 0, rotateZ), 0.5f, RotateMode.FastBeyond360);
            ClearGame(false);
        }
    }

    public void GameOver()
    {
        isGameOver = true;
        firebaseManager.StageFail(stageName); // Firebaseにステージ失敗を通知
        FadeManager.Instance.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name, 0.5f);
    }

    public void ClearGame(bool isGoHome)
    {
        GameDataManager.IsHome = isGoHome;
        if(isGoHome)
            GameDataManager.isPlayHomePieceAnimation = true; // ホームのステージ進行アニメーション実行
        // 広告再生の判定
        Debug.Log($"AdsCheck:Timer:{ AdsTimerManager.instance.ElapsedTime }, stage:{ PlayerPrefs.GetInt("totalLevel", 1) }");
        if( 60 <= AdsTimerManager.instance.ElapsedTime && 4 <= PlayerPrefs.GetInt("totalLevel", 1))
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
            firebaseManager.StageClear(stageName,pureElapsedTime); // Firebaseにステージクリアを通知
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

                PlayerPrefs.SetInt("Stage", isNowStage + 1); // 次のステージを保存
                PlayerPrefs.SetInt("totalLevel", currentTotalLevel + 1); // 全ステージ数を保存
                if (isNowStage + 1 >= GetStageLength())
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
            _clearViewManager.PosText();
         
            reloadButtonImage.DOFade(0f, 0.5f).SetEase(Ease.InOutSine);
            reloadButtonImage.transform.DOScale(Vector3.zero, 0.5f);
            isClear = true;
            // ★ カメラ移動アニメーション
            Camera cam = Camera.main;
            if (cam != null)
            {
                // Y座標 +2.5f に移動
                cam.DOOrthoSize(cam.orthographicSize+1.5f, 0.8f)
                    .SetEase(Ease.InOutSine).SetDelay(0.1f);
                cam.transform.DOMoveY(cam.transform.position.y - 1.5f, 0.7f)
                    .SetEase(Ease.InOutSine).OnComplete(() =>
                    {    // Orthographic Size を 17 に
            
                        // パーティクル再生
                        if (ps != null)
                        {
                            ps.Play();
                        }
                    });
            }
        }
    }

    public void NextStage()
    {
        Debug.Log($"デバッグ：ステージを進める:{!isClear}");
        if (isClear == false)
        {
            firebaseManager.StageClear(stageName,pureElapsedTime); // Firebaseにステージクリアを通知
            isClear = true;
            PlayerPrefs.SetInt("Stage", isNowStage + 1); // 次のステージを保存
            Debug.Log($"ステージリロード：{isNowStage}, {PlayerPrefs.GetInt("Stage", 0)}");
            PlayerPrefs.SetInt("totalLevel", PlayerPrefs.GetInt("totalLevel", 1) + 1); // 全ステージ数を保存
            if ( PlayerPrefs.GetInt("Stage") >= GetStageLength() )
            {
                PlayerPrefs.SetInt("Stage", 0); // 最後のステージをクリアしたら最初のステージに戻す
            }
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
            PlayerPrefs.SetInt("Stage", isNowStage - 1); // 次のステージを保存
            PlayerPrefs.SetInt("totalLevel", PlayerPrefs.GetInt("totalLevel", 1) - 1); // 全ステージ数を保存
            if (PlayerPrefs.GetInt("Stage") <0)
            {
                PlayerPrefs.SetInt("Stage", GetStageLength()-1); // 最後のステージをクリアしたら最初のステージに戻す
            }
            PlayerPrefs.Save();
            ReLoadScene(0.0f); 
            GameDataManager.InitMoveCount = -1;
            GameDataManager.DailyInitMoveCount = -1;
            // SceneManager.LoadScene (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }
    }

    private int GetStageLength()
    {
        if(_isStageLoadFromScene)
            return stages.Length;
        return _stagePrefabs.Length;
    }


    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            string key = $"{ELAPSED_TIME_KEY}_{isNowStage}";
            PlayerPrefs.SetFloat(key, pureElapsedTime);
            PlayerPrefs.Save();

            Debug.Log($"⏸ 中断。ステージ{isNowStage} 経過時間 {pureElapsedTime:F2}秒 保存");
            firebaseManager.Withdrwal(pureElapsedTime);
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
