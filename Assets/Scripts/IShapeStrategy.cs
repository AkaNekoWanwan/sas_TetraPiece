using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 形状固有の処理を定義するStrategyインターフェース
/// </summary>
public interface IShapeStrategy
{
    /// <summary>
    /// この戦略が対応する形状タイプ
    /// </summary>
    ShapeType ShapeType { get; }

    /// <summary>
    /// ターゲット領域のパーセント補正値を取得
    /// </summary>
    int GetTargetPercent();

    /// <summary>
    /// グリッドサイズの推奨値を取得（gridIdから）
    /// </summary>
    Vector2Int GetGridSizeFromId(int gridId);

    /// <summary>
    /// セルサイズの補正値を取得
    /// </summary>
    float GetCellSizeMultiplier();

    /// <summary>
    /// 形状固有の初期化処理
    /// </summary>
    void Initialize(AbstractGridImageSplitter splitter);

    /// <summary>
    /// 形状固有の画像分割処理（オプション：Strategyで完全にオーバーライドする場合）
    /// </summary>
    void CustomSplitImageIfNeeded(AbstractGridImageSplitter splitter);
}

/// <summary>
/// Square（四角）用のStrategy実装
/// </summary>
public class SquareShapeStrategy : IShapeStrategy
{
    public ShapeType ShapeType => ShapeType.Square;

    public int GetTargetPercent() => 100;

    public Vector2Int GetGridSizeFromId(int gridId)
    {
        return gridId switch
        {
            3 => new Vector2Int(3, 4),
            4 => new Vector2Int(4, 5),
            5 => new Vector2Int(5, 7),
            6 => new Vector2Int(6, 8),
            7 => new Vector2Int(7, 8),
            8 => new Vector2Int(7, 9),
            _ => Vector2Int.zero
        };
    }

    public float GetCellSizeMultiplier() => 1.0f;

    public void Initialize(AbstractGridImageSplitter splitter)
    {
        // Square固有の初期化（現状特になし）
    }

    public void CustomSplitImageIfNeeded(AbstractGridImageSplitter splitter)
    {
        // Squareは基本的な分割ロジックをそのまま使用
    }
}

/// <summary>
/// Triangle（三角）用のStrategy実装
/// </summary>
public class TriangleShapeStrategy : IShapeStrategy
{
    public ShapeType ShapeType => ShapeType.Triangle;

    public int GetTargetPercent() => 120;

    public Vector2Int GetGridSizeFromId(int gridId)
    {
        return gridId switch
        {
            3 => new Vector2Int(3, 4),
            4 => new Vector2Int(4, 5),
            5 => new Vector2Int(6, 6),
            6 => new Vector2Int(7, 7),
            7 => new Vector2Int(8, 7),
            8 => new Vector2Int(8, 8),
            _ => Vector2Int.zero
        };
    }

    public float GetCellSizeMultiplier() => 1.0f;

    public void Initialize(AbstractGridImageSplitter splitter)
    {
        // Triangle固有の初期化（必要に応じて）
    }

    public void CustomSplitImageIfNeeded(AbstractGridImageSplitter splitter)
    {
        // Triangleの独自分割ロジック（GridImageSplitterTriangleから移植）
    }
}

/// <summary>
/// Hex（六角）用のStrategy実装
/// </summary>
public class HexShapeStrategy : IShapeStrategy
{
    public ShapeType ShapeType => ShapeType.Hex;

    public int GetTargetPercent() => 120;

    public Vector2Int GetGridSizeFromId(int gridId)
    {
        return gridId switch
        {
            3 => new Vector2Int(3, 4),
            4 => new Vector2Int(4, 5),
            5 => new Vector2Int(5, 7),
            6 => new Vector2Int(6, 8),
            7 => new Vector2Int(7, 8),
            8 => new Vector2Int(7, 9),
            _ => Vector2Int.zero
        };
    }

    public float GetCellSizeMultiplier() => 1.0f;

    public void Initialize(AbstractGridImageSplitter splitter)
    {
        // Hex固有の初期化
    }

    public void CustomSplitImageIfNeeded(AbstractGridImageSplitter splitter)
    {
        // Hexの独自分割ロジック
    }
}

/// <summary>
/// Strategy のファクトリークラス
/// </summary>
public static class ShapeStrategyFactory
{
    private static readonly Dictionary<ShapeType, IShapeStrategy> _strategies = new Dictionary<ShapeType, IShapeStrategy>
    {
        { ShapeType.Square, new SquareShapeStrategy() },
        { ShapeType.Triangle, new TriangleShapeStrategy() },
        { ShapeType.Hex, new HexShapeStrategy() }
    };

    public static IShapeStrategy GetStrategy(ShapeType shapeType)
    {
        if (_strategies.TryGetValue(shapeType, out var strategy))
        {
            return strategy;
        }

        Debug.LogError($"Strategy for ShapeType {shapeType} not found. Falling back to Square.");
        return _strategies[ShapeType.Square];
    }

    /// <summary>
    /// 新しい形状タイプのStrategyを追加
    /// </summary>
    public static void RegisterStrategy(ShapeType shapeType, IShapeStrategy strategy)
    {
        _strategies[shapeType] = strategy;
    }
}
