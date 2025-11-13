using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class PieceSorter
{
    public enum SortDirection { Left, Right, Up, Down }

    /// <summary>
    /// 文字列シードを元にリストをシャッフルし、子オブジェクト数で並び替え、その後ランダムサイクルでソートする
    /// </summary>
    /// <param name="source">PieceDragControllerのリスト</param>
    /// <param name="seedString">ランダム化に使用する文字列シード</param>
    /// <returns>並び替えられた新しいリスト</returns>
    public static List<PieceDragController> SortBySeededAlternatingDirections(this List<PieceDragController> source, string seedString)
    {
        if (source == null || source.Count == 0)
        {
            return new List<PieceDragController>();
        }

        // 1. 文字列シードを整数に変換
        int seed = StringToSeed(seedString);
        System.Random rng = new System.Random(seed);

        // 2. コピーを作成し、ランダムにシャッフルする
        var remainingPieces = new List<PieceDragController>(source);
        Shuffle(remainingPieces, rng); 

        // 3. 【追加された処理】子オブジェクト数の降順で並び替える
        // GetChildCount()で、孫以下のオブジェクトは考慮せず、直下の子の数を取得します。
        remainingPieces = remainingPieces
            .OrderByDescending(p => p.transform.childCount)
            .ToList();
            
        // この時点で、remainingPiecesは「シャッフルされ」→「子オブジェクト数で降順ソートされた」状態になる

        var sortedQueue = new List<PieceDragController>();
        
        // 4. ソート方向のサイクルをランダムに決定
        List<SortDirection> directions = new List<SortDirection> 
        { 
            SortDirection.Left, 
            SortDirection.Right, 
            SortDirection.Up, 
            SortDirection.Down 
        };
        Shuffle(directions, rng);

        int directionIndex = 0;

        while (remainingPieces.Count > 0)
        {
            SortDirection nextDirection = directions[directionIndex % directions.Count];
            PieceDragController nextPiece = null;
            
            // 5. ランダムに決めた方向に従ってピースを取得
            switch (nextDirection)
            {
                case SortDirection.Left:
                    // OrderByDescendingがないので、OrderBy().First()でX最小（左端）を取得
                    nextPiece = remainingPieces.OrderBy(p => p.transform.position.x).First();
                    break;
                case SortDirection.Right:
                    // OrderByDescending().First()でX最大（右端）を取得
                    nextPiece = remainingPieces.OrderByDescending(p => p.transform.position.x).First();
                    break;
                case SortDirection.Up:
                    // OrderByDescending().First()でY最大（上端）を取得
                    nextPiece = remainingPieces.OrderByDescending(p => p.transform.position.y).First();
                    break;
                case SortDirection.Down:
                    // OrderBy().First()でY最小（下端）を取得
                    nextPiece = remainingPieces.OrderBy(p => p.transform.position.y).First();
                    break;
            }

            // 見つけたピースを結果に追加し、元のリストから削除する
            if (nextPiece != null)
            {
                sortedQueue.Add(nextPiece);
                remainingPieces.Remove(nextPiece);
            }

            // 次の方向へ進む
            directionIndex++;
        }

        return sortedQueue;
    }

    /// <summary>
    /// リストをランダムにシャッフルする（フィッシャー・イェーツ・シャッフル）
    /// </summary>
    private static void Shuffle<T>(IList<T> list, System.Random rng)
    {
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            T value = list[k];
            list[k] = list[n];
            list[n] = value;
        }
    }

    /// <summary>
    /// 文字列から決定論的な整数シード値を生成する
    /// </summary>
    private static int StringToSeed(string s)
    {
        // シンプルなハッシュ関数（文字コードの合計）を使って整数を生成
        unchecked // オーバーフローを許容する
        {
            int hash = 0;
            foreach (char c in s)
            {
                // 文字コードにシードを乗算し加算する（より均等なハッシュを生成するため）
                hash = 31 * hash + c.GetHashCode();
            }
            return hash;
        }
    }
}