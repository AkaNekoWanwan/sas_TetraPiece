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

    // ピースの最大セル数 (枝刈り、ユーティリティ用に利用)
    private static int _maxCellCount = 0;

    // ★ 静的コンストラクタで一度だけ初期化
    static CellSplitter()
    {
        Debug.Log($"CellSplitter：ピース初期化！！");
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
        TRIANGLE_SHAPES.Add(new PieceShape("I2-B", new List<GridCoord> { new GridCoord(0, 0), new GridCoord(2, 1) }, -1, 1)); // /♾️
        TRIANGLE_SHAPES.Add(new PieceShape("I2-B", new List<GridCoord> { new GridCoord(0, 1), new GridCoord(2, 0) }, -1, 1)); // \♾️


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
        TRIANGLE_SHAPES.Add(new PieceShape("<4-A", new List<GridCoord> { new GridCoord(0, 0), new GridCoord(0, 1), new GridCoord(1, 0), new GridCoord(1, 1) }, -1, 2));

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

    public static void CellSplit( int cols, int rows, ref int orderPieceNum, List<AnswerGridPos> cells, GridPieceListController gridPieceListController, ShapeType type, string patternSeed = null, List<string> avoidPatternSeeds = null )
    {
        // ピース生成のパラメーターセット
        GridX = cols;
        GridY = rows;
        TargetPieceCount = orderPieceNum;
        CurrentShapeType = type; // ★ ここでShapeTypeを保持する
        PatternSeed = patternSeed; // パターンシードを保持

        // 乱数生成器の数値シードを決定 (ランダム探索の再現性用。パターン再現とは別)
        _randomSeed = GetRandomIntSeed(); // 新しい乱数シード生成関数を使用
        _random = new System.Random(_randomSeed);

        avoidPatternSeeds = null;

        // 1. ピース形状の定義を取得
        SetAvailableShapes();
        GroupShapesByCellCount();
        // 2. シード値の決定と解析 (PatternSeedのデコード)
        AnalysisPatternSeed(patternSeed);
        // ピース情報の生成
        CreatePiecePlacements(patternSeed, avoidPatternSeeds);

        // 作成したピース情報をもとにピースオブジェクトに反映させる
        // コントローラーの前準備
        orderPieceNum = _successfulPlacements.Count;
        gridPieceListController.pieceNum = orderPieceNum;
        bool backupFlg = gridPieceListController.isOverrayPieceNum;
        gridPieceListController.isOverrayPieceNum = false;
        gridPieceListController.PreSetPieceDragControllers();
        gridPieceListController.isOverrayPieceNum = backupFlg;
        List<PieceDragController> pieceList = gridPieceListController.gameObject.GetComponentsInChildren<PieceDragController>().ToList();
        
        // セルを対応するピースの子オブジェクトにする
        RegisterCellsAsPieces(pieceList, cells);
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

            Debug.Log($"Debug:{i}, Count:{_successfulPlacements.Count}, cellNum:{Cells.Count}, shapeName:{cellsInfo.Shape.Name}");

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
                    cell.transform.parent = piece;
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
    }

    // 指定のX,Yのセルを見つける
    private static AnswerGridPos FindCell(List<AnswerGridPos> cells, int searchX, int searchY)
    {
        AnswerGridPos cell = cells.FirstOrDefault(c => c.x == searchX && c.y == searchY);
        return cell;
    }

    private static void SetAvailableShapes()
    {
        switch (CurrentShapeType)
        {
            case ShapeType.Square:
            default:
                _availableShapes = new List<PieceShape>(SQUARE_SHAPES);
                break;
            case ShapeType.Hex:
                _availableShapes = new List<PieceShape>(HEX_SHAPES);
                break;
            case ShapeType.Triangle:
                _availableShapes = new List<PieceShape>(TRIANGLE_SHAPES);
                break;
        }
    }

    private static void CreatePiecePlacements(string patternSeed = null, List<string> avoidPatternSeeds = null)
    {
        bool success = false;
        bool isRandom = true;

        // =========================================================
        // 第1パス: 受け取ったパターンシードのデコードと強制再現
        // =========================================================
        if (_isPatternSeedActive)
        {
            PreSolve();
            Debug.Log($"--- 第1パス開始: パターンシードの再現 ---");
            success = Solve(0, 0, true, true);
            isRandom = false;
        }

        // =========================================================
        // 第2パス以降: ランダム探索（ユニーク性保証付き）
        // =========================================================
        if (!success)
        {
            const int MAX_UNIQUE_ATTEMPTS = 5; // ユニーク生成の試行回数上限
            List<string> _avoidSeeds = avoidPatternSeeds ?? new List<string>();
            
            for(int attempt = 0; attempt < MAX_UNIQUE_ATTEMPTS; attempt++)
            {
                // ユニーク探索のために、毎回異なる数値シードで乱数生成器をリセット
                _randomSeed = GetRandomIntSeed();
                _random = new System.Random(_randomSeed);

                // 探索パスの優先度順に試行
                bool currentAttemptSuccess = false;
                
                // 試行1: ターゲットピース数厳守・形状ユニーク
                if( 0 < TargetPieceCount )
                {
                    PreSolve();
                    if (Solve(0, 0, true)) currentAttemptSuccess = true;
                }
                
                // 試行2: ピース数無視
                if (!currentAttemptSuccess)
                {
                    PreSolve();
                    _grid = new int[GridX, GridY];
                    for(int x = 0; x < GridX; x++)
                    {
                        for(int y = 0; y < GridY; y++)
                        {
                            Debug.Log($"_grid[{x},{y}]={_grid[x, y]}");
                        }
                    }
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
                        Debug.Log($"<color=green>第{attempt + 2}パス成功 (ユニーク)！</color>");
                        break; // ループを抜けて成功
                    }
                    else
                    {
                        Debug.LogWarning($"生成されたパターンは既知のシードと重複しました。再試行します (試行回数: {attempt + 1})");
                    }
                }
            }
            
            if (!success)
            {
                Debug.LogError($"ランダム探索 ({MAX_UNIQUE_ATTEMPTS}回試行) でユニークなパターンの生成に失敗しました。");
            }
        }
        
        if (success)
        {
            Debug.Log($"<color=green>敷き詰め完了！</color> 最終ピース数: {_pieceIdCounter - 1}, 使用パターンシード: {PatternSeed}");
            if(isRandom)
            {
                // ランダム探索で成功した場合、新しいパターンシードを生成・更新
                PatternSeed = EncodePlacement(_successfulPlacements);
            }
        }
        else
        {
            Debug.LogError($"全パス失敗。グリッドサイズ ({GridX}x{GridY}) は敷き詰め不可能です。");
        }
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


    // ========== ピース形状の定義例（ポリオミノ） ==========
    // 四角形セル用
    private static void InitializeShapesSquare()
    {

    }
    // 六角形セル用
    private static void InitializeShapesHex()
    {
        
    }
    // 三角形セル用
    private static void InitializeShapesTriangle()
    {

    }

    private static int SubMaxUse(int value, int subNum)
    {
        int ret = Mathf.Max(value - subNum, 0);
        return ret;
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
        List<PieceShape> shapesToTry;
        Debug.Log($"CellSplitter:1:{_availableShapes.Count}, startX:{startX}, startY:{startY}");
        // Count厳守モードで、ピース数が上限を超えた場合は失敗 (早期終了)
        // 終了条件: すべてのセルが埋まった
        if (!FindNextEmptyCell(out startX, out startY))
        {
            if (enforceCount && _pieceIdCounter - 1 != TargetPieceCount) return false;
            // ランダム探索で成功した場合、CreatePiecePlacementsでPatternSeedを更新する
            return true;
        }
        Debug.Log($"CellSplitter:2, startX:{startX}, startY:{startY}");
        // ★ リファクタリング箇所: パターン復元モードの処理を専用関数に委譲
        if (isPatternSeedActive)
        {
            return SolveWithPatternSeed(startX, startY, enforceCount);
        }
        // ★ 1. ランダム探索モード: 抽選ロジックを実装 ★
        // セル数とその抽選確率に基づいて、試行するセル数の順序リストを生成
        List<int> availableCellCounts = GetShuffledCellCounts();

        Debug.Log($"CellSplitter:3:{availableCellCounts.Count}, startX:{startX}, startY:{startY}");
        
        // 試行済みだが失敗したセル数を除外するためのセット
        HashSet<int> failedCellCounts = new HashSet<int>();
        
        while(failedCellCounts.Count < availableCellCounts.Count)
        {
            // 1. 確率に基づいてセル数を抽選 (失敗済みのセル数は除外)
            int selectedCellCount = SelectCellCountByProbability(availableCellCounts, failedCellCounts);
            Debug.Log($"CellSplitter:4:{selectedCellCount}, startX:{startX}, startY:{startY}");
            if (selectedCellCount == -1) // 抽選対象が残っていない
            {
                break;
            }
            Debug.Log($"CellSplitter:5:{selectedCellCount}, startX:{startX}, startY:{startY}");
            // 2. 指定のセル数のピースから UseCount が最も少ないものから優先して選択
            //    (同UseCount内ではランダムに試行)
            List<PieceShape> candidates = GetPrioritizedCandidates(selectedCellCount);

            // 試行済みだが失敗したピースを除外するためのセット（この再帰レベル内のみ）
            HashSet<PieceShape> failedShapes = new HashSet<PieceShape>();
            
            // 3. 候補ピースを全て試行 (バックトラック処理)
            while(failedShapes.Count < candidates.Count)
            {
                // UseCountが最も少ないグループの中からランダムに選択
                PieceShape shape = SelectRandomCandidate(candidates, failedShapes);
                Debug.Log($"CellSplitter:6:tryGetShape, startX:{startX}, startY:{startY}");
                
                if (shape == null) break; // 選択対象なし (本来はcandidates.Count == failedShapes.Countでループ終了)
                Debug.Log($"CellSplitter:7:{shape.Name}, startX:{startX}, startY:{startY}");
                GridCoord origin = new GridCoord(startX, startY);

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

    // ※ TryPlaceAndSolve メソッドは、Solveのコアロジックを抽出したものです
    private static bool TryPlaceAndSolve(GridCoord origin, PieceShape shape, bool enforceCount)
    {
        // MaxUse制約チェック
        if (0 <= shape.MaxUse && shape.MaxUse <= shape.UseCount) return false;
        
        // 三角形セルの向きチェック
        if (CurrentShapeType == ShapeType.Triangle)
        {
            bool isUpSide = ((origin.X + origin.Y) % 2) == 0;
            if (shape.IsUpSide == 2 && isUpSide) return false;
            if (shape.IsUpSide == 1 && !isUpSide) return false;
        }
        // 六角形の位置チェック
        if (CurrentShapeType == ShapeType.Hex)
        {
            if (shape.IsUpSide == 2 && origin.X % 2 == 1) return false;
            if (shape.IsUpSide == 1 && origin.X % 2 == 0) return false;
        }

        Debug.Log($"CellSplitter:8:{shape.Name}, origin.X:{origin.X}, origin.Y:{origin.Y}");
        if (CanPlace(origin.X, origin.Y, shape))
        {
            Debug.Log($"CellSplitter:9:{shape.Name}, origin.X:{origin.X}, origin.Y:{origin.Y}");
            // 1. 配置
            PlacePiece(origin.X, origin.Y, shape);

            // 2. ピースの使用フラグをセット
            shape.UseCount++;

            // FindNextEmptyCell(out int nextX, out int nextY);
            
            // 3. 次のセルへ再帰
            if (Solve(origin.X, origin.Y, enforceCount, false)) // 再帰
            {
                return true; // 成功
            }

            // 4. 後戻り (Backtrack)
            RemovePiece(origin.X, origin.Y, shape);
            shape.UseCount--; 
        }
        Debug.Log($"CellSplitter:10:{shape.Name}, origin.X:{origin.X}, origin.Y:{origin.Y}");
        return false;
    }

    // CellSplitter クラス内に追加

    /// <summary>
    /// パターンシードから指定された単一のピースを配置し、再帰探索を試みる。
    /// </summary>
    private static bool SolveWithPatternSeed(int startX, int startY, bool enforceCount)
    {
        // 復元リストが尽きた場合、グリッド全体が埋まっていれば成功と見なす
        if (_placementIndex >= _pieceNameSequence.Count)
        {
            // グリッドが埋まっているか再確認
            if (!FindNextEmptyCell(out int nextX, out int nextY))
            {
                if (enforceCount && _pieceIdCounter - 1 != TargetPieceCount) return false;
                return true; // 成功
            }
            // グリッドが埋まっていないのにピースリストが尽きた場合は失敗
            return false;
        }
        
        // 復元リストから次のピース名と座標を取得
        string requiredName = _pieceNameSequence[_placementIndex];
        GridCoord requiredOrigin = _originCoordSequence[_placementIndex];

        // 復元された原点座標と現在の探索開始座標が一致しない場合は失敗（シードとの整合性エラー）
        if (requiredOrigin.X != startX || requiredOrigin.Y != startY)
        {
            // ピースが復元リスト順に配置されていないため、ここではスキップ（このパスは失敗）
            return false;
        }

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

            // 2. ピースの使用フラグをセット
            requiredShape.UseCount++;
            
            // 3. パターン復元モードの場合、インデックスを進める
            _placementIndex++;

            // 4. 次のセルへ再帰
            if (Solve(startX, startY, enforceCount, true)) // isPatternSeedActive = true で再帰
            {
                return true; // 成功
            }

            // 5. 後戻り (Backtrack)
            RemovePiece(requiredOrigin.X, requiredOrigin.Y, requiredShape);
            requiredShape.UseCount--; 

            // パターン復元モードの場合、インデックスを元に戻す
            _placementIndex--;
        }

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
    /// 2. 指定セル数のピースから、UseCountが最も少ないものを候補として返す
    /// </summary>
    private static List<PieceShape> GetPrioritizedCandidates(int cellCount)
    {
        if (!_shapesByCellCount.ContainsKey(cellCount)) return new List<PieceShape>();

        List<PieceShape> pool = _shapesByCellCount[cellCount];
        
        // UseCountが最も少ない値を取得
        int minUseCount = pool.Min(s => s.UseCount);

        // UseCountが最小値のピースを候補とする
        List<PieceShape> candidates = pool
            .Where(s => s.UseCount == minUseCount)
            .ToList();

        // 候補内でのランダム性を確保するためシャッフル
        // Note: ShuffleAvailableShapes()を直接使わず、Listの静的拡張メソッドとして実装するとより汎用的です
        ShuffleList(candidates, _random);
        
        return candidates;
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
        _isPatternSeedActive = false; // 初期化
        if (string.IsNullOrEmpty(seed)) return;

        // シード形式: "GX-GY-TC-Type|Name1:X1,Y1|Name2:X2,Y2|..."
        string[] headerAndData = seed.Split('|');

        // ヘッダー (GX-GY-TC-Type) の解析
        string[] headerParts = headerAndData[0].Split('-');
        if (headerParts.Length != 4) return;

        // パラメータの確認
        if (int.TryParse(headerParts[0], out int decodedX) && decodedX == GridX &&
            int.TryParse(headerParts[1], out int decodedY) && decodedY == GridY &&
            int.TryParse(headerParts[2], out int decodedCount) && decodedCount == TargetPieceCount &&
            int.TryParse(headerParts[3], out int shapeInt) && (ShapeType)shapeInt == CurrentShapeType)
        {
            // パラメータが一致した場合のみ復元を試みる
            _isPatternSeedActive = true;
            _pieceNameSequence = new List<string>();
            _originCoordSequence = new List<GridCoord>();

            if (headerAndData.Length > 1) // ピースデータがある場合
            {
                string[] pieceData = headerAndData.Skip(1).ToArray();
                foreach (string piece in pieceData)
                {
                    string[] nameAndCoord = piece.Split(':');
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
        string seed = $"{GridX}-{GridY}-{TargetPieceCount}-{shapeInt}";
        
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
    
    // ピースが(x, y)に配置可能かチェック
    private static bool CanPlace(int originX, int originY, PieceShape shape)
    {
        Debug.Log($"CellSplitter:8.1:{shape.Name}, originX:{originX}, originY:{originY}");
        // ピースを構成するセルの位置
        foreach (var cell in shape.Cells)
        {
            int x = originX + cell.X;
            int y = originY + cell.Y;
            // Debug.Log($"CellSplitter:8.2:{shape.Name}, originX:{originX}, originY:{originY}, cell.X:{cell.X}, cell.Y:{cell.Y}, x:{x}, y:{y}, {_grid[x, y]}");
            
            // グリッド範囲外、またはすでに埋まっているセルと重複する場合は配置不可
            if (x < 0 || x >= GridX || y < 0 || y >= GridY || _grid[x, y] != 0)
            {
                return false;
            }
        }
        
        // TODO: 2x2などの大きな長方形の形成チェックをここに追加する（非常に複雑）
        // 現時点では、ピースの定義段階でその制約を満たしていることを前提としています。
        
        return true;
    }

    // ピースをグリッドに配置
    private static void PlacePiece(int originX, int originY, PieceShape shape)
    {
        int id = _pieceIdCounter++;
        foreach (var cell in shape.Cells)
        {
            _grid[originX + cell.X, originY + cell.Y] = id;
        }
        _successfulPlacements.Add(new PieceRecord 
        { 
            Shape = shape, 
            Origin = new GridCoord(originX, originY), 
            PieceId = id 
        });
    }

    // ピースをグリッドから除去 (後戻り用)
    private static void RemovePiece(int originX, int originY, PieceShape shape)
    {
        if (_successfulPlacements != null && 1 <= _successfulPlacements.Count)
        {
            // ピース配置リストから削除
            _successfulPlacements.RemoveAt(_successfulPlacements.Count - 1);
        }
        // _successfulPlacements.RemoveAt(_successfulPlacements.Count - 1);

        // グリッドをクリア
        foreach (var cell in shape.Cells)
        {
            _grid[originX + cell.X, originY + cell.Y] = 0;
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
        return $"{gridX}-{gridY}-{targetCount}-{shapeInt}-{numericPart}";
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
}