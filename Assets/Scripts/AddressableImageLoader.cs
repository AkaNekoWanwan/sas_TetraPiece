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

    void Awake()
    {
        LoadExternal();
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

#if UNITY_EDITOR
        if(!UnityEditor.EditorApplication.isPlaying)
        {
            // Debug.Log($"[Loader: {gameObject.name}] Editor Mode: Loading image from AssetDatabase: {addressName}");
            // addressNameから直接取得するエディター用の簡易ロード
            // Texture2D editorTex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(addressName);
            // if(editorTex != null)
            // {
            //     // Texture2D から Sprite をその場で生成する
            //     Sprite sp = Sprite.Create(
            //         editorTex, 
            //         new Rect(0, 0, editorTex.width, editorTex.height), 
            //         new Vector2(0.5f, 0.5f)
            //     );  
            //     _imageComponent.sprite = sp;
            // }
            Sprite sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(addressName);
            _imageComponent.sprite = sp;
            
            // Addressable化されているか確認 
            if(sp != null)
            {
                var settings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;
                if (settings != null)
                {
                    string guid = UnityEditor.AssetDatabase.AssetPathToGUID(addressName);
                    var entry = settings.FindAssetEntry(guid);
                    
                    if (entry != null)
                    {
                        // Debug.Log($"<color=green>✅ Addressable化確認:OK:</color> {gameObject.name} → {entry.address}");
                    }
                    else
                    {
                        Debug.LogWarning($"<color=yellow>⚠️ Addressable化されていません。自動的にAddressable化します:</color> {gameObject.name} → {addressName}", this);

                        // グループ名を抽出（addressNameの親フォルダ名）
                        string directoryPath = System.IO.Path.GetDirectoryName(addressName);
                        string groupName = System.IO.Path.GetFileName(directoryPath);
                        
                        // グループを取得または作成
                        var group = settings.FindGroup(groupName);
                        if (group == null)
                        {
                            Debug.Log($"<color=cyan>グループ '{groupName}' を新規作成します</color>");
                            var groupTemplate = settings.GetGroupTemplateObject(0) as UnityEditor.AddressableAssets.Settings.AddressableAssetGroupTemplate;
                            group = settings.CreateGroup(groupName, false, false, true, null, groupTemplate.GetTypes());
                            groupTemplate.ApplyToAddressableAssetGroup(group);
                        }
                        
                        // アセットをグループに追加
                        entry = settings.CreateOrMoveEntry(guid, group);
                        
                        if (entry != null)
                        {
                            Debug.Log($"<color=green>✅ Addressable化完了:</color> {gameObject.name} → グループ '{groupName}'", this);
                            UnityEditor.EditorUtility.SetDirty(settings);
                            UnityEditor.AssetDatabase.SaveAssets();
                        }
                        else
                        {
                            Debug.LogError($"<color=red>❌ Addressable化失敗:</color> {gameObject.name} → {addressName}", this);
                        }
                    }
                }
                else
                {
                    // Debug.LogWarning($"<color=red>❌ Addressable化確認:Addressable Settingsが見つかりません:</color> {gameObject.name}", this);
                }
            }

            return default;
        }
#endif
        
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
        // img.color = new Color(1, 1, 1, 1); 

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
                    // img.color = new Color(1f, 1f, 1f, currentAlpha); // ★Alphaは既に1で初期化されているため不要
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




