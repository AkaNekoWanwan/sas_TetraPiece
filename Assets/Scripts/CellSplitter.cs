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

    private static void CreatePiecePlacements(string patternSeed = null, List<string> avoidPatternSeeds = null)
    {
        // 1. ピース形状の定義と生成
        switch (CurrentShapeType)
        {
            case ShapeType.Square:
            default:
                InitializeShapesSquare();
                break;
            case ShapeType.Hex:
                InitializeShapesHex();
                break;
            case ShapeType.Triangle:
                InitializeShapesTriangle();
                break;
        }
        // 2. シード値の決定と解析 (PatternSeedのデコード)
        AnalysisPatternSeed(patternSeed);

        bool success = false;
        bool isRandom = true;

        // =========================================================
        // 第1パス: 受け取ったパターンシードのデコードと強制再現
        // =========================================================
        if (_isPatternSeedActive)
        {
            PreSolve();
            Debug.Log($"--- 第1パス開始: パターンシードの再現 ---");
            success = Solve(0, 0, true, true, true);
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
                PreSolve();
                if (Solve(0, 0, true, true)) currentAttemptSuccess = true;
                
                // 試行2: ターゲットピース数厳守・形状ユニーク性無視
                if (!currentAttemptSuccess)
                {
                    PreSolve();
                    if (Solve(0, 0, true, false)) currentAttemptSuccess = true;
                }
                
                // 試行3: ピース数無視・形状ユニーク
                if (!currentAttemptSuccess)
                {
                    PreSolve();
                    if (Solve(0, 0, false, true)) currentAttemptSuccess = true;
                }

                // 試行4: ピース数・形状ユニーク性無視 (敷き詰め優先)
                if (!currentAttemptSuccess)
                {
                    PreSolve();
                    if (Solve(0, 0, false, false)) currentAttemptSuccess = true;
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
        _availableShapes = new List<PieceShape>();

        // 1セル (I-1)
        int maxUseI1 = _random.Next(0, 2);
        _availableShapes.Add(new PieceShape("I1", new List<GridCoord> { new GridCoord(0, 0) }, maxUseI1));

        // 2セル (I-2)
        _availableShapes.Add(new PieceShape("I2", new List<GridCoord> { new GridCoord(0, 0), new GridCoord(1, 0) }));
        _availableShapes.Add(new PieceShape("I2", new List<GridCoord> { new GridCoord(0, 0), new GridCoord(0, 1) }));

        // 3セル (I-3, L-3, V-3)
        _availableShapes.Add(new PieceShape("I3", new List<GridCoord> { new GridCoord(0, 0), new GridCoord(1, 0), new GridCoord(2, 0) }));
        _availableShapes.Add(new PieceShape("I3", new List<GridCoord> { new GridCoord(0, 0), new GridCoord(0, 1), new GridCoord(0, 2) }));
        _availableShapes.Add(new PieceShape("L3", new List<GridCoord> { new GridCoord(0, 0), new GridCoord(1, 0), new GridCoord(0, 1) }));
        _availableShapes.Add(new PieceShape("」3", new List<GridCoord> { new GridCoord(0, 0), new GridCoord(1, 0), new GridCoord(1, 1) }));
        _availableShapes.Add(new PieceShape("「3", new List<GridCoord> { new GridCoord(0, 0), new GridCoord(0, 1), new GridCoord(1, 1) }));
        _availableShapes.Add(new PieceShape("73", new List<GridCoord> { new GridCoord(1, 0), new GridCoord(0, 1), new GridCoord(1, 1) }));
        // 4セル (T字の例) - 3x3制約内に収まっている
        _availableShapes.Add(new PieceShape("T4", new List<GridCoord> {
            new GridCoord(1, 0),
            new GridCoord(0, 1), new GridCoord(1, 1), new GridCoord(2, 1)
        }));
        _availableShapes.Add(new PieceShape("凸4", new List<GridCoord> {
            new GridCoord(0, 0), new GridCoord(1, 0), new GridCoord(2, 0),
             new GridCoord(1, 1)
        }));
        _availableShapes.Add(new PieceShape("ト4", new List<GridCoord> {
            new GridCoord(0, 0), new GridCoord(0, 1), new GridCoord(0, 2),
             new GridCoord(1, 1)
        }));
        _availableShapes.Add(new PieceShape("{4", new List<GridCoord> {
            new GridCoord(1, 0), new GridCoord(1, 1), new GridCoord(1, 2),
             new GridCoord(0, 1)
        }));
    }
    // 六角形セル用
    private static void InitializeShapesHex()
    {
        _availableShapes = new List<PieceShape>();

        // 1セル (I-1)
        int maxUseI1 = _random.Next(0, 2);
        _availableShapes.Add(new PieceShape(".1-A", new List<GridCoord> { new GridCoord(0, 0) }, maxUseI1));

        // 2セル (I-2)
        _availableShapes.Add(new PieceShape("I2-A", new List<GridCoord> { new GridCoord(0, 0), new GridCoord(1, 0) }));
        _availableShapes.Add(new PieceShape("I2-B", new List<GridCoord> { new GridCoord(0, 0), new GridCoord(0, 1) }));

        // 3セル (I-3, L-3, V-3)
        _availableShapes.Add(new PieceShape("I3-A", new List<GridCoord> { new GridCoord(0, 0), new GridCoord(1, 0), new GridCoord(2, 0) }));
        _availableShapes.Add(new PieceShape("I3-B", new List<GridCoord> { new GridCoord(0, 0), new GridCoord(0, 1), new GridCoord(0, 2) }));
        _availableShapes.Add(new PieceShape("L3-A", new List<GridCoord> { new GridCoord(0, 0), new GridCoord(1, 0), new GridCoord(0, 1) }));
        _availableShapes.Add(new PieceShape("L3-B", new List<GridCoord> { new GridCoord(0, 0), new GridCoord(1, 0), new GridCoord(1, 1) }));
        _availableShapes.Add(new PieceShape("L3-C", new List<GridCoord> { new GridCoord(0, 0), new GridCoord(0, 1), new GridCoord(1, 1) }));
        _availableShapes.Add(new PieceShape("L3-D", new List<GridCoord> { new GridCoord(1, 0), new GridCoord(0, 1), new GridCoord(1, 1) }));
        // 4セル (T字の例) - 3x3制約内に収まっている
        _availableShapes.Add(new PieceShape("T4-A", new List<GridCoord> {
            new GridCoord(1, 0),
            new GridCoord(0, 1), new GridCoord(1, 1), new GridCoord(2, 1)
        }));
        _availableShapes.Add(new PieceShape("T4-B", new List<GridCoord> {
            new GridCoord(0, 0), new GridCoord(1, 0), new GridCoord(2, 0),
             new GridCoord(1, 1)
        }));
        _availableShapes.Add(new PieceShape("T4-C", new List<GridCoord> {
            new GridCoord(0, 0), new GridCoord(0, 1), new GridCoord(0, 2),
             new GridCoord(1, 1)
        }));
        _availableShapes.Add(new PieceShape("T4-D", new List<GridCoord> {
            new GridCoord(1, 0), new GridCoord(1, 1), new GridCoord(1, 2),
             new GridCoord(0, 1)
        }));
    }
    // 三角形セル用
    private static void InitializeShapesTriangle()
    {
        _availableShapes = new List<PieceShape>();

        // 1セル (I-1)
        int maxUseI1 = _random.Next(0, 2);
        _availableShapes.Add(new PieceShape(".1-A", new List<GridCoord> { new GridCoord(0, 0) }, -1, maxUseI1));

        // 2セル (I-2)
        _availableShapes.Add(new PieceShape("I2-A", new List<GridCoord> { new GridCoord(0, 0), new GridCoord(1, 0) }, -1, 0));
        _availableShapes.Add(new PieceShape("I2-B", new List<GridCoord> { new GridCoord(0, 0), new GridCoord(0, 1) }, -1, 1));

        _availableShapes.Add(new PieceShape("_3-A", new List<GridCoord> { new GridCoord(0, 0), new GridCoord(1, 0), new GridCoord(2, 0) }, -1, 0));

        _availableShapes.Add(new PieceShape("/3-A", new List<GridCoord> { new GridCoord(0, 0), new GridCoord(0, 1), new GridCoord(1, 1) }, -1, 1));
        _availableShapes.Add(new PieceShape("/3-B", new List<GridCoord> { new GridCoord(0, 0), new GridCoord(0, 1), new GridCoord(1, 0) }, -1, 1));
        _availableShapes.Add(new PieceShape("/3-C", new List<GridCoord> { new GridCoord(1, 0), new GridCoord(1, 1), new GridCoord(0, 1) }, -1, 2));
        _availableShapes.Add(new PieceShape("/3-D", new List<GridCoord> { new GridCoord(2, 0), new GridCoord(1, 0), new GridCoord(1, 1) }, -1, 2));

        _availableShapes.Add(new PieceShape("/4-A", new List<GridCoord> { new GridCoord(0, 0), new GridCoord(1, 0), new GridCoord(1, 1), new GridCoord(2, 1) }, -1, 2));
        _availableShapes.Add(new PieceShape("/4-B", new List<GridCoord> { new GridCoord(0, 1), new GridCoord(1, 1), new GridCoord(1, 0), new GridCoord(2, 0) }, -1, 1));
        _availableShapes.Add(new PieceShape("/4-C", new List<GridCoord> { new GridCoord(0, 0), new GridCoord(0, 1), new GridCoord(1, 1), new GridCoord(2, 1) }, -1, 1));
        _availableShapes.Add(new PieceShape("/4-D", new List<GridCoord> { new GridCoord(0, 2), new GridCoord(0, 1), new GridCoord(1, 1), new GridCoord(1, 0) }, -1, 2));

        _availableShapes.Add(new PieceShape("<4-A", new List<GridCoord> { new GridCoord(0, 0), new GridCoord(0, 1), new GridCoord(1, 0), new GridCoord(1, 1) }, -1, 1));
        _availableShapes.Add(new PieceShape("<4-A", new List<GridCoord> { new GridCoord(0, 0), new GridCoord(0, 1), new GridCoord(1, 0), new GridCoord(1, 1) }, -1, 2));

        _availableShapes.Add(new PieceShape("L4-A", new List<GridCoord> { new GridCoord(0, 0), new GridCoord(1, 0), new GridCoord(2, 0), new GridCoord(0, 1) }, -1, 1));
        _availableShapes.Add(new PieceShape("L4-B", new List<GridCoord> { new GridCoord(0, 0), new GridCoord(1, 0), new GridCoord(2, 0), new GridCoord(2, 1) }, -1, 1));
        _availableShapes.Add(new PieceShape("L4-C", new List<GridCoord> { new GridCoord(0, 1), new GridCoord(1, 1), new GridCoord(2, 1), new GridCoord(0, 0) }, -1, 1));
        _availableShapes.Add(new PieceShape("L4-D", new List<GridCoord> { new GridCoord(0, 1), new GridCoord(1, 1), new GridCoord(2, 1), new GridCoord(2, 0) }, -1, 1));

        int maxUseI2 = _random.Next(0, 4);
        _availableShapes.Add(new PieceShape("y5-A", new List<GridCoord> { new GridCoord(0, 0), new GridCoord(1, 0), new GridCoord(1, 1), new GridCoord(0, 1), new GridCoord(2, 1) }, maxUseI2 == 0 ? 1 : 0, 2));
        _availableShapes.Add(new PieceShape("y5-B", new List<GridCoord> { new GridCoord(1, 0), new GridCoord(2, 0), new GridCoord(0, 1), new GridCoord(1, 1), new GridCoord(2, 1) }, maxUseI2 == 1 ? 1 : 0, 2));
        _availableShapes.Add(new PieceShape("y5-C", new List<GridCoord> { new GridCoord(0, 0), new GridCoord(1, 0), new GridCoord(2, 0), new GridCoord(1, 1), new GridCoord(0, 1) }, maxUseI2 == 2 ? 1 : 0, 2));
        _availableShapes.Add(new PieceShape("y5-D", new List<GridCoord> { new GridCoord(0, 0), new GridCoord(1, 0), new GridCoord(2, 0), new GridCoord(1, 1), new GridCoord(2, 1) }, maxUseI2 == 3 ? 1 : 0, 2));

        _availableShapes.Add(new PieceShape("「5-A", new List<GridCoord> { new GridCoord(0, 0), new GridCoord(0, 1), new GridCoord(1, 1), new GridCoord(2, 1), new GridCoord(3, 1) }, -1, 1));
        _availableShapes.Add(new PieceShape("「5-B", new List<GridCoord> { new GridCoord(2, 0), new GridCoord(0, 1), new GridCoord(1, 1), new GridCoord(2, 1), new GridCoord(3, 1) }, -1, 1));
        _availableShapes.Add(new PieceShape("「5-C", new List<GridCoord> { new GridCoord(3, 0), new GridCoord(0, 1), new GridCoord(1, 1), new GridCoord(2, 1), new GridCoord(3, 1) }, -1, 2));
        _availableShapes.Add(new PieceShape("「5-D", new List<GridCoord> { new GridCoord(1, 0), new GridCoord(0, 1), new GridCoord(1, 1), new GridCoord(2, 1), new GridCoord(3, 1) }, -1, 2));

        _availableShapes.Add(new PieceShape("「5-E", new List<GridCoord> { new GridCoord(0, 1), new GridCoord(0, 0), new GridCoord(1, 0), new GridCoord(2, 0), new GridCoord(3, 0) }, -1, 1));
        _availableShapes.Add(new PieceShape("「5-F", new List<GridCoord> { new GridCoord(1, 1), new GridCoord(0, 0), new GridCoord(1, 0), new GridCoord(2, 0), new GridCoord(3, 0) }, -1, 2));
        _availableShapes.Add(new PieceShape("「5-G", new List<GridCoord> { new GridCoord(2, 1), new GridCoord(0, 0), new GridCoord(1, 0), new GridCoord(2, 0), new GridCoord(3, 0) }, -1, 1));
        _availableShapes.Add(new PieceShape("「5-H", new List<GridCoord> { new GridCoord(3, 1), new GridCoord(0, 0), new GridCoord(1, 0), new GridCoord(2, 0), new GridCoord(3, 0) }, -1, 2));

        _availableShapes.Add(new PieceShape("「5-I", new List<GridCoord> { new GridCoord(0, 0), new GridCoord(1, 0), new GridCoord(2, 0), new GridCoord(2, 1), new GridCoord(3, 1) }, -1, 1));
        _availableShapes.Add(new PieceShape("「5-J", new List<GridCoord> { new GridCoord(1, 0), new GridCoord(2, 0), new GridCoord(3, 0), new GridCoord(0, 1), new GridCoord(1, 1) }, -1, 2));
        _availableShapes.Add(new PieceShape("「5-K", new List<GridCoord> { new GridCoord(0, 0), new GridCoord(1, 0), new GridCoord(1, 1), new GridCoord(2, 1), new GridCoord(3, 1) }, -1, 2));
        _availableShapes.Add(new PieceShape("「5-L", new List<GridCoord> { new GridCoord(0, 1), new GridCoord(1, 1), new GridCoord(2, 1), new GridCoord(1, 0), new GridCoord(2, 0) }, -1, 2));
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
    // enforceUnique: 形状の重複を許さないか
    private static bool Solve(int startX, int startY, bool enforceCount, bool enforceUnique, bool isPatternSeedActive = false) // 引数を追加
    {
        // 探索に使用するピースのリストを決定
        List<PieceShape> shapesToTry;
        
        if (isPatternSeedActive)
        {
            // パターン復元モード: 次に使用するピースが復元リストにあるか確認
            if (_placementIndex >= _pieceNameSequence.Count)
            {
                // ピースリストが尽きたが、グリッドが埋まっていない場合は失敗
                if (!FindNextEmptyCell(out startX, out startY))
                {
                    // グリッドが埋まっており、ピース数が一致すれば成功
                    if (enforceCount && _pieceIdCounter - 1 != TargetPieceCount) return false;
                    
                    // 成功したパターンのエンコードは CreatePiecePlacements で処理するため、ここでは true のみ
                    return true;
                }
                return false;
            }
            
            // 復元リストから次のピース名を取得
            string requiredName = _pieceNameSequence[_placementIndex];
            PieceShape requiredShape = _availableShapes.FirstOrDefault(s => s.Name == requiredName);
            
            if (requiredShape == null)
            {
                // ピース定義リストに復元したいピース名がない場合、復元失敗
                Debug.LogError($"パターンシードに記載された形状名 '{requiredName}' が定義されていません。");
                return false;
            }

            // 復元モードでは、このピースのみを試行リストに入れる
            shapesToTry = new List<PieceShape> { requiredShape };
            
        }
        else
        {
            // ランダム探索モード: 乱数でシャッフルし、制約に従ってソート
            ShuffleAvailableShapes();
            // ピースの使用回数（UseCount）が少ない順に並べる
            shapesToTry = _availableShapes.OrderBy(shape => shape.UseCount)
                                          .ThenBy(shape => (0 <= shape.MaxUse && shape.MaxUse <= shape.UseCount))
                                          .ToList();
        }

        // 終了条件: すべてのセルが埋まった
        if (!FindNextEmptyCell(out startX, out startY))
        {
            if (enforceCount && _pieceIdCounter - 1 != TargetPieceCount) return false;
            // ランダム探索で成功した場合、CreatePiecePlacementsでPatternSeedを更新する
            return true;
        }

        // Count厳守モードで、ピース数が上限を超えた場合は失敗 (早期終了)
        if (enforceCount && _pieceIdCounter - 1 >= TargetPieceCount)
            return false;

        
        // 全ての利用可能な（または復元された）ピース形状を試す
        foreach (var shape in shapesToTry)
        {
            // Unique厳守モードの場合、既に使われた形状はスキップ
            if (!isPatternSeedActive && enforceUnique && (0 < shape.UseCount)) continue;
            // if (0 <= shape.MaxUse && shape.MaxUse <= shape.UseCount) continue; // MaxUse制約は常に考慮

            // パターン復元モードの場合、配置座標も復元リストから取得
            GridCoord origin;
            if (isPatternSeedActive)
            {
                origin = _originCoordSequence[_placementIndex];
                // 復元された原点座標と現在の探索開始座標が一致しない場合はスキップ (復元データとの整合性を保つ)
                if (origin.X != startX || origin.Y != startY) continue; 
            }
            else
            {
                origin = new GridCoord(startX, startY);
            }
            
            // 三角形セルの向きチェック
            if (CurrentShapeType == ShapeType.Triangle)
            {
                bool isUpSide = ((startX + startY) % 2) == 0;
                if (shape.IsUpSide == 2 && isUpSide) continue;
                if (shape.IsUpSide == 1 && !isUpSide) continue;
            }

            if (CanPlace(origin.X, origin.Y, shape))
            {
                // 1. 配置
                PlacePiece(origin.X, origin.Y, shape);

                // 2. ピースの使用フラグをセット
                shape.UseCount++;
                
                // 3. パターン復元モードの場合、インデックスを進める
                if (isPatternSeedActive)
                {
                    _placementIndex++;
                }

                // 4. 次のセルへ再帰
                if (Solve(startX, startY, enforceCount, enforceUnique, isPatternSeedActive))
                {
                    return true; // 成功
                }

                // 5. 後戻り (Backtrack)
                RemovePiece(origin.X, origin.Y, shape);
                shape.UseCount--; 

                // パターン復元モードの場合、インデックスを元に戻す
                if (isPatternSeedActive)
                {
                    _placementIndex--;
                }
            }
        }

        return false; // どのピースも配置できなかった
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
        // ピースを構成するセルの位置
        foreach (var cell in shape.Cells)
        {
            int x = originX + cell.X;
            int y = originY + cell.Y;
            
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
        // ピース配置リストから削除
        _successfulPlacements.RemoveAt(_successfulPlacements.Count - 1);

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
}