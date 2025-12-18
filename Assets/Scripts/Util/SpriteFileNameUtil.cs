using UnityEngine;
using System.IO;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// スプライトのファイル名を管理するユーティリティクラス
/// </summary>
public static class SpriteFileNameUtil
{
#if UNITY_EDITOR
    /// <summary>
    /// スプライトリストのファイル名を更新する
    /// ファイル名の先頭に連番と接頭辞を付与する。すでに付与されている場合はそれを更新する
    /// </summary>
    /// <param name="sprites">更新対象のスプライトリスト</param>
    /// <param name="prefix">ファイル名の先頭に付ける接頭辞（例: "Daily"）</param>
    /// <param name="startNumber">開始番号（デフォルト: 1）</param>
    /// <param name="enableLog">ログ出力を有効にするか（デフォルト: true）</param>
    /// <example>
    /// 例: 001_Hoge.png -> Daily001_Hoge.png
    ///     001Hoge_1080x1350.png -> Daily001_Hoge_1080x1350.png
    ///     Hoge.png -> Daily001_Hoge.png
    ///     001__Hoge.png -> Daily001_Hoge.png
    /// </example>
    public static void UpdateSpriteFileNames(List<Sprite> sprites, string prefix = "", int startNumber = 1, bool enableLog = true)
    {
        if (sprites == null)
        {
            Debug.LogWarning("SpriteFileNameUtil: スプライトリストがnullです");
            return;
        }

        for (int i = 0; i < sprites.Count; i++)
        {
            Sprite sprite = sprites[i];
            if (sprite == null)
                continue;

            string path = AssetDatabase.GetAssetPath(sprite);
            string directory = Path.GetDirectoryName(path);
            string extension = Path.GetExtension(path);
            string fileName = Path.GetFileNameWithoutExtension(path);

            // 既に先頭に番号が付与されている場合はそれを削除
            // パターン: (prefix)?(数字1個以上)(_の0個以上)を削除
            // 例: "001_Hoge" -> "Hoge", "001__Hoge" -> "Hoge", "001Hoge" -> "Hoge", "Daily001_Hoge" -> "Hoge"
            string pattern = string.IsNullOrEmpty(prefix) 
                ? @"^\d+_*" 
                : $@"^({prefix})?\d+_*";
            
            string cleanFileName = System.Text.RegularExpressions.Regex.Replace(
                fileName,
                pattern,
                "",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );

            string newFileName = $"{prefix}{i + startNumber:D3}_{cleanFileName}";
            string newPath = Path.Combine(directory, newFileName + extension);
            
            AssetDatabase.RenameAsset(path, newFileName);
            
            if (enableLog)
            {
                Debug.Log($"SpriteFileNameUtil: 画像ファイル名更新: {fileName} -> {newFileName}");
            }
        }
    }
#endif
}
