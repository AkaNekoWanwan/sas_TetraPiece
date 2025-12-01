using UnityEngine;
using System.Text.RegularExpressions; // これが必要です

public static class TextParser
{
    /// <summary>
    /// "answer_Y_X" の形式（負の数を含む）から Y 座標と X 座標を抽出し、Vector2Intで返します。
    /// </summary>
    /// <param name="text">例: "answer_3_0" または "answer_0_-1"</param>
    public static Vector2Int ParseAnswerCoordinates(string text)
    {
        // 変更点: 数字 (\d+) の前にマイナス記号 (-) があっても良い (?) という条件を追加
        // (-?\d+): 負号が省略可能な数字の並び
        string pattern = @"_(-?\d+)_(-?\d+)";
        
        Regex regex = new Regex(pattern);
        
        // 文字列に対してマッチングを実行
        Match match = regex.Match(text);

        if (match.Success && match.Groups.Count >= 3)
        {
            // グループ 1: 最初の数字（Y座標）
            // グループ 2: 2番目の数字（X座標）
            
            // int.Parseは自動的にマイナス記号を処理します
            int y = int.Parse(match.Groups[1].Value);
            int x = int.Parse(match.Groups[2].Value);

            Debug.Log($"パース成功: y={y}, x={x}");
            // UnityのVector2Intは通常 (x, y) の順で格納
            return new Vector2Int(x, y); 
        }
        else
        {
            Debug.LogError($"文字列 '{text}' は予期されたパターンに一致しませんでした。");
            return Vector2Int.zero; 
        }
    }
}