using UnityEngine;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System; // Guidを使うために必要
using System.Security.Cryptography; // シード値生成のために追加

// 生成したセルをオートでいい感じにピースに分けるクラス
public enum ShapeType
{
    Square,
    Hex,
    Triangle
}

// セルの座標を表す構造体
public struct GridCoord
{
    public int X;
    public int Y;
    public GridCoord(int x, int y) { X = x; Y = y; }
}

// パズルのピースの形状を定義するクラス
public class PieceShape
{
    // ピースを構成するセルの相対座標リスト
    public readonly List<GridCoord> Cells;
    public readonly string Name;
    // 形状の重複を避けるために使用されるフラグ
    public int UseCount = 0;
    public int MaxUse = -1;
    public int IsUpSide = 0;   // 三角形セル用。上向き三角形用のShapeなのか下向き三角形用のShapeなのかどちらでも可なのか
                               // 六角形セル用にも。0なら両方用。１ならX奇数用(Yが半個分上)。2ならX偶数用(半個分下)。    

    public PieceShape(string name, List<GridCoord> cells, int maxUse = -1, int isUpSide = 0)
    {
        Name = name;
        Cells = cells;
        MaxUse = maxUse;
        IsUpSide = isUpSide;
    }
}

/// <summary>
// 本体クラス
/// </summary>
public static class CellSplitter
{
    // 公開変数 (読み取り専用)
    public static int GridX = 6; 
    public static int GridY = 6;
    public static int TargetPieceCount = 10;

    // グリッドの状態 (0: 未使用, 1～N: ピースID)
    private static int[,] _grid;
    // 使用可能なピースの形状リスト
    private static List<PieceShape> _availableShapes;
    // ピースIDのカウンター
    private static int _pieceIdCounter; // 0で初期化
    // 成功したピースのリスト
    private static List<PieceRecord> _successfulPlacements = new List<PieceRecord>();

    public static ShapeType CurrentShapeType { get; private set; }

    // === シード値関係 ===
    private static System.Random _random; // 探索のランダム性を制御する乱数生成器
    private static int _randomSeed;       // System.Randomの初期化に使用する数値シード
    public static string PatternSeed { get; private set; } // パターンを再現するためのシード (エンコードされた文字列)

    private static bool _isPatternSeedActive = false;             // パターンシードからピースパターンを復元するか
    private static bool _isPartialPatternSeedActive = false;      // パターンシードからピースを部分復元するか
    private static List<string> _pieceNameSequence;               // パターンシードから復元した、使用するPieceShapeのNameリスト
    private static List<GridCoord> _originCoordSequence;          // パターンシードから復元した、使用するPieceの原点座標リスト
    private static int _placementIndex = 0;                       // 復元用リストのインデックス
    
    // 配置されたピースの情報
    public struct PieceRecord
    {
        public PieceShape Shape;
        public GridCoord Origin;
        public int PieceId;
    }

    // ★ 静的読み取り専用フィールドとして定義
    private static readonly List<PieceShape> SQUARE_SHAPES;
    private static readonly List<PieceShape> HEX_SHAPES;
    private static readonly List<PieceShape> TRIANGLE_SHAPES;
    // ピースのセル数ごとの抽選確率 (合計100%)
    private static readonly Dictionary<int, float> CELL_COUNT_PROBABILITIES = new Dictionary<int, float>
    {
        { 1, 0.10f }, // 1セル: 10%
        { 2, 0.20f }, // 2セル: 30%
        { 3, 0.35f }, // 3セル: 30%
        { 4, 0.35f }  // 4セル: 30%
    };

    // ピースのグループ化リスト (キー: セル数)
    private static Dictionary<int, List<PieceShape>> _shapesByCellCount;
    // private static List<Vector2Int> _neighbors;

    // ピースの最大セル数 (枝刈り、ユーティリティ用に利用)
    private static int _maxCellCount = 0;

    // ★ 静的コンストラクタで一度だけ初期化
    static CellSplitter()
    {
        // Debug.Log($"CellSplitter：ピース初期化！！");
        // C#の静的コンストラクタは、クラスが初めて使用される前に一度だけ実行されます。
        // ここで全ての形状を定義し、実行時（Run-time）の初期化コストを削減します。
        
        // (1) 四角形セルの定義
        SQUARE_SHAPES = new List<PieceShape>();

        // 1セル (I-1)
        SQUARE_SHAPES.Add(new PieceShape("1A", new List<GridCoord> { new GridCoord(0, 0) }));

        // 2セル (I-2)
        SQUARE_SHAPES.Add(new PieceShape("I2-A", new List<GridCoord> { new GridCoord(0, 0), new GridCoord(1, 0) }));
        SQUARE_SHAPES.Add(new PieceShape("I2-B", new List<GridCoord> { new GridCoord(0, 0), new GridCoord(0, 1) }));
        SQUARE_SHAPES.Add(new PieceShape("/2-A", new List<GridCoord> { new GridCoord(0, 0), new GridCoord(1, 1) }));
        SQUARE_SHAPES.Add(new PieceShape("/2-B", new List<GridCoord> { new GridCoord( 1, 0), new GridCoord(0, 1) }));

        // 3セル (I-3, L-3, V-3)
        SQUARE_SHAPES.Add(new PieceShape("I3-A", new List<GridCoord> { new GridCoord(0, 0), new GridCoord(1, 0), new GridCoord(2, 0) }));
        SQUARE_SHAPES.Add(new PieceShape("I3-B", new List<GridCoord> { new GridCoord(0, 0), new GridCoord(0, 1), new GridCoord(0, 2) }));
        SQUARE_SHAPES.Add(new PieceShape("L3-A", new List<GridCoord> { new GridCoord(0, 0), new GridCoord(1, 0), new GridCoord(0, 1) }));
        SQUARE_SHAPES.Add(new PieceShape("」3-B", new List<GridCoord> { new GridCoord(0, 0), new GridCoord(1, 0), new GridCoord(1, 1) }));
        SQUARE_SHAPES.Add(new PieceShape("「3-C", new List<GridCoord> { new GridCoord(0, 0), new GridCoord(0, 1), new GridCoord(1, 1) }));
        SQUARE_SHAPES.Add(new PieceShape("73-D", new List<GridCoord> { new GridCoord(1, 0), new GridCoord(0, 1), new GridCoord(1, 1) }));
        // 4セル (T字の例) - 3x3制約内に収まっている
        SQUARE_SHAPES.Add(new PieceShape("T4-A", new List<GridCoord> {
            new GridCoord(1, 0), new GridCoord(0, 1), new GridCoord(1, 1), new GridCoord(2, 1)
        }));
        SQUARE_SHAPES.Add(new PieceShape("T4-B", new List<GridCoord> {
            new GridCoord(0, 0), new GridCoord(1, 0), new GridCoord(2, 0), new GridCoord(1, 1)
        }));
        SQUARE_SHAPES.Add(new PieceShape("T4-C", new List<GridCoord> {
            new GridCoord(0, 0), new GridCoord(0, 1), new GridCoord(0, 2), new GridCoord(1, 1)
        }));
        SQUARE_SHAPES.Add(new PieceShape("T4-D", new List<GridCoord> {
            new GridCoord(1, 0), new GridCoord(1, 1), new GridCoord(1, 2), new GridCoord(0, 1)
        }));
        SQUARE_SHAPES.Add(new PieceShape("Z4-A", new List<GridCoord> {
            new GridCoord(0, 0), new GridCoord(1, 0), new GridCoord(1, 1), new GridCoord(2, 1)
        }));
        SQUARE_SHAPES.Add(new PieceShape("Z4-B", new List<GridCoord> {
            new GridCoord(1, 0), new GridCoord(2, 0), new GridCoord(0, 1), new GridCoord(1, 1)
        }));
        SQUARE_SHAPES.Add(new PieceShape("Z4-C", new List<GridCoord> {
            new GridCoord(0, 0), new GridCoord(0, 1), new GridCoord(1, 1), new GridCoord(1, 2)
        }));
        SQUARE_SHAPES.Add(new PieceShape("Z4-D", new List<GridCoord> {
            new GridCoord(0, 1), new GridCoord(0, 2), new GridCoord(1, 0), new GridCoord(1, 1)
        }));
        SQUARE_SHAPES.Add(new PieceShape("L4-A", new List<GridCoord> {
            new GridCoord(0, 0), new GridCoord(1, 0), new GridCoord(0, 1), new GridCoord(0, 2)
        }));
        SQUARE_SHAPES.Add(new PieceShape("L4-B", new List<GridCoord> {
            new GridCoord(0, 0), new GridCoord(0, 1), new GridCoord(0, 2), new GridCoord(1, 2)
        }));
        SQUARE_SHAPES.Add(new PieceShape("L4-C", new List<GridCoord> {
            new GridCoord(0, 0), new GridCoord(1, 0), new GridCoord(1, 1), new GridCoord(1, 2)
        }));
        SQUARE_SHAPES.Add(new PieceShape("L4-D", new List<GridCoord> {
            new GridCoord(1, 0), new GridCoord(1, 1), new GridCoord(1, 2), new GridCoord(0, 2)
        }));
        SQUARE_SHAPES.Add(new PieceShape("L4-E", new List<GridCoord> {
            new GridCoord(0, 0), new GridCoord(0, 1), new GridCoord(1, 1), new GridCoord(2, 1)
        }));
        SQUARE_SHAPES.Add(new PieceShape("L4-F", new List<GridCoord> {
            new GridCoord(0, 1), new GridCoord(1, 1), new GridCoord(2, 1), new GridCoord(2, 0)
        }));
        SQUARE_SHAPES.Add(new PieceShape("L4-G", new List<GridCoord> {
            new GridCoord(0, 0), new GridCoord(1, 0), new GridCoord(2, 0), new GridCoord(0, 1)
        }));
        SQUARE_SHAPES.Add(new PieceShape("L4-H", new List<GridCoord> {
            new GridCoord(0, 0), new GridCoord(1, 0), new GridCoord(2, 0), new GridCoord(2, 1)
        }));

        // (2) 六角形セルの定義
        HEX_SHAPES = new List<PieceShape>();
        // 1セル (I-1)
        HEX_SHAPES.Add(new PieceShape(".1-A", new List<GridCoord> { new GridCoord(0, 0) }));

        // 2セル (I-2)
        HEX_SHAPES.Add(new PieceShape("I2-A", new List<GridCoord> { new GridCoord(0, 0), new GridCoord(1, 0) }));
        HEX_SHAPES.Add(new PieceShape("I2-B", new List<GridCoord> { new GridCoord(0, 0), new GridCoord(0, 1) }));
        HEX_SHAPES.Add(new PieceShape("I2-C", new List<GridCoord> { new GridCoord(0, 1), new GridCoord(1, 0) }, -1, 2));
        HEX_SHAPES.Add(new PieceShape("I2-D", new List<GridCoord> { new GridCoord(0, 0), new GridCoord(1, 1) }, -1, 1));

        // 3セル (I-3, L-3, V-3)
        HEX_SHAPES.Add(new PieceShape("_3-A", new List<GridCoord> { new GridCoord(0, 0), new GridCoord(1, 0), new GridCoord(2, 0) })); // 緩やかなVかヘ
        HEX_SHAPES.Add(new PieceShape("_3-A", new List<GridCoord> { new GridCoord(0, 0), new GridCoord(1, 1), new GridCoord(2, 0) }, -1, 1)); // ヘ
        HEX_SHAPES.Add(new PieceShape("_3-A", new List<GridCoord> { new GridCoord(0, 1), new GridCoord(1, 0), new GridCoord(2, 0) }, -1, 2)); // 緩やかなV
        HEX_SHAPES.Add(new PieceShape("I3-A", new List<GridCoord> { new GridCoord(0, 0), new GridCoord(0, 1), new GridCoord(0, 2) })); // 縦長I
        HEX_SHAPES.Add(new PieceShape("I3-B", new List<GridCoord> { new GridCoord(0, 0), new GridCoord(1, 0), new GridCoord(2, 1) }, -1, 2 )); // /
        HEX_SHAPES.Add(new PieceShape("I3-C", new List<GridCoord> { new GridCoord(0, 1), new GridCoord(1, 0), new GridCoord(2, 0) }, -1, 2 )); // \
        HEX_SHAPES.Add(new PieceShape("I3-D", new List<GridCoord> { new GridCoord(0, 0), new GridCoord(1, 1), new GridCoord(2, 1) }, -1, 1 )); // /
        HEX_SHAPES.Add(new PieceShape("I3-E", new List<GridCoord> { new GridCoord(0, 1), new GridCoord(1, 1), new GridCoord(2, 0) }, -1, 1 )); // \
        HEX_SHAPES.Add(new PieceShape("L3-A", new List<GridCoord> { new GridCoord(0, 0), new GridCoord(1, 0), new GridCoord(0, 1) })); // >かL
        HEX_SHAPES.Add(new PieceShape("L3-B", new List<GridCoord> { new GridCoord(0, 0), new GridCoord(1, 0), new GridCoord(1, 1) })); // Jか<
        HEX_SHAPES.Add(new PieceShape("L3-C", new List<GridCoord> { new GridCoord(0, 0), new GridCoord(0, 1), new GridCoord(1, 1) })); // Fか<
        HEX_SHAPES.Add(new PieceShape("L3-D", new List<GridCoord> { new GridCoord(1, 0), new GridCoord(0, 1), new GridCoord(1, 1) })); // <か司
        HEX_SHAPES.Add(new PieceShape("L3-E", new List<GridCoord> { new GridCoord(0, 1), new GridCoord(0, 2), new GridCoord(1, 0) }, -1, 2)); // L
        HEX_SHAPES.Add(new PieceShape("L3-E", new List<GridCoord> { new GridCoord(0, 2), new GridCoord(1, 1), new GridCoord(1, 0) }, -1, 2)); // 司
        HEX_SHAPES.Add(new PieceShape("L3-E", new List<GridCoord> { new GridCoord(0, 0), new GridCoord(1, 1), new GridCoord(1, 2) }, -1, 1)); // J
        HEX_SHAPES.Add(new PieceShape("L3-E", new List<GridCoord> { new GridCoord(0, 0), new GridCoord(0, 1), new GridCoord(1, 2) }, -1, 1)); // F
        // 4セル (T字の例) - 3x3制約内に収まっている
        HEX_SHAPES.Add(new PieceShape("Y◇4-A", new List<GridCoord> {
            new GridCoord(1, 0), new GridCoord(0, 1), new GridCoord(1, 1), new GridCoord(2, 1)})); // ◇かY
        HEX_SHAPES.Add(new PieceShape("Y◇4-B", new List<GridCoord> {
            new GridCoord(0, 0), new GridCoord(1, 0), new GridCoord(2, 0), new GridCoord(1, 1)})); // 逆Yか◇
        HEX_SHAPES.Add(new PieceShape("ト4-A", new List<GridCoord> {
            new GridCoord(0, 0), new GridCoord(0, 1), new GridCoord(0, 2), new GridCoord(1, 1)})); // 逆さトかト
        HEX_SHAPES.Add(new PieceShape("ト4-B", new List<GridCoord> {
            new GridCoord(0, 0), new GridCoord(0, 1), new GridCoord(0, 2), new GridCoord(0, 1)})); // トか逆さト
        HEX_SHAPES.Add(new PieceShape("ト4-C", new List<GridCoord> {
            new GridCoord(1, 0), new GridCoord(1, 1), new GridCoord(1, 2), new GridCoord(0, 1)})); // 反転トか逆さ反転ト
        HEX_SHAPES.Add(new PieceShape("ト4-D", new List<GridCoord> {
            new GridCoord(1, 0), new GridCoord(1, 1), new GridCoord(1, 2), new GridCoord(0, 2)}, -1, 2 )); // 逆さ反転ト
        HEX_SHAPES.Add(new PieceShape("ト4-E", new List<GridCoord> {
            new GridCoord(1, 0), new GridCoord(1, 1), new GridCoord(1, 2), new GridCoord(0, 0)}, -1, 1)); // 反転ト
        HEX_SHAPES.Add(new PieceShape("く4-A", new List<GridCoord> {
            new GridCoord(1, 0), new GridCoord(0, 1), new GridCoord(1, 1), new GridCoord(2, 2)}, -1, 2)); // 上長の「く」
        HEX_SHAPES.Add(new PieceShape("く4-B", new List<GridCoord> {
            new GridCoord(0, 0), new GridCoord(1, 0), new GridCoord(1, 1), new GridCoord(2, 1)})); // 下長反転「く」か上長の「く」
        HEX_SHAPES.Add(new PieceShape("く4-C", new List<GridCoord> {
            new GridCoord(0, 2), new GridCoord(1, 1), new GridCoord(2, 1), new GridCoord(1, 0)}, -1, 2)); // 上長反転「く」
        HEX_SHAPES.Add(new PieceShape("く4-D", new List<GridCoord> {
            new GridCoord(0, 1), new GridCoord(1, 0), new GridCoord(1, 1), new GridCoord(2, 0)})); // 下長「く」か上長反転「く」
        HEX_SHAPES.Add(new PieceShape("く4-C", new List<GridCoord> {
            new GridCoord(2, 0), new GridCoord(1, 1), new GridCoord(0, 1), new GridCoord(1, 2)}, -1, 1)); // 下長「く」
        HEX_SHAPES.Add(new PieceShape("く4-D", new List<GridCoord> {
            new GridCoord(0, 0), new GridCoord(1, 1), new GridCoord(2, 1), new GridCoord(1, 2)}, -1, 1)); // 下長反転「く」
        HEX_SHAPES.Add(new PieceShape("◇4-A", new List<GridCoord> {
            new GridCoord(0, 0), new GridCoord(0, 1), new GridCoord(1, 1), new GridCoord(1, 2)}, -1, 1));  // /の平行四辺形
        HEX_SHAPES.Add(new PieceShape("◇4-A", new List<GridCoord> {
            new GridCoord(0, 0), new GridCoord(0, 1), new GridCoord(1, 1), new GridCoord(1, 0)}));  // /の平行四辺形か\の平行四辺形
        HEX_SHAPES.Add(new PieceShape("◇4-A", new List<GridCoord> {
            new GridCoord(0, 1), new GridCoord(1, 0), new GridCoord(1, 1), new GridCoord(0, 2)}, -1, 2));  // \の平行四辺形

        // (3) 三角形セルの定義
        TRIANGLE_SHAPES = new List<PieceShape>();

        // 1セル (I-1)
        TRIANGLE_SHAPES.Add(new PieceShape(".1-A", new List<GridCoord> { new GridCoord(0, 0) }, -1));

        // 2セル (I-2)
        TRIANGLE_SHAPES.Add(new PieceShape("I2-A", new List<GridCoord> { new GridCoord(0, 0), new GridCoord(1, 0) }, -1, 0));
        TRIANGLE_SHAPES.Add(new PieceShape("I2-B", new List<GridCoord> { new GridCoord(0, 0), new GridCoord(0, 1) }, -1, 0)); // ◇ or 砂時計型
        TRIANGLE_SHAPES.Add(new PieceShape("I2-C", new List<GridCoord> { new GridCoord(0, 0), new GridCoord(2, 1) }, -1, 1)); // /♾️
        TRIANGLE_SHAPES.Add(new PieceShape("I2-D", new List<GridCoord> { new GridCoord(0, 1), new GridCoord(2, 0) }, -1, 1)); // \♾️


        TRIANGLE_SHAPES.Add(new PieceShape("_3-A", new List<GridCoord> { new GridCoord(0, 0), new GridCoord(1, 0), new GridCoord(2, 0) }, -1, 0));

        TRIANGLE_SHAPES.Add(new PieceShape("/3-A", new List<GridCoord> { new GridCoord(0, 0), new GridCoord(0, 1), new GridCoord(1, 1) }, -1, 1));
        TRIANGLE_SHAPES.Add(new PieceShape("/3-B", new List<GridCoord> { new GridCoord(0, 0), new GridCoord(1, 0), new GridCoord(1, 1) }, -1, 2));
        TRIANGLE_SHAPES.Add(new PieceShape("/3-C", new List<GridCoord> { new GridCoord(1, 0), new GridCoord(1, 1), new GridCoord(0, 1) }, -1, 2));
        TRIANGLE_SHAPES.Add(new PieceShape("/3-D", new List<GridCoord> { new GridCoord(2, 0), new GridCoord(1, 0), new GridCoord(1, 1) }, -1, 2));

        TRIANGLE_SHAPES.Add(new PieceShape("/4-A", new List<GridCoord> { new GridCoord(0, 0), new GridCoord(1, 0), new GridCoord(1, 1), new GridCoord(2, 1) }, -1, 2));
        TRIANGLE_SHAPES.Add(new PieceShape("/4-B", new List<GridCoord> { new GridCoord(0, 1), new GridCoord(1, 1), new GridCoord(1, 0), new GridCoord(2, 0) }, -1, 2));
        TRIANGLE_SHAPES.Add(new PieceShape("/4-C", new List<GridCoord> { new GridCoord(0, 0), new GridCoord(0, 1), new GridCoord(1, 1), new GridCoord(2, 1) }, -1, 1));
        TRIANGLE_SHAPES.Add(new PieceShape("/4-D", new List<GridCoord> { new GridCoord(0, 2), new GridCoord(0, 1), new GridCoord(1, 1), new GridCoord(1, 0) }, -1, 2));

        TRIANGLE_SHAPES.Add(new PieceShape("<4-A", new List<GridCoord> { new GridCoord(0, 0), new GridCoord(0, 1), new GridCoord(1, 0), new GridCoord(1, 1) }, -1, 1));
        TRIANGLE_SHAPES.Add(new PieceShape("<4-B", new List<GridCoord> { new GridCoord(0, 0), new GridCoord(0, 1), new GridCoord(1, 0), new GridCoord(1, 1) }, -1, 2));

        TRIANGLE_SHAPES.Add(new PieceShape("L4-A", new List<GridCoord> { new GridCoord(0, 0), new GridCoord(1, 0), new GridCoord(2, 0), new GridCoord(0, 1) }, -1, 1));
        TRIANGLE_SHAPES.Add(new PieceShape("L4-B", new List<GridCoord> { new GridCoord(0, 0), new GridCoord(1, 0), new GridCoord(2, 0), new GridCoord(2, 1) }, -1, 1));
        TRIANGLE_SHAPES.Add(new PieceShape("L4-C", new List<GridCoord> { new GridCoord(0, 1), new GridCoord(1, 1), new GridCoord(2, 1), new GridCoord(0, 0) }, -1, 1));
        TRIANGLE_SHAPES.Add(new PieceShape("L4-D", new List<GridCoord> { new GridCoord(0, 1), new GridCoord(1, 1), new GridCoord(2, 1), new GridCoord(2, 0) }, -1, 1));

        TRIANGLE_SHAPES.Add(new PieceShape("Δ4-A", new List<GridCoord> { new GridCoord(0, 0), new GridCoord(1, 0), new GridCoord(2, 0), new GridCoord(1, 1) }, -1, 2));
        TRIANGLE_SHAPES.Add(new PieceShape("Δ4-B", new List<GridCoord> { new GridCoord(1, 0), new GridCoord(0, 1), new GridCoord(1, 1), new GridCoord(2, 1) }, -1, 1));
    }

    /// <summary>
    /// ピース形状リストをセル数ごとにグループ化する
    /// </summary>
    private static void GroupShapesByCellCount()
    {
        _shapesByCellCount = _availableShapes
            .GroupBy(s => s.Cells.Count)
            .ToDictionary(g => g.Key, g => g.ToList());
        
        // 最大セル数を更新
        _maxCellCount = _shapesByCellCount.Keys.Max();
    }

    private static readonly Comparison<PieceShape> ShapeComparer = (a, b) => 
    {
        // 1. UseCountが少ない方を優先
        int useCompare = a.UseCount.CompareTo(b.UseCount);
        if (useCompare != 0) return useCompare;

        // 2. MaxUse超過状態を後回し（MaxUse <= UseCount の状態を後回し）
        bool aOver = (0 <= a.MaxUse && a.MaxUse <= a.UseCount);
        bool bOver = (0 <= b.MaxUse && b.MaxUse <= b.UseCount);

        if (aOver != bOver)
            return aOver ? 1 : -1; // aが超過なら後(1)、bが超過なら後(-1)

        // 3. 乱数によるランダムな順序（同順位の場合のランダム性確保）
        // System.Random を使ったカスタムソートは非推奨だが、乱数性を確保するにはシャッフルとの併用が必要
        return 0; // 一旦同じ順位として扱う
    };
 
    public static void CellSplit( int cols, int rows, ref int orderPieceNum, List<AnswerGridPos> cells, GridPieceListController gridPieceListController, ShapeType type, string patternSeed = null, List<string> avoidPatternSeeds = null, bool shouldClearCells = true )
    {
        // ピース生成のパラメーターセット
        GridX = cols;
        GridY = rows;
        CurrentShapeType = type; // ★ ここでShapeTypeを保持する
        PatternSeed = patternSeed; // パターンシードを保持

        // パターンシードから取得したピース数を一時保存
        int seedPieceCount = -1;
        
        // パターンシードがある場合は解析
        if (!string.IsNullOrEmpty(patternSeed))
        {
            AnalysisPatternSeed(patternSeed);
            seedPieceCount = TargetPieceCount; // シードから取得した値を保存
        }

        // orderPieceNumを優先して使用
        TargetPieceCount = orderPieceNum;
        
        // パターンシードのピース数とorderPieceNumが異なるかチェック
        bool forceFlexibleCount = (seedPieceCount > 0 && seedPieceCount != orderPieceNum);

        // 乱数生成器の数値シードを決定 (ランダム探索の再現性用。パターン再現とは別)
        _randomSeed = GetRandomIntSeed(); // 新しい乱数シード生成関数を使用
        _random = new System.Random(_randomSeed);

        avoidPatternSeeds = null;

        // 1. ピース形状の定義を取得
        SetAvailableShapes();
        GroupShapesByCellCount();
        
        // ピース情報の生成
        CreatePiecePlacements(patternSeed, avoidPatternSeeds, forceFlexibleCount);

        // 作成したピース情報をもとにピースオブジェクトに反映させる
        // コントローラーの前準備
        orderPieceNum = _successfulPlacements.Count;
        if(gridPieceListController != null)
        {
            gridPieceListController.pieceNum = orderPieceNum;
            bool backupFlg = gridPieceListController.isOverrayPieceNum;
            gridPieceListController.isOverrayPieceNum = false;
            // shouldClearCells: 新規作成時はtrue（セル削除）、再利用時はfalse（セル保持）
            gridPieceListController.PreSetPieceDragControllers(shouldClearCells: shouldClearCells);
            gridPieceListController.isOverrayPieceNum = backupFlg;
            List<PieceDragController> pieceList = gridPieceListController.gameObject.GetComponentsInChildren<PieceDragController>().ToList();   
            // セルを対応するピースの子オブジェクトにする
            RegisterCellsAsPieces(pieceList, cells);
        }
    }

    // セルを対応するピースの子オブジェクトにする
    private static void RegisterCellsAsPieces(List<PieceDragController> pieceList, List<AnswerGridPos> cells)
    {
        for(int i = 0; i < _successfulPlacements.Count; i++)
        {
            Transform piece = pieceList[i].transform;
            // ピース情報を取得
            PieceRecord cellsInfo = _successfulPlacements[i];
            // そのピースの基礎位置のセルの位置を取得
            GridCoord originCell = cellsInfo.Origin;
            // そのピースの形状(基礎セルからの相対位置)情報を取得
            List<GridCoord> Cells = cellsInfo.Shape.Cells;

            // Debug.Log($"Debug:{i}, Count:{_successfulPlacements.Count}, cellNum:{Cells.Count}, shapeName:{cellsInfo.Shape.Name}");

            // 三角形アウトライン用
            List<TriangleCellCopyHandler> triCellCopyList;
            triCellCopyList = new List<TriangleCellCopyHandler>();
            for (int j = 0; j < Cells.Count; j++)
            {
                int x = originCell.X + Cells[j].X;
                int y = originCell.Y + Cells[j].Y;

                AnswerGridPos cell = FindCell(cells, x, y);
                if (cell != null)
                {
                    // ワールド位置を保持するためSetParentを使用（第2引数true）
                    cell.transform.SetParent(piece, true);
                    
                    TriangleCellCopyHandler triCellCopy = cell.gameObject.GetComponent<TriangleCellCopyHandler>();
                    if (triCellCopy != null)
                        triCellCopyList.Add(triCellCopy);
                }
                else
                {
                    Debug.LogError($"セルが見つからない!! x:{x}, y:{y}");
                }
            }
            if (1 <= triCellCopyList.Count)
            {
                triCellCopyList[0].UpdateAllCellCopyTransform(triCellCopyList);
            }
        }
        
        // ★ 全てのセルの親変更が完了した後、RectTransformの座標計算を強制的に完了
        Canvas.ForceUpdateCanvases();
    }

    // 指定のX,Yのセルを見つける
    private static AnswerGridPos FindCell(List<AnswerGridPos> cells, int searchX, int searchY)
    {
        AnswerGridPos cell = cells.FirstOrDefault(c => c.x == searchX && c.y == searchY);
        return cell;
    }

    private static void SetAvailableShapes()
    {
        _availableShapes = GetPieceShapes(CurrentShapeType);
    }

    public static List<PieceShape> GetPieceShapes(ShapeType shapeType)
    {
        switch (shapeType)
        {
            case ShapeType.Square:
            default:
                return new List<PieceShape>(SQUARE_SHAPES);
            case ShapeType.Hex:
                return new List<PieceShape>(HEX_SHAPES);
            case ShapeType.Triangle:
                return new List<PieceShape>(TRIANGLE_SHAPES);
        }
    }

    private static void CreatePiecePlacements(string patternSeed = null, List<string> avoidPatternSeeds = null, bool forceFlexibleCount = false)
    {
        bool success = false;
        bool isRandom = true;
        bool enforceCount = false;
        
        // パターンシードのピース数とorderPieceNumが異なる場合は強制的にenforceCount=falseにする
        if (forceFlexibleCount)
        {
            enforceCount = false;
        }

        // =========================================================
        // 第1パス: 受け取ったパターンシードのデコードと強制再現
        // =========================================================
        if (_isPatternSeedActive)
        {
            PreSolve();
            success = Solve(0, 0, true, true);
            isRandom = false;
            // Debug.Log($"CellSplitter.CreatePiecePlacements:--- 第1パス開始: パターンシードの再現 ---, success:{success}");
        }

        if (_isPartialPatternSeedActive && !success)
        {
            PreSolve();
            isRandom = true;

            // --- 追加：部分再現（先行配置）フェーズ ---
            // ヘッダーがない、あるいは完全再現に失敗した場合のフォールバック用
            for (int i = 0; i < _pieceNameSequence.Count; i++)
            {
                string name = _pieceNameSequence[i];
                GridCoord origin = _originCoordSequence[i];
                PieceShape shape = _availableShapes.FirstOrDefault(s => s.Name == name);

                if (shape != null)
                {
                    // ここでIsUpSideのチェックも事前に行う
                    bool sideOk = true;
                    if (CurrentShapeType == ShapeType.Triangle)
                    {
                        bool isUp = ((origin.X + origin.Y) % 2) == 0;
                        if (shape.IsUpSide == 2 && isUp) sideOk = false;
                        if (shape.IsUpSide == 1 && !isUp) sideOk = false;
                    }
                    else if (CurrentShapeType == ShapeType.Hex)
                    {
                        if (shape.IsUpSide == 2 && origin.X % 2 == 1) sideOk = false;
                        if (shape.IsUpSide == 1 && origin.X % 2 == 0) sideOk = false;
                    }

                    // 配置可能かつ向きが合っていれば配置、ダメなら「無視して次へ」
                    if (sideOk && CanPlace(origin.X, origin.Y, shape))
                    {
                        PlacePiece(origin.X, origin.Y, shape);
                        shape.UseCount++;
                    }
                }
            }

            // 先行配置が終わった状態で、残りの空きマスを埋める
            // 部分再現なので enforceCount は false に設定
            success = Solve(0, 0, false, false); 
            isRandom = true; 
        }

        // =========================================================
        // 第2パス以降: ランダム探索（ユニーク性保証付き）
        // =========================================================
        if (!success)
        {
            _isPatternSeedActive = false;
            _isPartialPatternSeedActive = false;
            const int MAX_UNIQUE_ATTEMPTS = 100; // ユニーク生成の試行回数上限
            List<string> _avoidSeeds = avoidPatternSeeds ?? new List<string>();
            
            for(int attempt = 0; attempt < MAX_UNIQUE_ATTEMPTS; attempt++)
            {
                // ユニーク探索のために、毎回異なる数値シードで乱数生成器をリセット
                _randomSeed = GetRandomIntSeed();
                _random = new System.Random(_randomSeed);

                // 探索パスの優先度順に試行
                bool currentAttemptSuccess = false;
                
                // 試行1: ターゲットピース数厳守・形状ユニーク
                if( 0 < TargetPieceCount && !forceFlexibleCount )
                {
                    enforceCount = true;
                    for(int attempt2 = 0; attempt2 < MAX_UNIQUE_ATTEMPTS; attempt2++)
                    {
                        PreSolve();
                        if (Solve(0, 0, true))
                        {
                            currentAttemptSuccess = true;
                            break;
                        }
                    }   
                }
                
                // 試行2: ピース数無視
                if (!currentAttemptSuccess)
                {
                    enforceCount = false;
                    PreSolve();
                    // _grid = new int[GridX, GridY];
                    // for(int x = 0; x < GridX; x++)
                    // {
                    //     for(int y = 0; y < GridY; y++)
                    //     {
                    //         Debug.Log($"_grid[{x},{y}]={_grid[x, y]}");
                    //     }
                    // }
                    if (Solve(0, 0, false)) currentAttemptSuccess = true;
                }

                // 成功した場合、ユニーク性をチェック
                if (currentAttemptSuccess)
                {
                    string newPatternSeed = EncodePlacement(_successfulPlacements);
                    
                    if (IsUniqueSeed(newPatternSeed, _avoidSeeds))
                    {
                        // ユニーク性が確認された！
                        success = true;
                        PatternSeed = newPatternSeed;
                        // Debug.Log($"<color=green>第{attempt + 2}パス成功 (ユニーク)！</color>");
                        break; // ループを抜けて成功
                    }
                    // else
                    // {
                    //     Debug.LogWarning($"生成されたパターンは既知のシードと重複しました。再試行します (試行回数: {attempt + 1})");
                    // }
                }
            }
            
            // if (!success)
            // {
            //     Debug.LogError($"ランダム探索 ({MAX_UNIQUE_ATTEMPTS}回試行) でユニークなパターンの生成に失敗しました。");
            // }
        }
        
        if (success)
        {
            // Debug.Log($"<color=green>敷き詰め完了！</color> 最終ピース数: {_pieceIdCounter - 1}, 使用パターンシード: {PatternSeed}");
            if (!enforceCount)
                MergeSmallPieces();
            else
            {
                RebalancePieces();
            }
            
            if(isRandom)
            {
                // ランダム探索で成功した場合、新しいパターンシードを生成・更新
                PatternSeed = EncodePlacement(_successfulPlacements);
            }
        }
        // else
        // {
        //     Debug.LogError($"全パス失敗。グリッドサイズ ({GridX}x{GridY}) は敷き詰め不可能です。");
        // }
    }
    
    // ピースの使用フラグをリセット
    private static void ResetPieceUsage()
    {
        foreach (var shape in _availableShapes)
        {
            shape.UseCount = 0;
        }
    }

    // ピースデータ作成開始前の準備
    private static void PreSolve()
    {
        // グリッドとカウンターをリセット
        _grid = new int[GridX, GridY];
        _pieceIdCounter = 1;
        _successfulPlacements.Clear();
        ResetPieceUsage(); // ピースの使用フラグをリセット
        _placementIndex = 0; // **インデックスをリセット**
        
        // ランダム探索がブレないよう、乱数生成器もリセット（再初期化）
        _random = new System.Random(_randomSeed);
    }

    // ========== バックトラッキングの中核ロジック ==========

    // 次の空きセルを探す (左上から順に)
    private static bool FindNextEmptyCell(out int nextX, out int nextY)
    {
        for (int y = 0; y < GridY; y++)
        {
            for (int x = 0; x < GridX; x++)
            {
                // Debug.Log($"debug:x:{x}, y:{y}, {_grid[x, y]}");
                if (_grid[x, y] == 0)
                {
                    nextX = x;
                    nextY = y;
                    return true;
                }
            }
        }

        nextX = -1;
        nextY = -1;
        return false; // 全て埋まった
    }

    // ピースを再帰的に配置しようと試みる関数
    // enforceCount: TargetPieceCountを厳守するか
    private static bool Solve(int startX, int startY, bool enforceCount, bool isPatternSeedActive = false) // 引数を追加
    {
        // 探索に使用するピースのリストを決定
        // Debug.Log($"CellSplitter.Solve:1:{_availableShapes.Count}, startX:{startX}, startY:{startY}, isPatternSeedActive:{isPatternSeedActive}");
        // Count厳守モードで、ピース数が上限を超えた場合は失敗 (早期終了)
        // 終了条件: すべてのセルが埋まった
        if (!FindNextEmptyCell(out startX, out startY))
        {
            // Debug.Log($"CellSplitter.Solve, 全て埋まった！, _isPatternSeedActive:{_isPatternSeedActive}, _pieceIdCounter:{_pieceIdCounter}, enforceCount:{enforceCount}, TargetPieceCount:{TargetPieceCount}");
            if(_isPatternSeedActive)
                return true;
            if (_pieceIdCounter != -1 && enforceCount && _pieceIdCounter - 1 != TargetPieceCount) return false;
            // ランダム探索で成功した場合、CreatePiecePlacementsでPatternSeedを更新する
            return true;
        }
        // FindNextEmptyCell の直後あたりに追加
if (enforceCount && TargetPieceCount > 0)
{
    int currentPieces = _pieceIdCounter - 1;

    // ① 既に上限超えてたら即死
    if (currentPieces > TargetPieceCount) return false;

    // ② 残りセル数を数える
    int remaining = 0;
    for (int y = 0; y < GridY; y++)
        for (int x = 0; x < GridX; x++)
            if (_grid[x, y] == 0) remaining++;

    // ③ これ以上「少なく」できない最小ピース数（最大サイズで埋めたとして）
    // ceil(remaining / _maxCellCount)
    int minMorePieces = (remaining + _maxCellCount - 1) / _maxCellCount;

    // ④ これ以上「多く」できない最大ピース数（最小サイズで埋めたとして）
    int minSize = GetMinPieceSize(); // だいたい1のはずだけど一応
    int maxMorePieces = remaining / minSize;

    // ⑤ 目標個数に届かない/必ず超えるなら切る
    if (currentPieces + minMorePieces > TargetPieceCount) return false;
    if (currentPieces + maxMorePieces < TargetPieceCount) return false;
}

        // Debug.Log($"CellSplitter.Solve:2, startX:{startX}, startY:{startY}");
        // ★ ここで次の探索原点として origin を定義
        GridCoord origin = new GridCoord(startX, startY);

        // ★ リファクタリング箇所: パターン復元モードの処理を専用関数に委譲
        if (isPatternSeedActive)
        {
            return SolveWithPatternSeed(startX, startY, enforceCount);
        }
        // ★ 1. ランダム探索モード: 抽選ロジックを実装 ★
        // セル数とその抽選確率に基づいて、試行するセル数の順序リストを生成
        List<int> availableCellCounts = GetShuffledCellCounts();        
        // 試行済みだが失敗したセル数を除外するためのセット
        HashSet<int> failedCellCounts = new HashSet<int>();

        // Debug.Log($"CellSplitter.Solve:3:{availableCellCounts.Count}, startX:{startX}, startY:{startY}");
        
        while(failedCellCounts.Count < availableCellCounts.Count)
        {
            // 1. 確率に基づいてセル数を抽選 (失敗済みのセル数は除外)
            int selectedCellCount = SelectCellCountByProbability(availableCellCounts, failedCellCounts);
            // Debug.Log($"CellSplitter.Solve:4:{selectedCellCount}, startX:{startX}, startY:{startY}");
            if (selectedCellCount == -1) break;  // 抽選対象が残っていない
            // Debug.Log($"CellSplitter.Solve:5:{selectedCellCount}, startX:{startX}, startY:{startY}");
            // 2. 指定のセル数のピースから UseCount が最も少ないものから優先して選択
            //    (同UseCount内ではランダムに試行)
            List<PieceShape> candidates = GetPrioritizedCandidates(selectedCellCount, origin);

            // 試行済みだが失敗したピースを除外するためのセット（この再帰レベル内のみ）
            HashSet<PieceShape> failedShapes = new HashSet<PieceShape>();
            
            // 3. 候補ピースを全て試行 (バックトラック処理)
            while(failedShapes.Count < candidates.Count)
            {
                // UseCountが最も少ないグループの中からランダムに選択
                PieceShape shape = SelectRandomCandidate(candidates, failedShapes);
                // Debug.Log($"CellSplitter.Solve:6:tryGetShape, startX:{startX}, startY:{startY}");
                if (shape == null) break; // 選択対象なし (本来はcandidates.Count == failedShapes.Countでループ終了)

                // Debug.Log($"CellSplitter.Solve:7:{shape.Name}, startX:{startX}, startY:{startY}");

                // ピースの配置、再帰、後戻りのコアロジック
                if (TryPlaceAndSolve(origin, shape, enforceCount))
                {
                    return true; // 成功
                }
                
                // 3. バックトラックしてきた場合、試みたピースを候補から除外
                failedShapes.Add(shape);
            }
            
            // 3-1. 指定したセル数のピースが全てうまくいかなかったら、そのセル数を除いて1に戻る
            failedCellCounts.Add(selectedCellCount);
        }
        
        // 3-2. 全てのピースを試してダメだったら従来通りreturn false
        return false;
    }

    // TryPlaceAndSolve メソッド全体の修正
    private static bool TryPlaceAndSolve(GridCoord origin, PieceShape shape, bool enforceCount)
    {   
        // MaxUse制約チェック
        if (0 <= shape.MaxUse && shape.MaxUse <= shape.UseCount) return false;

        // 1. シフト量の計算
        GridCoord shift = CalculateOptimalShift(shape); 

        // 2. シフト後の最終的な原点座標 (IsUpSideチェックに使用する基準位置)
        int finalOriginX = origin.X + shift.X;
        int finalOriginY = origin.Y + shift.Y;
        
        // ★ 3. シフト後の基準位置に基づいて IsUpSide 規制チェックを実行 ★
        
        // 三角形セルの向きチェック
        if (CurrentShapeType == ShapeType.Triangle)
        {
            // 基準: シフト後の原点 (finalOriginX, finalOriginY)
            bool isUpSide = ((finalOriginX + finalOriginY) % 2) == 0; 
            if (shape.IsUpSide == 2 && isUpSide) return false;
            if (shape.IsUpSide == 1 && !isUpSide) return false;
        }
        // 六角形の位置チェック
        if (CurrentShapeType == ShapeType.Hex)
        {
            // 基準: シフト後の原点 (finalOriginX)
            if (shape.IsUpSide == 2 && finalOriginX % 2 == 1) return false;
            if (shape.IsUpSide == 1 && finalOriginX % 2 == 0) return false;
        }

        // Debug.Log($"CellSplitter:8:{shape.Name}, origin.X:{origin.X}, origin.Y:{origin.Y}");
        
        // 4. 配置可能性チェック (CanPlaceはシフトを考慮)
        if (CanPlace(origin.X, origin.Y, shape))
        {
            // Debug.Log($"CellSplitter:9:{shape.Name}, origin.X:{origin.X}, origin.Y:{origin.Y}");
            // 1. 配置（PlacePieceは内部でシフトと finalOrigin を使用）
            PlacePiece(origin.X, origin.Y, shape);

            // 2. ピースの使用フラグをセット
            shape.UseCount++;

            // 3. 次のセルへ再帰
            if (Solve(origin.X, origin.Y, enforceCount, false))
            {
                return true; // 成功
            }

            // 4. 後戻り (Backtrack)
            // RemovePieceにシフト前の原点とシフト後の原点の両方が必要になるため、
            // 呼び出し側でシフト後の原点を計算し直すか、RemovePiece自体を修正する必要がある。
            RemovePiece(origin.X, origin.Y, shape); // RemovePieceの修正が必要
            shape.UseCount--; 
        }
        // Debug.Log($"CellSplitter:10:{shape.Name}, origin.X:{origin.X}, origin.Y:{origin.Y}");
        return false;
    }

    // CellSplitter クラス内に追加

    /// <summary>
    /// パターンシードから指定された単一のピースを配置し、再帰探索を試みる。
    /// </summary>
    private static bool SolveWithPatternSeed(int startX, int startY, bool enforceCount)
    {
        // Debug.Log($"CellSplitter.SolveWithPatternSeed:1: startX:{startX}, startY:{startY}, enforceCount:{enforceCount}, _pieceNameSequence.Count:{_pieceNameSequence.Count}, _placementIndex:{_placementIndex}");
        // 復元リストが尽きた場合、グリッド全体が埋まっていれば成功と見なす
        if (_placementIndex >= _pieceNameSequence.Count)
        {
            // グリッドが埋まっているか再確認
            if (!FindNextEmptyCell(out int nextX, out int nextY))
            {
                // Debug.Log($"CellSplitter.SolveWithPatternSeed:2:");
                if (_pieceIdCounter != -1 && enforceCount && _pieceIdCounter - 1 != TargetPieceCount) return false;
                // Debug.Log($"CellSplitter.SolveWithPatternSeed:3:");
                return true; // 成功
            }
            // グリッドが埋まっていないのにピースリストが尽きた場合は失敗
            // Debug.Log($"CellSplitter.SolveWithPatternSeed:4:");
            return false;
        }
        
        // 復元リストから次のピース名と座標を取得
        string requiredName = _pieceNameSequence[_placementIndex];
        GridCoord requiredOrigin = _originCoordSequence[_placementIndex];
        startX = requiredOrigin.X;
        startY = requiredOrigin.Y;
        // 復元された原点座標と現在の探索開始座標が一致しない場合は失敗（シードとの整合性エラー）
        // if (requiredOrigin.X != startX || requiredOrigin.Y != startY)
        // {
        //     Debug.Log($"CellSplitter.SolveWithPatternSeed:5: requiredOrigin.X:{requiredOrigin.X}, startX:{startX}, requiredOrigin.Y:{requiredOrigin.Y}, startY:{startY},");
        //     // ピースが復元リスト順に配置されていないため、ここではスキップ（このパスは失敗）
        //     return false;
        // }

        // ピース定義リストから形状オブジェクトを探す
        PieceShape requiredShape = _availableShapes.FirstOrDefault(s => s.Name == requiredName);
        if (requiredShape == null)
        {
            Debug.LogError($"パターンシードに記載された形状名 '{requiredName}' が定義されていません。");
            return false;
        }

        // ★ 試行と再帰の実行 ★

        // 三角形セルの向きチェック (TryPlaceAndSolveと同様の処理が必要だが、ここではインラインで処理)
        if (CurrentShapeType == ShapeType.Triangle)
        {
            bool isUpSide = ((requiredOrigin.X + requiredOrigin.Y) % 2) == 0;
            if (requiredShape.IsUpSide == 2 && isUpSide) return false;
            if (requiredShape.IsUpSide == 1 && !isUpSide) return false;
        }
        // 六角形の位置チェック
        if (CurrentShapeType == ShapeType.Hex)
        {
            if (requiredShape.IsUpSide == 2 && requiredOrigin.X % 2 == 1) return false;
            if (requiredShape.IsUpSide == 1 && requiredOrigin.X % 2 == 0) return false;
        }

        if (CanPlace(requiredOrigin.X, requiredOrigin.Y, requiredShape))
        {
            // 1. 配置
            PlacePiece(requiredOrigin.X, requiredOrigin.Y, requiredShape);

            // // 2. ピースの使用フラグをセット
            // requiredShape.UseCount++;
            
            // 3. パターン復元モードの場合、インデックスを進める
            _placementIndex++;

            // 4. 次のセルへ再帰
            if (Solve(startX, startY, enforceCount, true)) // isPatternSeedActive = true で再帰
            {
                // Debug.Log($"CellSplitter.SolveWithPatternSeed:6:");
                return true; // 成功
            }

            // フォールバック=失敗で、次のパスで初期化するので後戻り処理は不要
            // 5. 後戻り (Backtrack)
            // RemovePiece(requiredOrigin.X, requiredOrigin.Y, requiredShape);
            // requiredShape.UseCount--; 

            // パターン復元モードの場合、インデックスを元に戻す
            // _placementIndex--;
        }

        // Debug.Log($"CellSplitter.SolveWithPatternSeed:7:");
        return false; // 配置できなかったか、配置後の再帰に失敗した
    }

    // CellSplitter クラス内に追加

    /// <summary>
    /// 1. 確率に基づいてセル数を抽選する (失敗済みのセル数は除外)
    /// </summary>
    private static int SelectCellCountByProbability(List<int> availableCellCounts, HashSet<int> failedCellCounts)
    {
        float totalWeight = 0f;
        foreach (var count in availableCellCounts)
        {
            if (!failedCellCounts.Contains(count) && CELL_COUNT_PROBABILITIES.ContainsKey(count))
            {
                totalWeight += CELL_COUNT_PROBABILITIES[count];
            }
        }

        if (totalWeight <= 0f) return -1; // 全てのセル数が失敗済みまたは定義されていない

        float r = (float)_random.NextDouble() * totalWeight;
        float cumulativeWeight = 0f;

        foreach (var count in availableCellCounts)
        {
            if (!failedCellCounts.Contains(count) && CELL_COUNT_PROBABILITIES.ContainsKey(count))
            {
                cumulativeWeight += CELL_COUNT_PROBABILITIES[count];
                if (r < cumulativeWeight)
                {
                    return count;
                }
            }
        }

        return -1; // 予期せぬエラー
    }

    /// <summary>
    /// 2. 指定セル数のピースから、そのグループ内での UseCount が最も少ないものを候補とする。
    ///    選出は同じセルサイズのピース群の中で完結する。
    /// </summary>
    private static List<PieceShape> GetPrioritizedCandidates(int cellCount, GridCoord currentOrigin)
    {
        // 指定されたセル数 (cellCount) のピースのリストを取得
        if (!_shapesByCellCount.ContainsKey(cellCount)) return new List<PieceShape>();

        List<PieceShape> pool = _shapesByCellCount[cellCount];
        
        // ★ 最小使用回数 (minUseCount) は、この pool（同じセルサイズのピース群）内でのみ計算する
        int minUseCount = int.MaxValue;
        List<PieceShape> viableShapes = new List<PieceShape>();

        foreach (var shape in pool)
        {
            // 0. 制約チェック
            if (0 <= shape.MaxUse && shape.MaxUse <= shape.UseCount) continue;
            
            // 配置可能性チェック（前回導入したヒューリスティック改善）
            if (!CanPlace(currentOrigin.X, currentOrigin.Y, shape)) continue;

            // ★ 1. UseCountが最小のものを比較（絶対値比較に戻す）
            if (shape.UseCount < minUseCount)
            {
                minUseCount = shape.UseCount;
                viableShapes.Clear();
                viableShapes.Add(shape);
            }
            else if (shape.UseCount == minUseCount)
            {
                viableShapes.Add(shape);
            }
        }

        // 候補リストをシャッフル
        ShuffleList(viableShapes, _random);
        
        return viableShapes;
    }

    /// <summary>
    /// 2. 試行済みのピースを除外し、残りの候補からランダムに一つ選ぶ
    /// </summary>
    private static PieceShape SelectRandomCandidate(List<PieceShape> candidates, HashSet<PieceShape> failedShapes)
    {
        var remainingCandidates = candidates.Where(s => !failedShapes.Contains(s)).ToList();
        
        if (remainingCandidates.Count == 0) return null;

        int index = _random.Next(remainingCandidates.Count);
        return remainingCandidates[index];
    }

    // 汎用シャッフル関数（Listの拡張メソッドとして定義することを推奨）
    private static void ShuffleList<T>(List<T> list, System.Random rng)
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

    // セル数のシャッフル関数 (ランダムな順序で抽選を行うための準備)
    private static List<int> GetShuffledCellCounts()
    {
        List<int> counts = CELL_COUNT_PROBABILITIES.Keys.ToList();
        // 確率抽選の前に、試行順序自体をランダムにするためにシャッフル
        ShuffleList(counts, _random); 
        return counts;
    }

    // ========== ユーティリティ関数 ==========

    /// <summary>
    /// 新仕様のパターンシードを解析し、ピースの配置順序と座標を復元する
    /// </summary>
    private static void AnalysisPatternSeed(string seed)
    {
        _isPatternSeedActive = false;
        if (string.IsNullOrEmpty(seed)) return;

        // ヘッダーが含まれているかチェック
        if (seed.Contains("="))
        {
            // --- 既存の完全再現モードの処理 ---
            string[] headerAndData = seed.Split('|');
            string[] headerParts = headerAndData[0].Split('=');
            
            if (headerParts.Length == 4 && 
                int.TryParse(headerParts[0], out int decodedX) && decodedX == GridX &&
                int.TryParse(headerParts[3], out int shapeInt) && (ShapeType)shapeInt == CurrentShapeType)
            {
                _isPatternSeedActive = true;
                _pieceNameSequence = new List<string>();
                _originCoordSequence = new List<GridCoord>();
                
                // TargetPieceCountをシードから復元
                if (int.TryParse(headerParts[2], out int pieceCount))
                {
                    TargetPieceCount = pieceCount;
                }
                
                // データ部からピース情報をパース
                for (int i = 1; i < headerAndData.Length; i++)
                {
                    string[] nameAndCoord = headerAndData[i].Split(':');
                    if (nameAndCoord.Length == 2)
                    {
                        string name = nameAndCoord[0];
                        string[] coords = nameAndCoord[1].Split(',');
                        if (coords.Length == 2 && 
                            int.TryParse(coords[0], out int x) && 
                            int.TryParse(coords[1], out int y))
                        {
                            _pieceNameSequence.Add(name);
                            _originCoordSequence.Add(new GridCoord(x, y));
                        }
                    }
                }
            }
        }
        else
        {
            // --- 新規：部分指定モードの処理 ---
            // ヘッダーがない場合、文字列をピース指定の羅列として扱う
            _pieceNameSequence = new List<string>();
            _originCoordSequence = new List<GridCoord>();
            
            // 先頭が'|'で始まっていてもいなくても分割できるように
            string[] pieceEntries = seed.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
            
            foreach (string entry in pieceEntries)
            {
                string[] nameAndCoord = entry.Split(':');
                if (nameAndCoord.Length == 2)
                {
                    string name = nameAndCoord[0];
                    string[] coords = nameAndCoord[1].Split(',');
                    if (coords.Length == 2 && int.TryParse(coords[0], out int x) && int.TryParse(coords[1], out int y))
                    {
                        _pieceNameSequence.Add(name);
                        _originCoordSequence.Add(new GridCoord(x, y));
                    }
                }
            }
            
            // ピースが一つでも取れればアクティブにする
            if (_pieceNameSequence.Count > 0)
            {
                _isPartialPatternSeedActive = true; 
                // ※この時、完全再現ではないのでフラグの扱いに注意（後述）
            }
        }
    }
    
    /// <summary>
    /// 成功した配置結果をパターンシード文字列にエンコードする
    /// </summary>
    private static string EncodePlacement(List<PieceRecord> placements)
    {
        // ヘッダー: "GX-GY-TC-Type"
        int shapeInt = (int)CurrentShapeType;
        string seed = $"{GridX}={GridY}={TargetPieceCount}={shapeInt}";
        
        // データ部: "|Name1:X1,Y1|Name2:X2,Y2|..."
        foreach (var record in placements)
        {
            seed += $"|{record.Shape.Name}:{record.Origin.X},{record.Origin.Y}";
        }
        
        return seed;
    }
    
    // =========================================================
    // 【追加関数】乱数によるシャッフル
    // =========================================================
    private static void ShuffleAvailableShapes()
    {
        int n = _availableShapes.Count;
        while (n > 1)
        {
            n--;
            // _random を使ってランダムなインデックス k を決定
            int k = _random.Next(n + 1);
            
            // 要素を交換
            PieceShape value = _availableShapes[k];
            _availableShapes[k] = _availableShapes[n];
            _availableShapes[n] = value;
        }
    }
    
    // ピースが(x, y)に配置可能かチェック (CanPlaceの修正)
    private static bool CanPlace(int originX, int originY, PieceShape shape)
    {
        // ★ ピースの最適シフトを計算
        GridCoord shift = CalculateOptimalShift(shape); 

        // ピースを構成するセルの位置
        foreach (var cell in shape.Cells)
        {
            // ★ シフトを適用した後の絶対座標
            int x = originX + cell.X + shift.X;
            int y = originY + cell.Y + shift.Y;
            
            // グリッド範囲外、またはすでに埋まっているセルと重複する場合は配置不可
            if (x < 0 || x >= GridX || y < 0 || y >= GridY || _grid[x, y] != 0)
            {
                return false;
            }
        }
        
        return true;
    }

    /// <summary>
    /// ピースをグリッドに配置（シフトを適用し、空きセル(originX, originY)を覆うように調整）
    /// </summary>
    private static void PlacePiece(int originX, int originY, PieceShape shape)
    {
        // ★ 1. ピースの最適シフトを計算
        GridCoord shift = CalculateOptimalShift(shape); 
        
        // ★ 2. シフト後の最終的な配置原点を決定
        int finalOriginX = originX + shift.X;
        int finalOriginY = originY + shift.Y;
        
        int id = _pieceIdCounter++;

        // 3. シフトを適用した後の絶対座標でグリッドを埋める
        foreach (var cell in shape.Cells)
        {
            // ★ finalOriginX/Y を使用
            _grid[finalOriginX + cell.X, finalOriginY + cell.Y] = id; 
        }
        
        // 4. ピースレコードには、実際に配置された原点（シフト後）を記録
        _successfulPlacements.Add(new PieceRecord 
        { 
            Shape = shape, 
            Origin = new GridCoord(finalOriginX, finalOriginY), // ★ シフト後の原点を記録
            PieceId = id 
        });
    }

    // RemovePiece メソッドの修正
    private static void RemovePiece(int originX, int originY, PieceShape shape)
    {
        if (_successfulPlacements != null && 1 <= _successfulPlacements.Count)
        {
            // 1. ピース配置リストから削除（この時、シフト後の座標が記録されている）
            // ここでの削除は、配置されたピースのRemovePieceで実行されるため、リストの末尾を削除します。
            // ただし、バックトラックのロジックを確実にするため、削除する前にそのピースが
            // 最後に配置されたピースであることを確認するのが安全ですが、ここではシンプルに削除します。
            _successfulPlacements.RemoveAt(_successfulPlacements.Count - 1);
        }
        // _pieceIdCounter--; // このカウンターのデクリメントは、グリッドクリア後に行う方が論理的だが、ここでは場所を変更しない

        // ★ グリッドをクリアする座標をシフト後のものに修正 ★
        GridCoord shift = CalculateOptimalShift(shape);
        int finalOriginX = originX + shift.X;
        int finalOriginY = originY + shift.Y;
        
        // グリッドをクリア
        foreach (var cell in shape.Cells)
        {
            // ★ finalOriginX/Y を使用
            _grid[finalOriginX + cell.X, finalOriginY + cell.Y] = 0;
        }
        _pieceIdCounter--;
    }

    /// <summary>
    /// 生成されたパターンシードが、避けるべきリストに含まれていないか確認する
    /// </summary>
    private static bool IsUniqueSeed(string newSeed, List<string> avoidSeeds)
    {
        return !avoidSeeds.Contains(newSeed);
    }
    
    /// <summary>
    /// GridX, GridY, TargetPieceCountの情報を埋め込んだ文字列シードを生成する
    /// </summary>
    public static string EncodeSeed(int gridX, int gridY, int targetCount, ShapeType shapeType)
    {
        // 乱数生成用の基となる数値シード（システム時間など）
        int numericPart = GetRandomIntSeed();

        // ShapeTypeをintにキャストしてシードに含める
        int shapeInt = (int)shapeType;
        
        // シード形式: "GX-GY-TC-Numeric"
        return $"{gridX}={gridY}={targetCount}={shapeInt}={numericPart}";
    }

    /// <summary>
    /// 暗号学的に強力なランダムな整数シードを生成する
    /// </summary>
    private static int GetRandomIntSeed()
    {
        // System.Randomの初期シードとして使える32bit整数を生成
        using (var rng = RandomNumberGenerator.Create())
        {
            byte[] bytes = new byte[4];
            rng.GetBytes(bytes);
            return BitConverter.ToInt32(bytes, 0);
        }
    }


    // ========== 枝刈りロジック関連 ==========

    // 最小ピースサイズを決定（現在のピース形状定義から最小セル数を持つピースのセル数を取得）
    private static int GetMinPieceSize()
    {
        if (_availableShapes == null || _availableShapes.Count == 0) return 1;
        
        // ピースリスト内の最小セル数を取得
        return _availableShapes.Min(s => s.Cells.Count);
    }
    
    // 現在のグリッド状態が実行可能か（孤立した空きセルがないか）をチェック
    private static bool IsFeasible()
    {
        int minSize = GetMinPieceSize();

        // 訪問済みの空きセルを追跡
        bool[,] visited = new bool[GridX, GridY];

        for (int y = 0; y < GridY; y++)
        {
            for (int x = 0; x < GridX; x++)
            {
                // 未訪問の空きセルを見つける
                if (_grid[x, y] == 0 && !visited[x, y])
                {
                    // 孤立した空き領域のサイズをBFS/DFSで計測
                    int areaSize = CountConnectedEmptyArea(x, y, visited);
                    
                    // 孤立領域のサイズが最小ピースサイズを下回る場合は、この状態は実行不可能と判断
                    if (areaSize < minSize)
                    {
                        return false; 
                    }
                }
            }
        }
        return true;
    }
    
    // (x, y)から繋がっている空き領域のセル数を数える (DFS/BFS)
    private static int CountConnectedEmptyArea(int startX, int startY, bool[,] visited)
    {
        int count = 0;
        Queue<GridCoord> queue = new Queue<GridCoord>();
        queue.Enqueue(new GridCoord(startX, startY));
        visited[startX, startY] = true;

        while (queue.Count > 0)
        {
            GridCoord current = queue.Dequeue();
            count++;

            // 隣接セルの座標オフセット (四角形グリッドの場合)
            GridCoord[] neighbors = new GridCoord[] {
                new GridCoord(1, 0), new GridCoord(-1, 0), 
                new GridCoord(0, 1), new GridCoord(0, -1)
                // 六角形や三角形の場合は、そのグリッドに応じた隣接セル定義に変更が必要
            };

            foreach (var offset in neighbors)
            {
                int nx = current.X + offset.X;
                int ny = current.Y + offset.Y;

                if (nx >= 0 && nx < GridX && ny >= 0 && ny < GridY && 
                    _grid[nx, ny] == 0 && !visited[nx, ny])
                {
                    visited[nx, ny] = true;
                    queue.Enqueue(new GridCoord(nx, ny));
                }
            }
        }
        return count;
    }

    // CellSplitter クラス内に追加/修正

    /// <summary>
    /// ピース形状が占有するセルのうち、最も左上のセルを (0, 0) に合わせるためのシフト座標を計算する。
    /// </summary>
    /// <returns>シフト座標 (GridCoord)</returns>
    // CalculateOptimalShift メソッドの修正
    private static GridCoord CalculateOptimalShift(PieceShape shape)
    {
        if (shape.Cells == null || shape.Cells.Count == 0) return new GridCoord(0, 0);

        // 1. シフトが必要かどうか（原点(0, 0)が含まれているか）を判断
        bool containsOrigin = shape.Cells.Any(c => c.X == 0 && c.Y == 0);
        
        // 原点が含まれていればシフトは不要
        if (containsOrigin)
        {
            return new GridCoord(0, 0);
        }
        
        // 2. 原点が含まれていない場合、最適なシフト量（最も左上のセルを(0, 0)に合わせる）を計算
        int minX = shape.Cells.Min(c => c.X);
        int minY = shape.Cells.Min(c => c.Y);
        
        GridCoord calculatedShift = new GridCoord(-minX, -minY);
        
        // ★ 3. 【重要】前回の修正で追加した対称性チェックのロジックを削除する
        //     ここでは常に計算されたシフト量を返す
        
        return calculatedShift;
    }

    /// <summary>
    /// 探索成功後、1セルピースを起点に隣接するピースと統合し、
    /// より大きな利用可能なピースに置き換える処理を網羅的に実行する。
    /// </summary>
    private static void MergeSmallPieces()
    {
        // 統合が成功する限りループ
        bool merged = true;
        while (merged)
        {
            merged = false;
            
            // 現在の配置リストから1セルピースのみを抽出
            var oneCellPieces = _successfulPlacements.Where(r => r.Shape.Cells.Count == 1).ToList();
            
            foreach (var targetRecord in oneCellPieces)
            {
                // 1. 対象の1セルピースと隣接するピースのレコードを取得
                var neighbors = GetAdjacentPieceRecords(targetRecord);
                
                // 2. 隣接ピースとの組み合わせ候補を作成し、チェック
                // 1セルピース + 1ピース（隣接）の統合を試行
                foreach (var neighborRecord in neighbors)
                {
                    // 1セルピース + 1セルピース = 2セルピース
                    // 1セルピース + 2セルピース = 3セルピース
                    // 統合候補のセル集合を取得
                    List<GridCoord> combinedCells = new List<GridCoord>();
                    combinedCells.AddRange(GetAbsoluteCells(targetRecord));
                    combinedCells.AddRange(GetAbsoluteCells(neighborRecord));

                    // 統合したセル集合に一致する利用可能な形状を探索
                    PieceShape integratedShape = FindMatchingShape(combinedCells);

                    if (integratedShape != null)
                    {
                        // 3. 置き換え実行
                        ReplacePieces(targetRecord, neighborRecord, integratedShape, combinedCells);
                        merged = true;
                        // Debug.Log($"<color=blue>ピース統合成功: {targetRecord.Shape.Name} ({targetRecord.PieceId}) + {neighborRecord.Shape.Name} ({neighborRecord.PieceId}) -> {integratedShape.Name}</color>");
                        
                        // 成功したらリストが変更されたため、再チェックのためにループを抜ける
                        goto NextOuterLoop; 
                    }
                }
            }
            
            NextOuterLoop:;
        }
    }

    // ========= ピース数維持モードの再分配ロジック ==========
    // ピース数を維持しつつ、1セルピースを減らすための再分配処理
    // 優先順位1: 1セル統合(-1) + 4セル以上を2分割(+1) = ピース数維持
    // 優先順位2: 1セル統合(-1) + 3セル以上2つを3分割(-2+3=+1) = ピース数維持
    private static void RebalancePieces()
    {
        // ピース数指定がない、または統合が必要な1セルピースがない場合は、
        // 従来の高速な統合処理だけを行って終了
        if (TargetPieceCount <= 0)
        {
            MergeSmallPieces();
            return;
        }
        Debug.Log($"<color=orange>ピース数維持モードでの再分配処理を開始します。現在の1セルピース数: {_successfulPlacements.Count(p => p.Shape.Cells.Count == 1)}</color>");
        // --- ピース数維持モード ---
        
        int maxIterations = 5; // 無限ループ防止
        int iteration = 0;
        
        while (iteration < maxIterations)
        {
            iteration++;
            
            // 1. 1セルピースをリストアップ
            var oneCellPieces = _successfulPlacements.Where(p => p.Shape.Cells.Count == 1).ToList();
            if (oneCellPieces.Count == 0) 
            {
                Debug.Log($"<color=green>再分配完了: 1セルピースが0個になりました</color>");
                return; // 1セルがなければ完了
            }

            // 2. 1セルピースを1つ選択
            var oneCell = oneCellPieces[0];
            
            // 3. 隣接する1セル以上のピースを探す（統合相手、1セル同士の統合も許容）
            var adjacentPieces = GetAdjacentPieceRecords(oneCell)
                .Where(r => r.Shape.Cells.Count >= 1)
                .OrderBy(r => r.Shape.Cells.Count) // 小さい方から試す（1セル同士を優先）
                .ToList();
            
            bool success = false;
            
            foreach (var adjacent in adjacentPieces)
            {
                // 4. 1セルピース + 隣接ピースの統合形状を探す
                List<GridCoord> oneCellCells = GetAbsoluteCells(oneCell);
                List<GridCoord> adjacentCells = GetAbsoluteCells(adjacent);
                List<GridCoord> mergedCells = new List<GridCoord>(oneCellCells);
                mergedCells.AddRange(adjacentCells);
                
                PieceShape mergedShape = FindMatchingShape(mergedCells);
                if (mergedShape == null)
                { 
                    Debug.Log($"<color=gray>再分配候補スキップ: [{oneCell.Shape.Name}+{adjacent.Shape.Name}]の統合形状が見つかりません</color>");
                    continue;
                }
                else
                {
                    Debug.Log($"<color=gray>再分配候補発見: [{oneCell.Shape.Name}+{adjacent.Shape.Name}]の統合形状 [{mergedShape.Name}] を発見</color>");
                }
                
                // 5. 別の場所で分割可能な大きなピース（4セル以上）を探す
                // ※3セルは2つの2セル以上に分割できないため除外
                var splittablePieces = _successfulPlacements
                    .Where(p => p.Shape.Cells.Count >= 4 && 
                                p.PieceId != oneCell.PieceId && 
                                p.PieceId != adjacent.PieceId)
                    .OrderByDescending(p => p.Shape.Cells.Count) // 大きい方から試す
                    .ToList();
                
                Debug.Log($"<color=cyan>再分配: 1セル[{oneCell.PieceId}]+隣接[{adjacent.PieceId}({adjacent.Shape.Cells.Count}セル)]の統合候補。4セル以上のピース数: {splittablePieces.Count}</color>");
                 
                foreach (var splittable in splittablePieces)
                {
                    // 6. 分割を試みる（2つの2セル以上のピースに分割）
                    var splitResult = TrySplitPieceIntoTwo(splittable);
                    Debug.Log($"<color=gray>再分配候補分割試行: [{splittable.Shape.Name}({splittable.PieceId})]の分割を試行</color>: splitResult={(splitResult != null ? "成功" : "失敗")}");
                    if (splitResult != null)
                    {
                        // 7. 両方の操作を実行
                        var (shape1, shape2, cells1, cells2) = splitResult.Value;
                        
                        // 7-1. 1セルピース + 隣接ピースを統合
                        ReplacePieces(oneCell, adjacent, mergedShape, mergedCells);
                        
                        // 7-2. 別のピースを2つに分割
                        ReplacePieceWithSplit(splittable, shape1, shape2, cells1, cells2);
                        
                        Debug.Log($"<color=green>再分配成功(分割): [{oneCell.Shape.Name}+{adjacent.Shape.Name}→{mergedShape.Name}] & [{splittable.Shape.Name}→{shape1.Name}+{shape2.Name}]</color>");
                        success = true;
                        break;
                    }
                }
                
                if (success) break;
                
                // フォールバック: 4セル以上のピースが見つからない場合、3セル以上のピース2つを3つに再分割してピース数維持
                // 1セル統合(-1) + 2つのピース→3つのピース(-2+3=+1) = ピース数維持
                if (!success)
                {
                    // 3セル以上のピースのペアを探す（隣接している必要はない）
                    var redistributablePieces = _successfulPlacements
                        .Where(p => p.Shape.Cells.Count >= 3 && 
                                    p.PieceId != oneCell.PieceId && 
                                    p.PieceId != adjacent.PieceId)
                        .OrderByDescending(p => p.Shape.Cells.Count)
                        .ToList();
                    
                    for (int i = 0; i < redistributablePieces.Count && !success; i++)
                    {
                        for (int j = i + 1; j < redistributablePieces.Count && !success; j++)
                        {
                            var piece1 = redistributablePieces[i];
                            var piece2 = redistributablePieces[j];
                            
                            // 2つのピースを統合→ちょうど3つに分割（ピース数維持のため）
                            var redistributeResult = TryMergeAndSplitIntoThree(piece1, piece2);
                            if (redistributeResult != null)
                            {
                                // 8. 全ての操作を実行
                                // 8-1. 1セルピース + 隣接ピースを統合
                                ReplacePieces(oneCell, adjacent, mergedShape, mergedCells);
                                
                                // 8-2. 2つのピースを削除して、3つの新しいピースに置き換え
                                ReplaceTwoPiecesWithMultiple(piece1, piece2, redistributeResult);
                                
                                Debug.Log($"<color=green>再分配成功(3分割): [{oneCell.Shape.Name}+{adjacent.Shape.Name}→{mergedShape.Name}] & [{piece1.Shape.Name}+{piece2.Shape.Name}→3個のピース]</color>");
                                success = true;
                                break;
                            }
                        }
                    }
                }
                
                if (success) break;
            }
            
            if (!success)
            {
                Debug.Log($"<color=yellow>再分配失敗: 残り1セルピース {oneCellPieces.Count}個を処理できませんでした</color>");
                // break; // これ以上処理できないので終了
            }
        }
        
        if (iteration >= maxIterations)
        {
            Debug.LogWarning("RebalancePieces: 最大反復回数に達しました");
        }
    }
    
    /// <summary>
    /// 2つのピースを統合してちょうど3つの2セル以上のピースに分割できるか試行する（ピース数維持のため）
    /// 成功した場合: (Shape, Cells)のリスト（要素数3）を返す
    /// 失敗した場合: null を返す
    /// </summary>
    private static List<(PieceShape, List<GridCoord>)> TryMergeAndSplitIntoThree(PieceRecord piece1, PieceRecord piece2)
    {
        // 2つのピースを統合
        List<GridCoord> allCells = new List<GridCoord>();
        allCells.AddRange(GetAbsoluteCells(piece1));
        allCells.AddRange(GetAbsoluteCells(piece2));
        
        int totalCells = allCells.Count;
        
        // 最低でも6セル必要（2+2+2）
        if (totalCells < 6) return null;
        
        // 3つ以上のグループに分割する全パターンを試す
        // まず3分割を試行
        for (int pattern1 = 1; pattern1 < (1 << totalCells) - 1; pattern1++)
        {
            for (int pattern2 = 1; pattern2 < (1 << totalCells) - 1; pattern2++)
            {
                // pattern1とpattern2が重複しないか、かつ全てのセルをカバーするかチェック
                if ((pattern1 & pattern2) != 0) continue; // 重複あり
                int pattern3 = ((1 << totalCells) - 1) ^ pattern1 ^ pattern2;
                if (pattern3 == 0) continue; // グループ3が空
                
                List<GridCoord> group1 = new List<GridCoord>();
                List<GridCoord> group2 = new List<GridCoord>();
                List<GridCoord> group3 = new List<GridCoord>();
                
                for (int i = 0; i < totalCells; i++)
                {
                    if ((pattern1 & (1 << i)) != 0)
                        group1.Add(allCells[i]);
                    else if ((pattern2 & (1 << i)) != 0)
                        group2.Add(allCells[i]);
                    else
                        group3.Add(allCells[i]);
                }
                
                // 各グループが2セル以上必要
                if (group1.Count < 2 || group2.Count < 2 || group3.Count < 2) continue;
                
                // 各グループが連結しているか確認
                if (!IsConnected(group1) || !IsConnected(group2) || !IsConnected(group3)) continue;
                
                // 各グループが利用可能な形状と一致するか確認
                PieceShape shape1 = FindMatchingShape(group1);
                PieceShape shape2 = FindMatchingShape(group2);
                PieceShape shape3 = FindMatchingShape(group3);
                
                if (shape1 != null && shape2 != null && shape3 != null)
                {
                    return new List<(PieceShape, List<GridCoord>)>
                    {
                        (shape1, group1),
                        (shape2, group2),
                        (shape3, group3)
                    };
                }
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// 2つのピースを削除して、複数の新しいピースに置き換える
    /// </summary>
    private static void ReplaceTwoPiecesWithMultiple(PieceRecord piece1, PieceRecord piece2, 
                                                     List<(PieceShape, List<GridCoord>)> newPieces)
    {
        // 1. 古い2つのピースをグリッドから削除
        List<GridCoord> oldCells1 = GetAbsoluteCells(piece1);
        List<GridCoord> oldCells2 = GetAbsoluteCells(piece2);
        
        foreach (var cell in oldCells1)
        {
            _grid[cell.X, cell.Y] = 0;
        }
        foreach (var cell in oldCells2)
        {
            _grid[cell.X, cell.Y] = 0;
        }
        
        // 2. _successfulPlacementsから古いピースを削除
        _successfulPlacements.RemoveAll(p => p.PieceId == piece1.PieceId || p.PieceId == piece2.PieceId);
        
        // 3. 新しいピースを全て配置
        foreach (var (shape, cells) in newPieces)
        {
            _pieceIdCounter++;
            GridCoord origin = GetOriginCoord(cells);
            
            foreach (var cell in cells)
            {
                _grid[cell.X, cell.Y] = _pieceIdCounter;
            }
            
            _successfulPlacements.Add(new PieceRecord 
            { 
                Shape = shape, 
                Origin = origin, 
                PieceId = _pieceIdCounter 
            });
            shape.UseCount++;
        }
    }
    
    /// <summary>
    /// ピースを2つの2セル以上のピースに分割できるか試行する
    /// 成功した場合: (Shape1, Shape2, Cells1, Cells2) のタプルを返す
    /// 失敗した場合: null を返す
    /// </summary>
    private static (PieceShape, PieceShape, List<GridCoord>, List<GridCoord>)? TrySplitPieceIntoTwo(PieceRecord piece)
    {
        List<GridCoord> allCells = GetAbsoluteCells(piece);
        int totalCells = allCells.Count;
        
        // 最低でも4セル必要（2+2）
        if (totalCells < 4) return null;
        
        // 全ての可能な分割パターンを試す
        // ビット演算で全パターンを生成（0以外かつ全てのセルを含まないパターン）
        int maxPattern = (1 << totalCells) - 1;
        
        for (int pattern = 1; pattern < maxPattern; pattern++)
        {
            List<GridCoord> group1 = new List<GridCoord>();
            List<GridCoord> group2 = new List<GridCoord>();
            
            for (int i = 0; i < totalCells; i++)
            {
                if ((pattern & (1 << i)) != 0)
                    group1.Add(allCells[i]);
                else
                    group2.Add(allCells[i]);
            }
            
            // 各グループが2セル以上必要
            if (group1.Count < 2 || group2.Count < 2) continue;
            
            // 各グループが連結しているか確認
            if (!IsConnected(group1) || !IsConnected(group2)) continue;
            
            // 各グループが利用可能な形状と一致するか確認
            PieceShape shape1 = FindMatchingShape(group1);
            PieceShape shape2 = FindMatchingShape(group2);
            
            if (shape1 != null && shape2 != null)
            {
                return (shape1, shape2, group1, group2);
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// セルのリストが連結しているか確認（BFS）
    /// </summary>
    private static bool IsConnected(List<GridCoord> cells)
    {
        if (cells.Count == 0) return false;
        if (cells.Count == 1) return true;
        
        HashSet<GridCoord> cellSet = new HashSet<GridCoord>(cells);
        HashSet<GridCoord> visited = new HashSet<GridCoord>();
        Queue<GridCoord> queue = new Queue<GridCoord>();
        
        // 最初のセルから開始
        queue.Enqueue(cells[0]);
        visited.Add(cells[0]);
        
        while (queue.Count > 0)
        {
            GridCoord current = queue.Dequeue();
            
            // 隣接セルを探索
            GridCoord[] neighbors = GetNeighborOffsets(CurrentShapeType, current.X, current.Y);
            foreach (var offset in neighbors)
            {
                GridCoord neighbor = new GridCoord(current.X + offset.X, current.Y + offset.Y);
                
                if (cellSet.Contains(neighbor) && !visited.Contains(neighbor))
                {
                    visited.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }
        }
        
        return visited.Count == cells.Count;
    }
    
    /// <summary>
    /// ピースを2つの新しいピースで置き換える
    /// </summary>
    private static void ReplacePieceWithSplit(PieceRecord oldPiece, 
                                              PieceShape shape1, PieceShape shape2,
                                              List<GridCoord> cells1, List<GridCoord> cells2)
    {
        // 1. 古いピースをグリッドから削除
        List<GridCoord> oldCells = GetAbsoluteCells(oldPiece);
        foreach (var cell in oldCells)
        {
            _grid[cell.X, cell.Y] = 0;
        }
        
        // 2. _successfulPlacementsから古いピースを削除
        _successfulPlacements.RemoveAll(p => p.PieceId == oldPiece.PieceId);
        
        // 3. 新しいピース1を配置
        _pieceIdCounter++;
        GridCoord origin1 = GetOriginCoord(cells1);
        foreach (var cell in cells1)
        {
            _grid[cell.X, cell.Y] = _pieceIdCounter;
        }
        _successfulPlacements.Add(new PieceRecord 
        { 
            Shape = shape1, 
            Origin = origin1, 
            PieceId = _pieceIdCounter 
        });
        shape1.UseCount++;
        
        // 4. 新しいピース2を配置
        _pieceIdCounter++;
        GridCoord origin2 = GetOriginCoord(cells2);
        foreach (var cell in cells2)
        {
            _grid[cell.X, cell.Y] = _pieceIdCounter;
        }
        _successfulPlacements.Add(new PieceRecord 
        { 
            Shape = shape2, 
            Origin = origin2, 
            PieceId = _pieceIdCounter 
        });
        shape2.UseCount++;
    }
    
    /// <summary>
    /// セルリストの原点座標（最小X、最小Y）を取得
    /// </summary>
    private static GridCoord GetOriginCoord(List<GridCoord> cells)
    {
        int minX = cells.Min(c => c.X);
        int minY = cells.Min(c => c.Y);
        return new GridCoord(minX, minY);
    }
    // ========== ユーティリティ (GetAdjacentPieceRecords, GetAbsoluteCells, FindMatchingShape が必要) ==========

    // ターゲットピースレコードに隣接するピースレコードを取得
    private static List<PieceRecord> GetAdjacentPieceRecords(PieceRecord targetRecord)
    {
        // ターゲットピースの全セルを取得（絶対座標）
        List<GridCoord> targetCells = GetAbsoluteCells(targetRecord);
        
        // 隣接するピースレコードを重複なく格納
        HashSet<PieceRecord> adjacentPieces = new HashSet<PieceRecord>();

        // ターゲットのピースID
        int targetId = targetRecord.PieceId;
        
        // ターゲットピースの全てのセルについて処理
        foreach (var cell in targetCells)
        {
            // グリッド形状に応じた隣接セルのオフセットを取得
            GridCoord[] neighbors = GetNeighborOffsets(CurrentShapeType, cell.X, cell.Y);

            foreach (var offset in neighbors)
            {
                int nx = cell.X + offset.X;
                int ny = cell.Y + offset.Y;
                
                // 隣接するセルのピースレコードを取得
                PieceRecord neighborRecord = GetPieceRecordToGridCoord(nx, ny);

                // 1. レコードが有効である (ピースが存在する)
                // 2. ターゲットピース自身ではない
                if (neighborRecord.PieceId != 0 && neighborRecord.PieceId != targetId)
                {
                    adjacentPieces.Add(neighborRecord);
                }
            }
        }

        return adjacentPieces.ToList();
    }

    /// <summary>
    /// 指定座標に配置されているピースのレコードを取得する。
    /// 範囲外または空きセルの場合は、PieceId=0のデフォルト値を返す。
    /// </summary>
    private static PieceRecord GetPieceRecordToGridCoord(int x, int y)
    {
        // 範囲外チェック
        if (x < 0 || x >= GridX || y < 0 || y >= GridY)
        {
            return default; // PieceId=0で返る
        }

        int id = _grid[x, y];
        if (id == 0)
        {
            return default; // PieceId=0で返る
        }
        
        // _successfulPlacementsはPieceId=1から始まるため、Findで探索
        PieceRecord piece = _successfulPlacements.Find(value => value.PieceId == id);
        
        // ピースが見つからなければデフォルト値（異常系）
        return piece; 
    }

    // 形状と座標に応じた隣接オフセットを取得するユーティリティ（GetAdjacentPieceRecordsで使用）
    private static GridCoord[] GetNeighborOffsets(ShapeType type, int x, int y)
    {        
        // 六角形グリッドの場合の隣接セル（X座標の偶奇でYオフセットが変わる）
        if (type == ShapeType.Hex)
        {
            if (x % 2 == 0) // Xが偶数
            {
                return new GridCoord[] {
                    new GridCoord(1, 0), new GridCoord(-1, 0), // 左右
                    new GridCoord(0, 1), new GridCoord(0, -1), // 上下
                    new GridCoord(1, -1), new GridCoord(-1, -1) // 斜め下
                };
            }
            else // Xが奇数
            {
                return new GridCoord[] {
                    new GridCoord(1, 0), new GridCoord(-1, 0), // 左右
                    new GridCoord(0, 1), new GridCoord(0, -1), // 上下
                    new GridCoord(1, 1), new GridCoord(-1, 1) // 斜め上
                };
            }
        }
        // 三角形グリッドの場合の隣接セル（未実装の場合は四角形と同じものを暫定的に返すか、要定義）
        else if (type == ShapeType.Triangle)
        {   
            // 辺が上向き(下向き三角形)
            if ((x + y) % 2 == 0)
            {
                return new GridCoord[] {
                    new GridCoord(1, 0), new GridCoord(-1, 0), 
                    new GridCoord(0, 1), new GridCoord(0, -1),
                    new GridCoord(-2, 1), new GridCoord(2, 1)
                };
            }
            // 辺が下向き(上向き三角形)
            else
            {
                return new GridCoord[] {
                    new GridCoord(1, 0), new GridCoord(-1, 0), 
                    new GridCoord(0, 1), new GridCoord(0, -1),
                    new GridCoord(-2, -1), new GridCoord(2, -1)
                };
            }
        }
        // 四角形グリッドの場合の隣接セル 
        return new GridCoord[] {
                new GridCoord(1, 0), new GridCoord(-1, 0), 
                new GridCoord(0, 1), new GridCoord(0, -1),
                new GridCoord(1, 1), new GridCoord(-1, 1), 
                new GridCoord(-1, 1), new GridCoord(-1, -1)
            };
    }

    // ピースレコードから絶対座標リストを取得（再掲、MergeSmallPiecesからGetAbsoluteCellsに変更）
    private static List<GridCoord> GetAbsoluteCells(PieceRecord record)
    {
        return record.Shape.Cells.Select(c => new GridCoord(record.Origin.X + c.X, record.Origin.Y + c.Y)).ToList();
    }

    // セル集合に一致する利用可能な形状を見つける（IsUpSide制約も考慮）
    private static PieceShape FindMatchingShape(List<GridCoord> combinedCells)
    {
        if (combinedCells == null || combinedCells.Count == 0) return null;

        // 1. セル数をチェックし、同じセル数のプールに絞り込む（高速化）
        int targetCount = combinedCells.Count;
        if (!_shapesByCellCount.ContainsKey(targetCount)) return null;
        
        // 2. 統合されたセル集合を正規化（絶対座標を相対座標に変換）
        //    -> 最も左上（Min X, Min Y）のセルを (0, 0) にシフト
        int minX = combinedCells.Min(c => c.X);
        int minY = combinedCells.Min(c => c.Y);
        
        List<GridCoord> normalizedCells = combinedCells
            .Select(c => new GridCoord(c.X - minX, c.Y - minY))
            .OrderBy(c => c.Y) // 比較のために安定した順序にソート
            .ThenBy(c => c.X)
            .ToList();

        // 3. 候補のピース形状と一つずつ比較
        foreach (var shape in _shapesByCellCount[targetCount])
        {
            // 形状リスト内のピース形状も正規化（既にされているはずだが念のため）
            // PieceShapeのCellsリストは静的コンストラクタで定義されているため、ソートするだけでよい
            List<GridCoord> targetShapeCells = shape.Cells
                .OrderBy(c => c.Y)
                .ThenBy(c => c.X)
                .ToList();

            // セル数が一致し、座標が一つ一つ一致するかチェック
            if (targetShapeCells.Count == normalizedCells.Count)
            {
                bool shapeMatch = true;
                for (int i = 0; i < targetCount; i++)
                {
                    if (targetShapeCells[i].X != normalizedCells[i].X || 
                        targetShapeCells[i].Y != normalizedCells[i].Y)
                    {
                        shapeMatch = false;
                        break;
                    }
                }

                if (shapeMatch)
                {
                    // 4. IsUpSide制約チェック
                    if (IsUpSideCheckPassed(minX, minY, shape))
                    {
                         // 見つかったピース形状のUseCountを増やしすぎないよう、ここでMaxUseチェック
                        if (shape.MaxUse == -1 || shape.UseCount < shape.MaxUse)
                        {
                            return shape; // 一致する形状が見つかった
                        }
                    }
                }
            }
        }

        return null; // 一致する形状は見つからなかった
    }

    // IsUpSideの制約を満たしているかチェックするユーティリティ
    private static bool IsUpSideCheckPassed(int originX, int originY, PieceShape shape)
    {
        if (CurrentShapeType == ShapeType.Square) return true; // 四角形は常にOK

        if (CurrentShapeType == ShapeType.Triangle)
        {
            bool isUpSide = ((originX + originY) % 2) == 0;
            if (shape.IsUpSide == 2 && isUpSide) return false; // 下向き指定なのに上向き
            if (shape.IsUpSide == 1 && !isUpSide) return false; // 上向き指定なのに下向き
        }
        else if (CurrentShapeType == ShapeType.Hex)
        {
            if (shape.IsUpSide == 2 && originX % 2 == 1) return false; // X奇数用指定なのにX偶数
            if (shape.IsUpSide == 1 && originX % 2 == 0) return false; // X偶数用指定なのにX奇数
        }
        
        return true;
    }

    // ピースの置き換えを実行し、成功リストとグリッドを更新
    private static void ReplacePieces(PieceRecord rec1, PieceRecord rec2, PieceShape newShape, List<GridCoord> newCells)
    {
        // 1. グリッドから元のピースのIDを0に戻す
        //    rec1とrec2の全セルに対して、グリッドのIDを0に戻します。
        
        // 元のIDを取得（RemovePieceを使わない方が安全）
        int id1 = rec1.PieceId;
        int id2 = rec2.PieceId;

        // グリッドをクリア（新しいセルの座標リストnewCellsは絶対座標）
        foreach (var cell in newCells)
        {
            // 配置されているのがrec1かrec2のIDでなければ、ロジックエラーの可能性があるが、
            // 統合チェックが成功している前提なので単純にクリア
            _grid[cell.X, cell.Y] = 0;
        }

        // 2. _successfulPlacementsからrec1とrec2を削除
        _successfulPlacements.Remove(rec1);
        _successfulPlacements.Remove(rec2);
        
        // 3. 新しいピース（newShape）を配置
        
        // 新しいピースの配置原点（正規化された原点）を取得
        // FindMatchingShapeで使われた minX/minY が、新しいピースの最適な原点になります
        int minX = newCells.Min(c => c.X);
        int minY = newCells.Min(c => c.Y);

        // 新しいピースのIDをインクリメント（PlacePieceロジックを流用せず、最小限の処理で）
        int newId = _pieceIdCounter++;
        
        // グリッドに新しいIDを書き込み
        foreach (var cell in newShape.Cells) // newShape.Cellsは相対座標
        {
            _grid[minX + cell.X, minY + cell.Y] = newId;
        }

        // 4. 成功リストに追加
        _successfulPlacements.Add(new PieceRecord 
        { 
            Shape = newShape, 
            Origin = new GridCoord(minX, minY), // 配置されたシフト後の原点
            PieceId = newId 
        });

        // 5. 新しいピースのUseCountをインクリメント
        newShape.UseCount++;
        
        // 統合されたピースの元々のUseCountはリセットすべきだが、ここでは単純にインクリメント
        // 元のピースのUseCountはそのまま残るが、もう使われないため無視できる
    }
}