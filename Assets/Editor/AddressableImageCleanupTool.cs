using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using System.Collections.Generic;

public class AddressableImageCleanupTool : EditorWindow
{
    [MenuItem("Tools/Addressable Image Cleanup")]
    public static void ShowWindow()
    {
        GetWindow<AddressableImageCleanupTool>("Addressable Image Cleanup");
    }

    private void OnGUI()
    {
        GUILayout.Label("Addressable Image Cleanup Tool", EditorStyles.boldLabel);
        GUILayout.Space(10);
        
        EditorGUILayout.HelpBox(
            "このツールは、シーン内のプレハブインスタンス内で\n" +
            "AddressableImageLoaderを持つオブジェクトのImage.spriteが\n" +
            "nullでない場合にnullにして、プレハブを上書き保存します。",
            MessageType.Info
        );
        
        GUILayout.Space(10);
        
        if (GUILayout.Button("シーン内の全プレハブインスタンスをチェック", GUILayout.Height(40)))
        {
            CheckAndCleanupAllPrefabInstances();
        }
        
        GUILayout.Space(10);
        
        if (GUILayout.Button("選択中のオブジェクトをチェック", GUILayout.Height(40)))
        {
            CheckAndCleanupSelectedObjects();
        }
    }

    private void CheckAndCleanupAllPrefabInstances()
    {
        // シーン内の全てのGameObjectを取得
        GameObject[] allObjects = FindObjectsOfType<GameObject>();
        int processedPrefabCount = 0;
        int cleanedImageCount = 0;
        HashSet<GameObject> processedPrefabs = new HashSet<GameObject>();

        foreach (GameObject obj in allObjects)
        {
            // プレハブインスタンスのルートを取得
            GameObject prefabRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(obj);
            
            if (prefabRoot != null && !processedPrefabs.Contains(prefabRoot))
            {
                processedPrefabs.Add(prefabRoot);
                
                int cleaned = CheckAndCleanupPrefabInstance(prefabRoot);
                if (cleaned > 0)
                {
                    cleanedImageCount += cleaned;
                    processedPrefabCount++;
                }
            }
        }

        if (cleanedImageCount > 0)
        {
            Debug.Log($"<color=green>✅ クリーンアップ完了:</color> {processedPrefabCount}個のプレハブ内で、{cleanedImageCount}個のImageをクリーンアップしました。");
        }
        else
        {
            Debug.Log("<color=cyan>ℹ️ クリーンアップ対象のImageは見つかりませんでした。</color>");
        }
    }

    private void CheckAndCleanupSelectedObjects()
    {
        GameObject[] selectedObjects = Selection.gameObjects;
        
        if (selectedObjects.Length == 0)
        {
            EditorUtility.DisplayDialog("選択エラー", "オブジェクトが選択されていません。", "OK");
            return;
        }

        int processedPrefabCount = 0;
        int cleanedImageCount = 0;
        HashSet<GameObject> processedPrefabs = new HashSet<GameObject>();

        foreach (GameObject obj in selectedObjects)
        {
            // プレハブインスタンスのルートを取得
            GameObject prefabRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(obj);
            
            if (prefabRoot != null && !processedPrefabs.Contains(prefabRoot))
            {
                processedPrefabs.Add(prefabRoot);
                
                int cleaned = CheckAndCleanupPrefabInstance(prefabRoot);
                if (cleaned > 0)
                {
                    cleanedImageCount += cleaned;
                    processedPrefabCount++;
                }
            }
            else if (prefabRoot == null)
            {
                Debug.LogWarning($"<color=yellow>⚠️ プレハブインスタンスではありません:</color> {obj.name}", obj);
            }
        }

        if (cleanedImageCount > 0)
        {
            Debug.Log($"<color=green>✅ クリーンアップ完了:</color> {processedPrefabCount}個のプレハブ内で、{cleanedImageCount}個のImageをクリーンアップしました。");
        }
        else
        {
            Debug.Log("<color=cyan>ℹ️ クリーンアップ対象のImageは見つかりませんでした。</color>");
        }
    }

    /// <summary>
    /// プレハブインスタンス内のAddressableImageLoaderを持つオブジェクトをチェックしてクリーンアップ
    /// </summary>
    /// <returns>クリーンアップしたImage数</returns>
    private int CheckAndCleanupPrefabInstance(GameObject prefabInstance)
    {
        // プレハブアセットのパスを取得
        string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(prefabInstance);
        
        if (string.IsNullOrEmpty(prefabPath))
        {
            return 0;
        }

        // 階層内の全AddressableImageLoaderを取得
        AddressableImageLoader[] loaders = prefabInstance.GetComponentsInChildren<AddressableImageLoader>(true);
        
        if (loaders.Length == 0)
        {
            return 0;
        }

        int cleanedCount = 0;
        List<string> cleanedObjectNames = new List<string>();

        // プレハブアセットを開く
        GameObject prefabAsset = PrefabUtility.LoadPrefabContents(prefabPath);
        bool needsSave = false;

        try
        {
            foreach (AddressableImageLoader loader in loaders)
            {
                Image img = loader.GetComponent<Image>();
                
                if (img != null && img.sprite != null)
                {
                    // プレハブアセット内の対応するオブジェクトを見つける
                    string hierarchyPath = GetHierarchyPath(loader.transform, prefabInstance.transform);
                    Transform prefabTransform = FindChildByPath(prefabAsset.transform, hierarchyPath);
                    
                    if (prefabTransform != null)
                    {
                        Image prefabImage = prefabTransform.GetComponent<Image>();
                        
                        if (prefabImage != null && prefabImage.sprite != null)
                        {
                            // Imageのspriteをnullに設定
                            prefabImage.sprite = null;
                            EditorUtility.SetDirty(prefabImage);
                            
                            cleanedCount++;
                            cleanedObjectNames.Add(loader.gameObject.name);
                            needsSave = true;
                        }
                    }
                }
            }

            // プレハブを保存
            if (needsSave)
            {
                PrefabUtility.SaveAsPrefabAsset(prefabAsset, prefabPath);
                Debug.Log($"<color=cyan>🔧 プレハブ保存:</color> {prefabPath}\n  クリーンアップしたオブジェクト: {string.Join(", ", cleanedObjectNames)}");
            }
        }
        finally
        {
            // プレハブコンテンツをアンロード
            PrefabUtility.UnloadPrefabContents(prefabAsset);
        }

        return cleanedCount;
    }

    /// <summary>
    /// ルートからの階層パスを取得
    /// </summary>
    private string GetHierarchyPath(Transform target, Transform root)
    {
        List<string> path = new List<string>();
        Transform current = target;
        
        while (current != null && current != root)
        {
            path.Insert(0, current.name);
            current = current.parent;
        }
        
        return string.Join("/", path);
    }

    /// <summary>
    /// パスから子オブジェクトを検索
    /// </summary>
    private Transform FindChildByPath(Transform root, string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return root;
        }

        string[] parts = path.Split('/');
        Transform current = root;

        foreach (string part in parts)
        {
            Transform child = current.Find(part);
            if (child == null)
            {
                return null;
            }
            current = child;
        }

        return current;
    }
}
