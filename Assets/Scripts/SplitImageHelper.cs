using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.IO;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
#endif

/// <summary>
/// SplitImage処理で生成したセル/ピースに対して
/// Addressable化を含む統合された設定を適用するヘルパークラス
/// </summary>
public static class SplitImageHelper
{
#if UNITY_EDITOR
    /// <summary>
    /// GridCellの標準設定を適用（GridCellBatchUpdaterと同じ設定）
    /// </summary>
    public static void ApplyStandardGridCellSettings(GameObject answerObj, SpritterParam param)
    {
        if (param == null) return;

        Image img = answerObj.GetComponent<Image>();
        if (img != null)
        {
            img.color = param.AnswerColor;
            img.material = param.AnswerMaterial;
        }

        // Outlineは1つのみに統一
        UnityEngine.UI.Outline[] outlines = answerObj.GetComponents<UnityEngine.UI.Outline>();
        if (outlines.Length > 1)
        {
            for (int i = outlines.Length - 1; i >= 1; i--)
            {
                Object.DestroyImmediate(outlines[i]);
            }
        }

        if (outlines.Length > 0)
        {
            var outline = outlines[0];
            outline.effectDistance = Vector2.one * 1f;
            outline.effectColor = param.OutLineColor;
        }
    }

    /// <summary>
    /// Spriteを直接保存してAddressable化（SplitImage処理と統合）
    /// </summary>
    public static string SaveSpriteAndSetupAddressable(
        Sprite sprite,
        string fileName,
        string groupName,
        AddressableAssetGroup group,
        AddressableAssetSettings settings,
        out Sprite loadedSprite)
    {
        loadedSprite = null;

        // 保存先ディレクトリ
        string saveDirectory = $"Assets/Prefabs/Addressable/{groupName}";
        if (!AssetDatabase.IsValidFolder(saveDirectory))
        {
            Directory.CreateDirectory(saveDirectory);
            AssetDatabase.Refresh();
        }

        string assetPath = $"{saveDirectory}/{fileName}";

        // 既存ファイルがあれば削除
        if (File.Exists(assetPath))
        {
            AssetDatabase.DeleteAsset(assetPath);
        }

        // Addressableに登録
        var guid = AssetDatabase.AssetPathToGUID(assetPath);
        if (!string.IsNullOrEmpty(guid))
        {
            AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group);
            if (entry != null)
            {
                Debug.Log($"✅ Addressable化成功: {assetPath}");
            }
        }

        // Spriteをロード（既に保存されている前提）
        loadedSprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);

        return assetPath;
    }

    /// <summary>
    /// AddressableImageLoaderを追加/更新
    /// </summary>
    public static void SetupAddressableImageLoader(GameObject target, string addressPath)
    {
        AddressableImageLoader loader = target.GetComponent<AddressableImageLoader>();
        if (loader == null)
        {
            loader = target.AddComponent<AddressableImageLoader>();
        }
        loader.addressName = addressPath;

        // 注意: Imageのspriteはここではnullにしない（エディタ確認用に残す）
        // プレハブ保存前にClearAddressableImageSprites()で一括nullにする
    }

    /// <summary>
    /// AddressableImageLoaderがついている全Imageのspriteをnullにする
    /// （プレハブ保存直前に呼ばれる）
    /// </summary>
    public static void ClearAddressableImageSprites(GameObject root)
    {
        var loaders = root.GetComponentsInChildren<AddressableImageLoader>(true);
        foreach (var loader in loaders)
        {
            var img = loader.GetComponent<Image>();
            if (img != null && img.sprite != null)
            {
                img.sprite = null;
            }
        }
    }

    /// <summary>
    /// Addressableグループの取得または作成
    /// </summary>
    public static AddressableAssetGroup GetOrCreateAddressableGroup(string groupName)
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError("Addressable Asset Settingsが見つかりません。");
            return null;
        }

        AddressableAssetGroup group = settings.FindGroup(groupName);
        if (group == null)
        {
            var groupTemplate = settings.GetGroupTemplateObject(0) as AddressableAssetGroupTemplate;
            group = settings.CreateGroup(groupName, false, false, true, null, groupTemplate.GetTypes());
            groupTemplate.ApplyToAddressableAssetGroup(group);
            Debug.Log($"Addressable Group '{groupName}' を新規作成しました。");
        }

        return group;
    }

    /// <summary>
    /// SplitImage完了後の統合処理
    /// Addressable化とプレハブ保存を含む
    /// </summary>
    public static void FinalizeAfterSplitImage(
        AbstractGridImageSplitter splitter,
        List<(GameObject obj, string assetPath)> objectsToAddressable)
    {
        GameObject stageRoot = splitter.transform.parent.parent.gameObject;
        var prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(stageRoot);
        
        if (prefabAsset == null)
        {
            Debug.LogWarning("プレハブインスタンスではないため、Addressable化をスキップします。");
            EditorUtility.SetDirty(splitter.gameObject);
            AssetDatabase.SaveAssets();
            return;
        }

        string groupName = prefabAsset.name;
        var group = GetOrCreateAddressableGroup(groupName);
        var settings = AddressableAssetSettingsDefaultObject.Settings;

        if (group != null && settings != null)
        {
            // 各オブジェクトにAddressableImageLoaderを設定
            foreach (var (obj, assetPath) in objectsToAddressable)
            {
                SetupAddressableImageLoader(obj, assetPath);
            }

            EditorUtility.SetDirty(settings);
        }

        // プレハブ保存は AfterSplit() で行われるため、ここでは行わない
        // これにより、画像分割後も通常のオブジェクトとして扱われ、
        // 後続の「ピース再配置」処理でエラーが発生しない
        EditorUtility.SetDirty(stageRoot);
        
        Debug.Log($"✅ SplitImage完了: {groupName} - {objectsToAddressable.Count}個の画像をAddressable化");
    }
#endif
}
