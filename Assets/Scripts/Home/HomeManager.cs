using UnityEngine;
using AkanekoLib;
using DG.Tweening;  
using UnityEngine.UI;  
using UnityEngine.EventSystems;  
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using System.Globalization;
using UnityEditor;
using System.Linq;
using Cysharp.Threading.Tasks; // UniTaskを利用するために必要
using UnityEngine.AddressableAssets; // Addressables APIを利用するために必要
using System;
using System.Threading;
using UnityEngine.ResourceManagement.AsyncOperations; // 必要に応じて追

public class HomeManager : MonoBehaviour
{
    public static HomeManager Instance;
    public CanvasGroup _canvanGroup = default;
    public CustomButton _playButton = default;
    // public CustomButton _backButton = default;
    public GameObject _backGameObject = default;
    public HomePanelsManager[] _homePuzzlePrefabs;
    public Transform _homeParent;
    public HardEfffectManager _hardEfffectManager;
    public ParticleSystem _particle;
    private CancellationTokenSource _cts;

    // private const string HOME_STAGE_PREFABS_PATH = "Assets/Prefabs/HomePuzzles/";//HomePanels
    private const string HOME_STAGE_PREFABS_PATH = "Assets/Prefabs/HomePuzzles/HomePanels{0}.prefab";
    private const string ASSET_PATH_FORMAT = "Assets/Prefabs/HomePuzzles/HomePanels{0:D3}.prefab"; // ★★★ パスのフォーマットを修正 ★★★

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private async void Start() // ★★★ Coroutineからasync voidに変更 ★★★
    {
        _cts = new CancellationTokenSource();
        if(!GameDataManager.IsHome)
        {
            this.gameObject.SetActive(false);
            return;
        }
        
        // 処理を非同期で実行
        await InitlializeAsync();
    }

    private void OnDestroy()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }

    // StageManager.cs に追加
    /// <summary>
    /// ステージインデックス（0始まり）からAddressableのアドレス文字列を生成する
    /// </summary>
    /// <param name="index">ホームパズルのプレハブ配列のインデックス（0, 1, 2...）</param>
    /// <param name="prefabLength">ホームパズルの種類（配列の長さ）</param>
    /// <returns>Addressableのアドレス文字列</returns>
    private string GetHomePanelAddress(int index, int prefabLength)
    {
        // X = (index % prefabLength) + 1
        int panelNumber = (index % prefabLength) + 1;
        
        // Assets/Prefabs/HomePuzzles/HomePanels{0埋め3桁のX}.prefab
        return string.Format(ASSET_PATH_FORMAT, panelNumber);
    }

    // ★★★ InitlializeColoutine() を置き換え ★★★
    private async UniTask InitlializeAsync()
    {
        await UniTask.Yield(); // Coroutineの yield return null に相当

        // Addressablesの初期化が済んでいない場合はここで待機
        await AddressableUtil.InitAsync(); 
        
        _playButton.onClick += OnPlayButton;
        
        int totalLevel = PlayerPrefs.GetInt("totalLevel", 1);
        int nowBoardIndex = (totalLevel - 1) / 30;

        // ボード完成→次のボードへ移動アニメーションを流すか
        bool isBoardChangeAnimation = false;
        int beforeBoardIndex = 0;
        // クリアして戻ってきたか
        if(GameDataManager.isPlayHomePieceAnimation)
        {
            beforeBoardIndex = (totalLevel - 2) / 30;
            if( 0 <= beforeBoardIndex && beforeBoardIndex != nowBoardIndex)
                isBoardChangeAnimation = true;
        }

        Debug.Log($"アニメーションチェック: isAnim:{GameDataManager.isPlayHomePieceAnimation}, totalLevel:{totalLevel}, nowBoardIndex:{nowBoardIndex}, beforeBoardIndex:{beforeBoardIndex}, isBoardChangeAnimation:{isBoardChangeAnimation}");

        HomePanelsManager homePanelsManager = null;
        int prefabLength = 5; // ★★★ HomePanelsの総数を暫定的に定義。Addressable移行後は手動またはAPIで取得する必要があります。 ★★★
                              // ※ 既存コードでは _homePuzzlePrefabs.Length を使っていたため、手動で最大数を把握するか、
                              //    AddressableUtil.GetResourceCountByLabelAsync のようなメソッドで取得する必要があります。
                              //    ここでは便宜上 5 とします。
        
        // ボード切り替え演出なし
        if(!isBoardChangeAnimation)
        {
            string address = GetHomePanelAddress(nowBoardIndex, prefabLength);
            
            // 🔽 Addressable 非同期読み込み 🔽
            GameObject loadedPrefab = await AddressableUtil.LoadAssetAsync<GameObject>(address, _cts.Token);
            
            if (loadedPrefab != null)
            {
                // インスタンス化
                homePanelsManager = Instantiate(loadedPrefab).GetComponent<HomePanelsManager>();
            }
            
            if (homePanelsManager != null)
            {
                homePanelsManager.transform.parent = _homeParent;
                homePanelsManager.transform.localScale = Vector3.one;
                homePanelsManager.transform.localPosition = Vector3.zero;
                homePanelsManager.StartIndex = nowBoardIndex * 30;
                homePanelsManager.Initialize();
            }
        }
        else
        {
            // 前のボードを出す
            string address1 = GetHomePanelAddress(beforeBoardIndex, prefabLength);
            
            // 🔽 Addressable 非同期読み込み 1 🔽
            GameObject loadedPrefab1 = await AddressableUtil.LoadAssetAsync<GameObject>(address1, _cts.Token);
            
            if (loadedPrefab1 != null)
            {
                homePanelsManager = Instantiate(loadedPrefab1).GetComponent<HomePanelsManager>();
            }
            
            if (homePanelsManager != null)
            {
                homePanelsManager.transform.parent = _homeParent;
                homePanelsManager.transform.localScale = Vector3.one;
                homePanelsManager.transform.localPosition = Vector3.zero;
                homePanelsManager.StartIndex = beforeBoardIndex * 30;
                homePanelsManager.Initialize();
            }

            // 次のボードも用意しておく
            string address2 = GetHomePanelAddress(nowBoardIndex, prefabLength);
            
            // 🔽 Addressable 非同期読み込み 2 🔽
            GameObject loadedPrefab2 = await AddressableUtil.LoadAssetAsync<GameObject>(address2, _cts.Token);
            
            HomePanelsManager homePanelsManager2 = null;
            if (loadedPrefab2 != null)
            {
                homePanelsManager2 = Instantiate(loadedPrefab2).GetComponent<HomePanelsManager>();
            }
            
            if (homePanelsManager2 != null)
            {
                homePanelsManager2.transform.parent = _homeParent;
                homePanelsManager2.transform.localScale = Vector3.one * 0.5f;
                homePanelsManager2.transform.localPosition = new Vector3(0f, -2000f, 0f);
                homePanelsManager2.StartIndex = nowBoardIndex * 30;
                homePanelsManager2.Initialize();
            }

            // DOTween アニメーション (非同期読み込み後に実行)
            Sequence seq = DOTween.Sequence();
            seq.AppendInterval(1.3f);
            seq.AppendCallback(()=>{ _particle.Play(); AudioManager.Instance.PlayClearSound(); homePanelsManager.PlayClearAnimation();});
            seq.Append(homePanelsManager.transform.DOScale(Vector3.one * 1.05f, 1f).SetEase(Ease.Linear).SetLink(homePanelsManager.gameObject));
            seq.AppendInterval(2f);
            seq.AppendCallback(()=>{ _particle.Play(); AudioManager.Instance.PlayCardFlipSound();});
            seq.Append(homePanelsManager.transform.DOLocalMoveY(1500f, 1.2f).SetEase(Ease.InBack).SetLink(homePanelsManager.gameObject));
            seq.Join(homePanelsManager2.transform.DOLocalMoveY(0f, 2.2f).SetEase(Ease.OutCubic).SetLink(homePanelsManager2.gameObject));
            seq.Join(homePanelsManager2.transform.DOScale(Vector3.one, 2.2f).SetEase(Ease.OutCubic).SetLink(homePanelsManager2.gameObject));
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Addressablesに移行するため、Editorでの自動読み込みは基本的に不要になります
        // return; 
        // // ゲーム実行中は実行しない
        // if (EditorApplication.isPlaying) 
        //     return;

        // SetPrefabs(HOME_STAGE_PREFABS_PATH, ref _homePuzzlePrefabs);
    }
    
    // SetPrefabsメソッド全体も不要になります
    // private void SetPrefabs(string path, ref HomePanelsManager[] prefabArray)
    // {
    //     ...
    // }
#endif

    public void FedeGoHome()
    {
        FadeMoveHome(true);
    }
    public void FedeGoStage()
    {
        FadeMoveHome(false);
    }
    public void HideView()
    {
        _canvanGroup.alpha = 0f;
        _backGameObject.SetActive(false);
        this.gameObject.SetActive(false);
        _homeParent.gameObject.SetActive(false);
        _hardEfffectManager.PlayHardAnimation(GameDataManager.IsHard);
    }
    public void ShowView()
    {
        _canvanGroup.alpha = 1f;
        _backGameObject.SetActive(true);
    }

    private void FadeMoveHome(bool isGoHome)
    {
        GameDataManager.IsHome = isGoHome;
        FadeManager.Instance.FadeIn(()=>{
            if(isGoHome)
                ShowView();
            else
                HideView();
            FadeManager.Instance.FadeOut(0.25f); 
        }, 0.5f, false);
    }

    public void OnPlayButton()
    {
        FedeGoStage();
        AudioManager.Instance.PlayMergeSound();
    }
    public void OnHomeButton()
    {
        PlayerPrefs.SetInt("DailyStage", -1);
        GameDataManager.IsHome = true;
        FadeManager.Instance.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name, 0.5f);
    }
}
