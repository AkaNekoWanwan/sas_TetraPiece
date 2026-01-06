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
using System.Threading.Tasks;

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
    private Sequence clearAnimSeq = null;

    private const string HOME_STAGE_PREFABS_PATH = "Assets/Prefabs/HomePuzzles/";

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
    private async Task Start()
    {
        if(!GameDataManager.IsHome)
        {
            this.gameObject.SetActive(false);
            return;
        }
        await InitlializeColoutine();

        int totalLevel = PlayerPrefs.GetInt("totalLevel", 1);
        // 初回プレイ時はフェードアウトをステージマネージャー側に任せる
        if(totalLevel != 1 || GameDataManager.IsCreativeMode) 
            FadeManager.Instance.FadeOut(0.5f);
    }

    private async Task InitlializeColoutine()
    {
        _playButton.onClick += OnPlayButton;
        // _backButton.onClick += OnHomeButton;
        
        int totalLevel = PlayerPrefs.GetInt("totalLevel", 1);
        // 指定ステージクリアまではホーム画面を表示しない
        if(totalLevel <= GameConst.FIRST_HOME_STAGE_AFTER_CLEAR || GameDataManager.IsCreativeMode)
        {
            HideView();
            return;
        }

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

        // Debug.Log($"アニメーションチェック: isAnim:{GameDataManager.isPlayHomePieceAnimation}, totalLevel:{totalLevel}, nowBoardIndex:{nowBoardIndex}, beforeBoardIndex:{beforeBoardIndex}, isBoardChangeAnimation:{isBoardChangeAnimation}");

        HomePanelsManager homePanelsManager = null;
        // ボード切り替え演出なし
        if(!isBoardChangeAnimation)
        {
            homePanelsManager = Instantiate(_homePuzzlePrefabs[nowBoardIndex % _homePuzzlePrefabs.Length]);
            homePanelsManager.gameObject.SetActive(true);
            homePanelsManager.transform.parent = _homeParent;
            homePanelsManager.transform.localScale = Vector3.one;
            homePanelsManager.transform.localPosition = Vector3.zero;
            homePanelsManager.StartIndex = nowBoardIndex * 30;
            homePanelsManager.Initialize();
        }
        else
        {
            HomePanelsManager homePanelsManager2 = null;
            // 前のボードを出す
            homePanelsManager = Instantiate(_homePuzzlePrefabs[beforeBoardIndex % _homePuzzlePrefabs.Length]);
            homePanelsManager2 = Instantiate(_homePuzzlePrefabs[nowBoardIndex % _homePuzzlePrefabs.Length]);
            homePanelsManager.gameObject.SetActive(true);
            homePanelsManager.transform.parent = _homeParent;
            homePanelsManager.transform.localScale = Vector3.one;
            homePanelsManager.transform.localPosition = Vector3.zero;
            homePanelsManager.StartIndex = beforeBoardIndex * 30;
            homePanelsManager.Initialize();
            // 次のボードも用意しておく
            homePanelsManager2.gameObject.SetActive(true);
            homePanelsManager2.transform.parent = _homeParent;
            homePanelsManager2.transform.localScale = Vector3.one * 0.5f;
            homePanelsManager2.transform.localPosition = new Vector3(0f, -2000f, 0f);
            homePanelsManager2.StartIndex = nowBoardIndex * 30;
            homePanelsManager2.Initialize();

            clearAnimSeq = DOTween.Sequence();
            clearAnimSeq.AppendInterval(1.3f);
            clearAnimSeq.AppendCallback(()=>{ _particle.Play(); AudioManager.Instance.PlayClearSound(); homePanelsManager.PlayClearAnimation();});
            clearAnimSeq.Append(homePanelsManager.transform.DOScale(Vector3.one * 1.05f, 1f).SetEase(Ease.Linear).SetLink(homePanelsManager.gameObject));
            clearAnimSeq.AppendInterval(2f);
            clearAnimSeq.AppendCallback(()=>{ _particle.Stop(); AudioManager.Instance.PlayCardFlipSound();});
            clearAnimSeq.Append(homePanelsManager.transform.DOLocalMoveY(1500f, 1.2f).SetEase(Ease.InBack).SetLink(homePanelsManager.gameObject));
            clearAnimSeq.Join(homePanelsManager2.transform.DOLocalMoveY(0f, 2.2f).SetEase(Ease.OutCubic).SetLink(homePanelsManager2.gameObject));
            clearAnimSeq.Join(homePanelsManager2.transform.DOScale(Vector3.one, 2.2f).SetEase(Ease.OutCubic).SetLink(homePanelsManager2.gameObject));
            clearAnimSeq.SetLink(this.gameObject);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        return;
        // ゲーム実行中は実行しない
        if (EditorApplication.isPlaying) 
            return;

        SetPrefabs(HOME_STAGE_PREFABS_PATH, ref _homePuzzlePrefabs);
    }

    private void SetPrefabs(string path, ref HomePanelsManager[] prefabArray)
    {
        // 一時的に結果を格納するためのList
        List<HomePanelsManager> loadedPrefabs = new List<HomePanelsManager>();

        // 1. 指定されたフォルダ内の全アセットのGUIDを取得
        // string[] guids = AssetDatabase.FindAssets("t:HomePanelsManager", new[] { path });
        string[] guids = AssetDatabase.FindAssets("t:GameObject", new[] { path });
        
        // 2. GUIDをパスに変換し、プレハブとしてロード
        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);

            // フォルダ内のアセットがプレハブであることを確認してロード
            HomePanelsManager prefab = AssetDatabase.LoadAssetAtPath<HomePanelsManager>(assetPath);
            
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

        if(!GameDataManager.IsStageStarted)
            FirebaseManager.instance.StageStart();
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
            _particle.Stop();
            _particle.Clear();
            if(clearAnimSeq != null)
            {
                clearAnimSeq.Kill();
                clearAnimSeq = null;
            }
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
