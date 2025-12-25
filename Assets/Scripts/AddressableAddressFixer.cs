#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using System.Collections.Generic;

public class AddressableAddressFixer : EditorWindow
{

    [MenuItem("Tools/Addressables/Fix Addresses to Asset Path")]
    public static void FixAddresses()
    {
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;

        if (settings == null)
        {
            Debug.LogError("Addressable Asset Settingsが見つかりません。");
            return;
        }

        int count = 0;
        Undo.RecordObject(settings, "Fix Addressable Addresses");

        foreach (var group in settings.groups)
        {
            // 組み込みのグループなどはスキップ
            if (group == null || group.ReadOnly) continue;

            foreach (var entry in group.entries)
            {
                // 現在のアドレスがアセットの実際のパスと異なる場合、パスで上書きする
                if (entry.address != entry.AssetPath)
                {
                    entry.SetAddress(entry.AssetPath, false);
                    count++;
                }
            }
        }

        // 変更を保存して反映
        settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, null, true, true);
        AssetDatabase.SaveAssets();

        Debug.Log($"完了: {count} 件のアドレスをアセットのパスに修正しました。");
    }

}
#endif