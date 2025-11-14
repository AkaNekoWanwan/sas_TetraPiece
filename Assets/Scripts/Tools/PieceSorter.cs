using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class PieceSorter
{
    public enum SortDirection { Left, Right, Up, Down }

    public static bool prioritizeMultiCellPieces = true;

    /// <summary>
    /// 子オブジェクト数の降順で並び替え、その後、最も子オブジェクト数の多い外周ピースの方向を起点に
    /// 固定の上下左右サイクルでソートを行う。シード値によるランダム化は行わない。
    /// </summary>
    /// <param name="source">PieceDragControllerのリスト</param>
    /// <param name="seedString">使用しないが引数は維持</param>
    /// <param name="prioritizeMultiCellPieces">セル数2以上のピースをセル数1のピースより優先するか</param>
    /// <returns>並び替えられた新しいリスト</returns>
    public static List<PieceDragController> SortBySeededAlternatingDirections(
        this List<PieceDragController> source, 
        string seedString // ★ 変更点1: bool変数を追加し、デフォルトをtrueに設定
    )
    {
        if (source == null || source.Count == 0)
        {
            return new List<PieceDragController>();
        }

        // 1. コピーを作成し、初期ソートを行う
        
        IEnumerable<PieceDragController> sortedInitial;

        if (prioritizeMultiCellPieces)
        {
            // ★ 変更点2: prioritizeMultiCellPiecesがtrueの場合のソートロジック
            // セル数が2以上のピースを優先し、その後子オブジェクト数の降順で並び替える
            // GetSortPriority: セル数 > 1 なら 1、セル数 = 1 なら 0 を返す
            sortedInitial = source
                .OrderByDescending(p => GetSortPriority(p)) // 優先度順 (2以上が先、1が後)
                .ThenByDescending(p => p.transform.childCount) // 同じ優先度内で、子オブジェクト数の降順
                .ToList();
        }
        else
        {
            // 変更前の元のロジック：子オブジェクト数の降順のみ
            sortedInitial = source
                .OrderByDescending(p => p.transform.childCount)
                .ToList();
        }

        var remainingPieces = sortedInitial.ToList();
            
        var sortedQueue = new List<PieceDragController>();
        
        // 2. 最初の抽出方向を決定
        SortDirection initialDirection = DetermineInitialDirection(remainingPieces);

        // 3. 固定の方向サイクルを定義
        List<SortDirection> fullCycle = new List<SortDirection> 
        { 
            SortDirection.Up, 
            SortDirection.Right, 
            SortDirection.Down, 
            SortDirection.Left 
        };
        
        // 最初の方向がサイクル内のどこから始まるかを特定し、サイクルを再構築
        int startIndex = fullCycle.IndexOf(initialDirection);
        List<SortDirection> directions = new List<SortDirection>();
        for (int i = 0; i < fullCycle.Count; i++)
        {
            directions.Add(fullCycle[(startIndex + i) % fullCycle.Count]);
        }

        int directionIndex = 0;

        while (remainingPieces.Count > 0)
        {
            SortDirection nextDirection = directions[directionIndex % directions.Count];
            PieceDragController nextPiece = null;
            
            // 4. 決定した方向に従って、まだソートされていないピースの中から外周のピースを取得
            // remainingPiecesは既に初期ソートされているため、First()で選ばれるピースは、
            // **その方向で最も優先度の高い（セル数が多い）ピース**になります。
            
            switch (nextDirection)
            {
                case SortDirection.Left:
                    // 左端のピースの中から、remainingPiecesの現在のソート順（優先度順）で一番最初のものを取得
                    nextPiece = remainingPieces.OrderBy(p => p.transform.position.x).First();
                    break;
                case SortDirection.Right:
                    nextPiece = remainingPieces.OrderByDescending(p => p.transform.position.x).First();
                    break;
                case SortDirection.Up:
                    nextPiece = remainingPieces.OrderByDescending(p => p.transform.position.y).First();
                    break;
                case SortDirection.Down:
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

        if(prioritizeMultiCellPieces)
        {
            sortedQueue = sortedQueue.OrderByDescending(p => GetSortPriority(p)).ToList();
        }

        return sortedQueue;
    }

    /// <summary>
    /// セル数 > 1 のピースに高い優先度を付与するヘルパーメソッド。
    /// </summary>
    private static int GetSortPriority(PieceDragController piece)
    {
        return piece.transform.childCount > 1 ? 1 : 0;
    }

    /// <summary>
    /// リストの中で「外周の最も子オブジェクト数が多いピース」を見つけ、その方向を返す
    /// </summary>
    private static SortDirection DetermineInitialDirection(List<PieceDragController> pieces)
    {
        if (pieces == null || pieces.Count == 0)
        {
            return SortDirection.Up; // デフォルト
        }

        // 1. 最も優先度の高いピース群を取得 (子オブジェクト数 >= 2 のピース、または全ピース)
        // pieces は既に優先度順でソートされているため、先頭のピースと同じ優先度のグループを取得
        
        // 優先度が最大のグループを取得
        int maxPriority = GetSortPriority(pieces.First());
        int maxCount = pieces.First().transform.childCount;

        var maxPriorityPieces = pieces
            .Where(p => GetSortPriority(p) == maxPriority)
            .Where(p => p.transform.childCount == maxCount) // その中でさらに子オブジェクト数最大のグループ
            .ToList();
        
        // 2. 取得したピース群の中で、最も外周にあるものを探す
        
        // 外周の端座標を計算
        float minX = maxPriorityPieces.Min(p => p.transform.position.x);
        float maxX = maxPriorityPieces.Max(p => p.transform.position.x);
        float minY = maxPriorityPieces.Min(p => p.transform.position.y);
        float maxY = maxPriorityPieces.Max(p => p.transform.position.y);
        
        // 3. 最初に抽出される方向を決定 (上→右→下→左 の優先度)
        
        if (maxPriorityPieces.Any(p => Mathf.Abs(p.transform.position.y - maxY) < 0.001f))
        {
            return SortDirection.Up;
        }
        
        if (maxPriorityPieces.Any(p => Mathf.Abs(p.transform.position.x - maxX) < 0.001f))
        {
            return SortDirection.Right;
        }
        
        if (maxPriorityPieces.Any(p => Mathf.Abs(p.transform.position.y - minY) < 0.001f))
        {
            return SortDirection.Down;
        }
        
        if (maxPriorityPieces.Any(p => Mathf.Abs(p.transform.position.x - minX) < 0.001f))
        {
            return SortDirection.Left;
        }
        
        return SortDirection.Up;
    }
    
    // ... (Shuffle と StringToSeed メソッドは使用しないため削除または維持) ...
    // ShuffleとStringToSeedは元のコードにはありましたが、新しいロジックでは不要です。
    // メソッドの定義は省略します。
    
}