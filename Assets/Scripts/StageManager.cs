using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class StageManager : MonoBehaviour
{
    public bool isStart;
    public bool isClear;
    public bool isDoClearGame = false;
    public bool isGameOver;
    public Image stagePic;
    public Image stagePicBG;

    public GameObject[] transparentObjects; // 透明化するオブジェクトの配列

    public float timeLimit; // タイムリミット（秒）
    public Text timerText;         // タイマー表示用 UI Text

    private float timeRemaining;
    private bool hasStartedTimer = false;
    public bool isPause;
    public GameObject[] stages;
    public int isNowStage;
    public bool isRestart;
    public bool isTest;
    public Image reloadButtonImage;
    public FirebaseManager firebaseManager; // FirebaseManagerの参照
    public string stageName;
    public int clearBuffer;
    public int startBuffer;
    public int picCount;
    public int goalPicCount;
    public ParticleSystem ps;
    public Text levelText;
    public HardEfffectManager _hardEfffectManager;
    public float pureElapsedTime; // 純粋な経過時間
    private Coroutine autoSaveRoutine;
    private const string ELAPSED_TIME_KEY = "StageElapsedTime";


    
    public void Awake()
    {
        Application.targetFrameRate = 60; // フレームレートを60に設定
    }
    void Start()
    {
        isClear = false;
        firebaseManager = GameObject.Find("FirebaseManager").GetComponent<FirebaseManager>();
        isNowStage = PlayerPrefs.GetInt("Stage", 0); // PlayerPrefsから現在のステージを取得
        levelText.text = "Level " + (PlayerPrefs.GetInt("totalLevel", 1)).ToString();

        bool isHard = false;

        // 🔸ステージに応じてアクティブ設定
        if (!isTest)
        {
            for (int i = 0; i < stages.Length; i++)
            {
                stages[i].SetActive(i == isNowStage);
                isHard = stages[i].GetComponent<StageInfo>().isHard;
            }
        }
        
        //answerPosGrindの数をpicCountに代入
        picCount = FindAnyObjectByType<GridPieceListController>().gameObject.transform.childCount;

        // 🔸前回の経過時間を読み込み
        string key = $"{ELAPSED_TIME_KEY}_{isNowStage}";
        pureElapsedTime = PlayerPrefs.GetFloat(key, 0f);

        firebaseManager.StageStart("");

        Debug.Log($"▶ ステージ {isNowStage} 開始。前回経過時間 {pureElapsedTime:F2} 秒から再開");

        // 🔸5秒ごとに経過時間を保存
        autoSaveRoutine = StartCoroutine(AutoSaveElapsedTime());

        _hardEfffectManager.PlayHardAnimation(isHard);
    }

    private IEnumerator AutoSaveElapsedTime()
    {
        string key = $"{ELAPSED_TIME_KEY}_{isNowStage}";

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
        if (isStart && !hasStartedTimer)
        {
            hasStartedTimer = true;
            // StartCoroutine(CountdownTimer());
        }
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
                if (clearBuffer == 180)
                {
                    isDoClearGame = true;
                    ClearGame();
                    clearBuffer = 0;
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
    public void RestartGame()
    {
        if (!isRestart)
        {
            if (autoSaveRoutine != null)
                StopCoroutine(autoSaveRoutine);

            firebaseManager.StageRestart(stageName);

            string key = $"{ELAPSED_TIME_KEY}_{isNowStage}";
            PlayerPrefs.SetFloat(key, pureElapsedTime);
            PlayerPrefs.Save();

            FadeManager.Instance.LoadScene(SceneManager.GetActiveScene().name, 0.5f);
            isRestart = true;
        }
    }

    public void GameOver()
    {
        isGameOver = true;
        firebaseManager.StageFail(stageName); // Firebaseにステージ失敗を通知
        FadeManager.Instance.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name, 0.5f);


    }
    public void ClearGame()
    {
        // 広告再生の判定
        Debug.Log($"AdsCheck:Timer:{ AdsTimerManager.instance.ElapsedTime }, stage:{ PlayerPrefs.GetInt("totalLevel", 1) }");
        if( 60 <= AdsTimerManager.instance.ElapsedTime && 3 <= PlayerPrefs.GetInt("totalLevel", 1))
        {
            AdsTimerManager.instance.ElapsedTime = 0f;
            AdsTimerManager.instance.IsCounter = false;
            AdsManager.instance.OnInterstitialHidden += OnInterstitialHidden;
            FadeManager.Instance.FadeIn(()=>{
                AdsManager.instance.ShowAd();
            }, 0.5f, true);
        }
        else
        {
            ReLoadScene(0.5f);
        }  
        // FadeManager.Instance.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name, 0.5f);
        
    }
    private void OnDisable() {
        AdsManager.instance.OnInterstitialHidden -= OnInterstitialHidden;
    }
    private void OnInterstitialHidden()
    {
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        SceneManager.LoadScene (sceneName); 
        FadeManager.Instance.FadeOut(0.5f); 
        AdsTimerManager.instance.IsCounter = true;
    }

    public void ClearTrigger()
    {
        if (isClear == false)
        {
            firebaseManager.StageClear(stageName,pureElapsedTime); // Firebaseにステージクリアを通知
            isClear = true;
            Debug.Log("🎉 ゲームクリア！:1");
            PlayerPrefs.SetInt("Stage", isNowStage + 1); // 次のステージを保存
            PlayerPrefs.SetInt("totalLevel", PlayerPrefs.GetInt("totalLevel", 1) + 1); // 全ステージ数を保存
            if (isNowStage + 1 >= stages.Length)
            {
                PlayerPrefs.SetInt("Stage", 0); // 最後のステージをクリアしたら最初のステージに戻す
            }
            PlayerPrefs.Save();
      
         
            reloadButtonImage.DOFade(0f, 0.5f).SetEase(Ease.InOutSine);
            isClear = true;
            // ★ カメラ移動アニメーション
            Camera cam = Camera.main;
            if (cam != null)
            {
                // Y座標 +2.5f に移動
                cam.DOOrthoSize(cam.orthographicSize-1.5f, 0.8f)
                    .SetEase(Ease.InOutSine).SetDelay(0.1f);
                cam.transform.DOMoveY(cam.transform.position.y + 2.5f, 0.7f)
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
        if (isClear == false)
        {
            firebaseManager.StageClear(stageName,pureElapsedTime); // Firebaseにステージクリアを通知
            isClear = true;
            Debug.Log("🎉 ゲームクリア！:2");
            PlayerPrefs.SetInt("Stage", isNowStage + 1); // 次のステージを保存
            PlayerPrefs.SetInt("totalLevel", PlayerPrefs.GetInt("totalLevel", 1) + 1); // 全ステージ数を保存
            if (PlayerPrefs.GetInt("Stage") >= stages.Length)
            {
                PlayerPrefs.SetInt("Stage", 0); // 最後のステージをクリアしたら最初のステージに戻す
            }
            PlayerPrefs.Save();
            ReLoadScene(0.0f); 
        }
    }

    private void ReLoadScene(float duration = 0.5f)
    {
        FadeManager.Instance.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name, duration);
    }

    public void BackGame()
    {
         if (isClear == false)
        {
            isClear = true;
            Debug.Log("🎉 ゲームクリア！:3");
            PlayerPrefs.SetInt("Stage", isNowStage - 1); // 次のステージを保存
            PlayerPrefs.SetInt("totalLevel", PlayerPrefs.GetInt("totalLevel", 1) - 1); // 全ステージ数を保存
            if (PlayerPrefs.GetInt("Stage") <0)
            {
                PlayerPrefs.SetInt("Stage", stages.Length-1); // 最後のステージをクリアしたら最初のステージに戻す
            }
            PlayerPrefs.Save();
            FadeManager.Instance.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name, 0.0f);
        }
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

}
