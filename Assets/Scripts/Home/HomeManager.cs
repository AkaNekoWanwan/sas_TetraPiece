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

public class HomeManager : MonoBehaviour
{
    public static HomeManager Instance;
    public CanvasGroup _canvanGroup = default;
    public CustomButton _playButton = default;
    public CustomButton _backButton = default;
    public GameObject _backGameObject = default;
    public StageInfo[] _homePuzzlePrefabs;
    public Transform _homeParent;
    public HardEfffectManager _hardEfffectManager;

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
    void Start()
    {
        if(!GameDataManager.IsHome)
            this.gameObject.SetActive(false);
        _playButton.onClick += OnPlayButton;
        _backButton.onClick += OnHomeButton;


        int totalLevel = PlayerPrefs.GetInt("totalLevel", 1);
        int isNowStage = (totalLevel - 1) / 30 % _homePuzzlePrefabs.Length;

        StageInfo stage = null;
        stage = Instantiate(_homePuzzlePrefabs[isNowStage]);
        stage.transform.parent = _homeParent;
        stage.transform.localScale = Vector3.one;
        stage.transform.localPosition = Vector3.zero;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // ゲーム実行中は実行しない
        if (EditorApplication.isPlaying) 
            return;

        SetPrefabs(HOME_STAGE_PREFABS_PATH, ref _homePuzzlePrefabs);
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
        GameDataManager.IsHome = true;
        FadeManager.Instance.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name, 0.5f);
    }
}
