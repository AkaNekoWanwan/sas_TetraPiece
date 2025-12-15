using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

[RequireComponent(typeof(Image))]
public class AddressableImageLoader : MonoBehaviour
{
    public string addressName;
    
    private AsyncOperationHandle<Texture2D> _handle; 
    private bool _isLoaded = false;
    private Image _imageComponent; // ★Awakeから取得をやめ、LoadExternalで取得する

    // Awake() は削除またはコメントアウト
    /*
    void Awake()
    {
        // _imageComponent = GetComponent<Image>();
    }
    */

    void Awake()
    {
        LoadExternal();
    }

    void Start()
    {
        // Start() は空のまま
    }
    
    /// <summary>
    /// StageAddressablePreloaderから呼び出され、Addressableのロードを開始します。
    /// </summary>
    public AsyncOperationHandle LoadExternal()
    {
        // ★★★ 修正箇所: 1. Imageコンポーネントの確実な取得 ★★★
        if (_imageComponent == null)
        {
            _imageComponent = GetComponent<Image>();
        }
        
        // ガード句のチェック (Image Componentはここで null でないことが期待される)
        if (string.IsNullOrEmpty(addressName) || _isLoaded || _imageComponent == null)
        {
            if (_imageComponent == null) {
                 Debug.LogError($"[Loader: {gameObject.name}] SKIPPED: Image Component is NULL (Final Check).");
            }
            return default;
        }
        
        // 以下のロジックはあなたの安定版ロジックを維持

        var img = _imageComponent;
        
        // Alpha=1 で初期化 (あなたの安定版ロジック)
        img.color = new Color(1, 1, 1, 1); 

        var loadOp = Addressables.LoadAssetAsync<Texture2D>(addressName);
        
        loadOp.Completed += handle =>
        {
            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                _handle = handle;
                _isLoaded = true;

                if (this && img && handle.Result)
                {
                    Texture2D tex = handle.Result;
                      // ★ここを追加してもいい（安全策）
// 念のためここでも統一（Importer 側で設定していれば基本同じになるはず）
tex.filterMode = FilterMode.Point;
tex.wrapMode   = TextureWrapMode.Clamp;
                    // Texture2D から Sprite をその場で生成する
                    Sprite sp = Sprite.Create(
                        tex, 
                        new Rect(0, 0, tex.width, tex.height), 
                        new Vector2(0.5f, 0.5f)
                    );
                    
                    img.sprite = sp;
                    
                    // Alphaを維持しつつRGBを白に更新
                    var currentAlpha = img.color.a; 
                    img.color = new Color(1f, 1f, 1f, currentAlpha); 
                }
            }
            else
            {
                Debug.LogError($"[Loader] 画像のロードに失敗: {addressName} (Status: {handle.Status})");
                if (handle.IsValid()) Addressables.Release(handle);
            }
        };
        
        return loadOp;
    }


    void OnDestroy()
    {
        // メモリ解放
        if (_isLoaded && _handle.IsValid())
        {
            Addressables.Release(_handle);
        }
    }
}




