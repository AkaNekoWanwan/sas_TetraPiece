#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using System.ComponentModel;
using UnityEngine.Serialization;

public class AddressablesODRSettings
{
    [MenuItem("Tools/Addressables/Finalize ODR Settings")]
    public static void FinalizeODRSettings()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError("AddressableAssetSettingsが見つかりません。");
            return;
        }

        foreach (var group in settings.groups)
        {
            // Default Groupなどは除外
            if (group == null || group.IsDefaultGroup()) continue;

            // 1. AppleODRSchema の取得または追加
            var odrSchema = group.GetSchema<AppleODRSchema>();
            if (odrSchema == null)
            {
                odrSchema = group.AddSchema<AppleODRSchema>();
                Debug.Log($"[{group.Name}] AppleODRSchemaを追加しました。");
            }

            // 2. Build Path の設定 (AppleODRSchema内のBuildPath変数を修正)
            // settingsからRemoteBuildPathのIDを取得してセットします
            odrSchema.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteBuildPath);

            // 3. 基本となる BundledAssetGroupSchema (Content Packing & Loading) の設定
            var packingSchema = group.GetSchema<BundledAssetGroupSchema>();
            if (packingSchema != null)
            {
                // Build/Load Path を Remote に固定
                // この一行が確実に「RemoteBuildPath」かつ「iOS」を指していることが重要です
                packingSchema.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteBuildPath);
                packingSchema.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteLoadPath);
                
                // iOS ODRの場合、Bundle Modeを「Pack Together」にしておくと管理が楽です
                packingSchema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
            }

            // ツール内で実行するコアな修正
            odrSchema.odrTag = group.Name; // これが Xcode の [On-Demand Resource Tags] になります

            EditorUtility.SetDirty(group);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("✅ 全グループの AppleODRSchema 設定とパスの更新が完了しました。");
    }
}
#endif