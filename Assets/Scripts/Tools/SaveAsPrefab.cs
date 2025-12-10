using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement; // SceneManagerを使用するために必要
using System; // ShapeTypeを使用するために必要
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

public static class SaveAsPrefab
{
#if UNITY_EDITOR
    public static void Save(GameObject targetObject, string PrefabSavePath)
    {
        Debug.Log($"SaveAsPrefab：セーブ！！,{targetObject.name}");
        // プレハブの完全なパスを構築
        string path = Path.Combine(PrefabSavePath, targetObject.name + ".prefab");
        
        // パスを標準化し、Assets/で始まっていることを確認
        if (!path.StartsWith("Assets/"))
        {
            path = Path.Combine("Assets", PrefabSavePath, targetObject.name + ".prefab");
        }
        
        // ディレクトリが存在しない場合は作成
        string directory = Path.GetDirectoryName(path);
        if (!AssetDatabase.IsValidFolder(directory))
        {
            // ディレクトリを再帰的に作成（Assets/Prefabs/Stages のような構造に対応）
            string currentPath = "Assets";
            string[] subDirs = PrefabSavePath.Split(new char[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string subDir in subDirs)
            {
                string newPath = Path.Combine(currentPath, subDir);
                if (!AssetDatabase.IsValidFolder(newPath))
                {
                    AssetDatabase.CreateFolder(currentPath, subDir);
                }
                currentPath = newPath;
            }
            AssetDatabase.Refresh();
        }

        // プレハブを作成または上書き
        // targetObjectはシーン内のGameObject
        GameObject prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(targetObject, path, InteractionMode.UserAction);

        if (prefab != null)
        {
            Debug.Log($"【Prefab Saved】: {targetObject.name} を {path} に保存/上書きしました。", prefab);
        }
        else
        {
            Debug.LogError($"【Prefab Save Failed】: {targetObject.name} のプレハブ保存に失敗しました。", targetObject);
        }
    }
#endif
}
