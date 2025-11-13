using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;

public static class AssetSaver
{
    // 重複削除
    public static void SaveUniqueAsset(Mesh asset, string folderPath, string baseFileName)
    {
        // 拡張子を付ける（例: ".asset"）
        string extension = ".asset";
        string fullPath = Path.Combine(folderPath, baseFileName + extension);

        // 重複チェックしてユニークな名前を取得
        string uniquePath = GetUniqueAssetPath(fullPath);
#if UNITY_EDITOR
        // アセットを保存
        AssetDatabase.CreateAsset(asset, uniquePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
#endif

        // Debug.Log($"Asset saved as: {uniquePath}");
    }

    private static string GetUniqueAssetPath(string path)
    {
        if (!File.Exists(path))
        {
            return path; // すでにユニークならそのまま
        }

        string directory = Path.GetDirectoryName(path);
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(path);
        string extension = Path.GetExtension(path);

        int index = 1;
        string newPath;
        
        do
        {
            newPath = Path.Combine(directory, $"{fileNameWithoutExtension} ({index}){extension}");
            index++;
        } while (File.Exists(newPath));

        return newPath;
    } 
}
