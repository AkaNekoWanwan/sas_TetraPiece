using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class PieceSorter
{
    public enum SortDirection { Left, Right, Up, Down }

    public static bool prioritizeMultiCellPieces = false;

    public static List<PieceDragController> SortBySeededAlternatingDirections(
        this List<PieceDragController> source, 
        int seed
    )
    {
        if (source == null || source.Count == 0) return new List<PieceDragController>();

        // seedを使用してランダムインスタンスを作成
        var rng = new System.Random(seed);

        // 1. 初期ソート（優先度とセル数順）
        IEnumerable<PieceDragController> sortedInitial = prioritizeMultiCellPieces
            ? source.OrderByDescending(p => GetSortPriority(p)).ThenByDescending(p => p.transform.childCount)
            : source.OrderByDescending(p => p.transform.childCount);

        var remainingPieces = sortedInitial.ToList();
        var sortedQueue = new List<PieceDragController>();
        
        // 2. 動的な方向サイクル（1位〜4位）を決定
        List<SortDirection> dynamicCycle = DetermineDirectionCycle(remainingPieces);

        int directionIndex = 0;

        // 3. 決定したサイクルに従って抽出
        while (remainingPieces.Count > 0)
        {
            SortDirection nextDirection = dynamicCycle[directionIndex % dynamicCycle.Count];
            PieceDragController nextPiece = null;

            bool isRandomCellCount = true;
            if(directionIndex < 3)
                isRandomCellCount = false; // 最初の3つはランダムにしない

            switch (nextDirection)
            {
                case SortDirection.Left:
                    if (!isRandomCellCount)
                    {
                        nextPiece = remainingPieces.OrderBy(p => GetMinGridX(p)).First();
                    }
                    else
                    {
                        int minX = remainingPieces.Min(p => GetMinGridX(p));
                        nextPiece = remainingPieces
                            .Where(p => GetMinGridX(p) == minX) // 物理的に一番端にあるやつらだけ集める
                            .OrderBy(_ => rng.Next())           // その中ならセル数無視でランダムに選ぶ
                            .First();
                    }
                    break;
                case SortDirection.Right:
                    if (!isRandomCellCount)
                    {
                        nextPiece = remainingPieces.OrderByDescending(p => GetMaxGridX(p)).First();
                    }
                    else
                    {
                        int maxX = remainingPieces.Max(p => GetMaxGridX(p));
                        nextPiece = remainingPieces
                            .Where(p => GetMaxGridX(p) == maxX) // 物理的に一番端にあるやつらだけ集める
                            .OrderBy(_ => rng.Next())           // その中ならセル数無視でランダムに選ぶ
                            .First();
                    }
                    break;
                case SortDirection.Up:
                    if(!isRandomCellCount)
                    {
                        nextPiece = remainingPieces.OrderByDescending(p => GetMaxGridY(p)).First();
                    }
                    else
                    {
                        int maxY = remainingPieces.Max(p => GetMaxGridY(p));
                        nextPiece = remainingPieces
                            .Where(p => GetMaxGridY(p) == maxY)
                            .OrderBy(_ => rng.Next())
                            .First();
                    }
                    break;
                case SortDirection.Down:
                    if(!isRandomCellCount)
                    {
                        nextPiece = remainingPieces.OrderBy(p => GetMinGridY(p)).First();
                    }
                    else
                    {
                        int minY = remainingPieces.Min(p => GetMinGridY(p));
                        nextPiece = remainingPieces
                            .Where(p => GetMinGridY(p) == minY)
                            .OrderBy(_ => rng.Next())
                            .First();
                    }
                    break;
            }

            if (nextPiece != null)
            {
                sortedQueue.Add(nextPiece);
                remainingPieces.Remove(nextPiece);
            }
            directionIndex++;
        }

        // 混ぜ込み処理
        if (sortedQueue.Count >= 3)
        {   
            // 0, 1, 2 のいずれかのインデックスを決定
            int insertIndex = rng.Next(0, 3); 
            
            int insertTargetIndex = sortedQueue.Count - 1; 
            PieceDragController piece = sortedQueue[insertTargetIndex];
            piece.IsRandomPiece = true;
            
            sortedQueue.RemoveAt(insertTargetIndex);
            sortedQueue.Insert(insertIndex, piece);
        }
        
        return sortedQueue;
    }

    /// <summary>
    /// 各方向で「最も外周を支配しているピース」のセル数を比較し、1位〜4位の方向順序を返す
    /// </summary>
    private static List<SortDirection> DetermineDirectionCycle(List<PieceDragController> pieces)
    {
        var allCells = pieces.SelectMany(p => p.GetComponentsInChildren<AnswerGridPos>()).ToList();
        if (allCells.Count == 0) return new List<SortDirection> { SortDirection.Up, SortDirection.Right, SortDirection.Down, SortDirection.Left };

        int gMinX = allCells.Min(c => c.x);
        int gMaxX = allCells.Max(c => c.x);
        int gMinY = allCells.Min(c => c.y);
        int gMaxY = allCells.Max(c => c.y);

        // 各方向の「最大支配数（単一ピースによる最大接触数）」を格納するリスト
        var directionStrengths = new List<(SortDirection dir, int maxCount)>();

        var directions = new[] { SortDirection.Up, SortDirection.Down, SortDirection.Right, SortDirection.Left };

        foreach (var dir in directions)
        {
            int maxContactForThisDir = 0;
            foreach (var p in pieces)
            {
                var cells = p.GetComponentsInChildren<AnswerGridPos>();
                int contact = 0;
                switch (dir)
                {
                    case SortDirection.Up:    contact = cells.Count(c => c.y == gMaxY); break;
                    case SortDirection.Down:  contact = cells.Count(c => c.y == gMinY); break;
                    case SortDirection.Right: contact = cells.Count(c => c.x == gMaxX); break;
                    case SortDirection.Left:  contact = cells.Count(c => c.x == gMinX); break;
                }
                if (contact > maxContactForThisDir) maxContactForThisDir = contact;
            }
            directionStrengths.Add((dir, maxContactForThisDir));
        }

        // 支配セル数で降順ソート。同数の場合は Up > Right > Down > Left の優先順位で安定させる
        return directionStrengths
            .OrderByDescending(x => x.maxCount)
            .ThenBy(x => GetDirectionPriority(x.dir)) 
            .Select(x => x.dir)
            .ToList();
    }

    private static int GetDirectionPriority(SortDirection dir)
    {
        switch (dir)
        {
            case SortDirection.Up:    return 0;
            case SortDirection.Right: return 1;
            case SortDirection.Down:  return 2;
            case SortDirection.Left:  return 3;
            default: return 4;
        }
    }

    private static int GetSortPriority(PieceDragController piece) => piece.transform.childCount > 1 ? 1 : 0;
    private static int GetMinGridX(PieceDragController p) => p.GetComponentsInChildren<AnswerGridPos>().Min(c => c.x);
    private static int GetMaxGridX(PieceDragController p) => p.GetComponentsInChildren<AnswerGridPos>().Max(c => c.x);
    private static int GetMinGridY(PieceDragController p) => p.GetComponentsInChildren<AnswerGridPos>().Min(c => c.y);
    private static int GetMaxGridY(PieceDragController p) => p.GetComponentsInChildren<AnswerGridPos>().Max(c => c.y);
}