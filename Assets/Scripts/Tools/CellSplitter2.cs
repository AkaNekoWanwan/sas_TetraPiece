using UnityEngine;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System; // Guidを使うために必要
using System.Security.Cryptography; // シード値生成のために追加

// 生成したセルをオートでいい感じにピースに分けるクラス
// public enum ShapeType
// {
//     Square,
//     Hex,
//     Triangle
// }

// // セルの座標を表す構造体
// public struct GridCoord
// {
//     public int X;
//     public int Y;
//     public GridCoord(int x, int y) { X = x; Y = y; }
// }

// // パズルのピースの形状を定義するクラス
// public class PieceShape
// {
//     // ピースを構成するセルの相対座標リスト
//     public readonly List<GridCoord> Cells;
//     public readonly string Name;
//     // 形状の重複を避けるために使用されるフラグ
//     public int UseCount = 0;
//     public int MaxUse = -1;
//     public int IsUpSide = 0;   // 三角形セル用。上向き三角形用のShapeなのか下向き三角形用のShapeなのかどちらでも可なのか
//                                // 六角形セル用にも。0なら両方用。１ならX奇数用(Yが半個分上)。2ならX偶数用(半個分下)。    

//     public PieceShape(string name, List<GridCoord> cells, int maxUse = -1, int isUpSide = 0)
//     {
//         Name = name;
//         Cells = cells;
//         MaxUse = maxUse;
//         IsUpSide = isUpSide;
//     }
// }

/// <summary>
// 本体クラス
/// </summary>
public class CellSplitter2
{
    // 公開変数 (読み取り専用)
    public int GridX = 6; 
    public int GridY = 6;
    public int TargetPieceCount = 10;

    // グリッドの状態 (0: 未使用, 1～N: ピースID)
    private int[,] _grid;
    // 使用可能なピースの形状リスト
    private List<PieceShape> _availableShapes;
    // ピースIDのカウンター
    private int _pieceIdCounter; // 0で初期化
    // 成功したピースのリスト
    private List<PieceRecord> _successfulPlacements = new List<PieceRecord>();

    public ShapeType _currentShapeType { get; private set; }

    // === シード値関係 ===
    private System.Random _random; // 探索のランダム性を制御する乱数生成器
    private int _randomSeed;       // System.Randomの初期化に使用する数値シード
    public string PatternSeed { get; private set; } // パターンを再現するためのシード (エンコードされた文字列)

    private bool _isPatternSeedActive = false;             // パターンシードからピースパターンを復元するか
    private List<string> _pieceNameSequence;               // パターンシードから復元した、使用するPieceShapeのNameリスト
    private List<GridCoord> _originCoordSequence;          // パターンシードから復元した、使用するPieceの原点座標リスト
    private int _placementIndex = 0;                       // 復元用リストのインデックス
    
    // 配置されたピースの情報
    public struct PieceRecord
    {
        public PieceShape Shape;
        public GridCoord Origin;
        public int PieceId;
    }

    // ★ 静的読み取り専用フィールドとして定義
    // private readonly List<PieceShape> SQUARE_SHAPES;
    // private readonly List<PieceShape> HEX_SHAPES;
    // private readonly List<PieceShape> TRIANGLE_SHAPES;
    // ピースのセル数ごとの抽選確率 (合計100%)
    private readonly Dictionary<int, float> CELL_COUNT_PROBABILITIES = new Dictionary<int, float>
    {
        { 1, 0.10f }, // 1セル: 10%
        { 2, 0.20f }, // 2セル: 30%
        { 3, 0.35f }, // 3セル: 30%
        { 4, 0.35f }  // 4セル: 30%
    };

    // ピースのグループ化リスト (キー: セル数)
    private Dictionary<int, List<PieceShape>> _shapesByCellCount;
    // private List<Vector2Int> _neighbors;

    // ピースの最大セル数 (枝刈り、ユーティリティ用に利用)
    private int _maxCellCount = 0;

    // ★ 静的コンストラクタで一度だけ初期化
    public CellSplitter2(ShapeType shapeType)
    {
        _availableShapes = CellSplitter.GetPieceShapes(shapeType);
        _currentShapeType = shapeType; // ★ ここでShapeTypeを保持する
    }

    /// <summary>
    /// ピース形状リストをセル数ごとにグループ化する
    /// </summary>
    private void GroupShapesByCellCount()
    {
        _shapesByCellCount = _availableShapes
            .GroupBy(s => s.Cells.Count)
            .ToDictionary(g => g.Key, g => g.ToList());
        
        // 最大セル数を更新
        _maxCellCount = _shapesByCellCount.Keys.Max();
    }

    private readonly Comparison<PieceShape> ShapeComparer = (a, b) => 
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

    public void CellSplit( int cols, int rows, ref int orderPieceNum, List<AnswerGridPos> cells, GridPieceListController gridPieceListController, string patternSeed = null, List<string> avoidPatternSeeds = null )
    {
        // ピース生成のパラメーターセット
        GridX = cols;
        GridY = rows;
        TargetPieceCount = orderPieceNum;
        PatternSeed = patternSeed; // パターンシードを保持

        // 乱数生成器の数値シードを決定 (ランダム探索の再現性用。パターン再現とは別)
        _randomSeed = GetRandomIntSeed(); // 新しい乱数シード生成関数を使用
        _random = new System.Random(_randomSeed);

        avoidPatternSeeds = null;

        // 1. ピース形状の定義を取得
        // SetAvailableShapes();
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

    public void CellSplit( int cols, int rows, ref int orderPieceNum, string patternSeed = null, List<string> avoidPatternSeeds = null )
    {
        // ピース生成のパラメーターセット
        GridX = cols;
        GridY = rows;
        TargetPieceCount = orderPieceNum;
        PatternSeed = patternSeed; // パターンシードを保持

        // 乱数生成器の数値シードを決定 (ランダム探索の再現性用。パターン再現とは別)
        _randomSeed = GetRandomIntSeed(); // 新しい乱数シード生成関数を使用
        _random = new System.Random(_randomSeed);

        avoidPatternSeeds = null;

        // 1. ピース形状の定義を取得
        // SetAvailableShapes();
        GroupShapesByCellCount();
        // 2. シード値の決定と解析 (PatternSeedのデコード)
        AnalysisPatternSeed(patternSeed);
        // ピース情報の生成
        CreatePiecePlacements(patternSeed, avoidPatternSeeds);
    }

    public void SetUpSplitPieceData( ref int orderPieceNum, List<AnswerGridPos> cells, GridPieceListController gridPieceListController)
    {
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
    private void RegisterCellsAsPieces(List<PieceDragController> pieceList, List<AnswerGridPos> cells)
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
    private AnswerGridPos FindCell(List<AnswerGridPos> cells, int searchX, int searchY)
    {
        AnswerGridPos cell = cells.FirstOrDefault(c => c.x == searchX && c.y == searchY);
        return cell;
    }

    // private void SetAvailableShapes()
    // {
    //     switch (_currentShapeType)
    //     {
    //         case ShapeType.Square:
    //         default:
    //             _availableShapes = new List<PieceShape>(SQUARE_SHAPES);
    //             break;
    //         case ShapeType.Hex:
    //             _availableShapes = new List<PieceShape>(HEX_SHAPES);
    //             break;
    //         case ShapeType.Triangle:
    //             _availableShapes = new List<PieceShape>(TRIANGLE_SHAPES);
    //             break;
    //     }
    // }

    private void CreatePiecePlacements(string patternSeed = null, List<string> avoidPatternSeeds = null)
    {
        bool success = false;
        bool isRandom = true;

        // =========================================================
        // 第1パス: 受け取ったパターンシードのデコードと強制再現
        // =========================================================
        if (_isPatternSeedActive)
        {
            PreSolve();
            // Debug.Log($"--- 第1パス開始: パターンシードの再現 ---");
            success = Solve(0, 0, true, true);
            isRandom = false;
        }

        // =========================================================
        // 第2パス以降: ランダム探索（ユニーク性保証付き）
        // =========================================================
        if (!success)
        {
            _isPatternSeedActive = false;
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
            MergeSmallPieces();
            
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
    private void ResetPieceUsage()
    {
        foreach (var shape in _availableShapes)
        {
            shape.UseCount = 0;
        }
    }

    // ピースデータ作成開始前の準備
    private void PreSolve()
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
    private bool FindNextEmptyCell(out int nextX, out int nextY)
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
    private bool Solve(int startX, int startY, bool enforceCount, bool isPatternSeedActive = false) // 引数を追加
    {
        // 探索に使用するピースのリストを決定
        // Debug.Log($"CellSplitter2:1:{_availableShapes.Count}, startX:{startX}, startY:{startY}");
        // Count厳守モードで、ピース数が上限を超えた場合は失敗 (早期終了)
        // 終了条件: すべてのセルが埋まった
        if (!FindNextEmptyCell(out startX, out startY))
        {
            if(isPatternSeedActive)
                return true;
            if (_pieceIdCounter != -1 && enforceCount && _pieceIdCounter - 1 != TargetPieceCount) return false;
            // ランダム探索で成功した場合、CreatePiecePlacementsでPatternSeedを更新する
            return true;
        }
        // Debug.Log($"CellSplitter2:2, startX:{startX}, startY:{startY}");
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

        // Debug.Log($"CellSplitter2:3:{availableCellCounts.Count}, startX:{startX}, startY:{startY}");
        
        while(failedCellCounts.Count < availableCellCounts.Count)
        {
            // 1. 確率に基づいてセル数を抽選 (失敗済みのセル数は除外)
            int selectedCellCount = SelectCellCountByProbability(availableCellCounts, failedCellCounts);
            // Debug.Log($"CellSplitter2:4:{selectedCellCount}, startX:{startX}, startY:{startY}");
            if (selectedCellCount == -1) break;  // 抽選対象が残っていない
            // Debug.Log($"CellSplitter2:5:{selectedCellCount}, startX:{startX}, startY:{startY}");
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
                // Debug.Log($"CellSplitter2:6:tryGetShape, startX:{startX}, startY:{startY}");
                if (shape == null) break; // 選択対象なし (本来はcandidates.Count == failedShapes.Countでループ終了)

                // Debug.Log($"CellSplitter2:7:{shape.Name}, startX:{startX}, startY:{startY}");

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
    private bool TryPlaceAndSolve(GridCoord origin, PieceShape shape, bool enforceCount)
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
        if (_currentShapeType == ShapeType.Triangle)
        {
            // 基準: シフト後の原点 (finalOriginX, finalOriginY)
            bool isUpSide = ((finalOriginX + finalOriginY) % 2) == 0; 
            if (shape.IsUpSide == 2 && isUpSide) return false;
            if (shape.IsUpSide == 1 && !isUpSide) return false;
        }
        // 六角形の位置チェック
        if (_currentShapeType == ShapeType.Hex)
        {
            // 基準: シフト後の原点 (finalOriginX)
            if (shape.IsUpSide == 2 && finalOriginX % 2 == 1) return false;
            if (shape.IsUpSide == 1 && finalOriginX % 2 == 0) return false;
        }

        // Debug.Log($"CellSplitter2:8:{shape.Name}, origin.X:{origin.X}, origin.Y:{origin.Y}");
        
        // 4. 配置可能性チェック (CanPlaceはシフトを考慮)
        if (CanPlace(origin.X, origin.Y, shape))
        {
            // Debug.Log($"CellSplitter2:9:{shape.Name}, origin.X:{origin.X}, origin.Y:{origin.Y}");
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
        // Debug.Log($"CellSplitter2:10:{shape.Name}, origin.X:{origin.X}, origin.Y:{origin.Y}");
        return false;
    }

    // CellSplitter2 クラス内に追加

    /// <summary>
    /// パターンシードから指定された単一のピースを配置し、再帰探索を試みる。
    /// </summary>
    private bool SolveWithPatternSeed(int startX, int startY, bool enforceCount)
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
        // if (requiredOrigin.X != startX || requiredOrigin.Y != startY)
        // {
        //     // ピースが復元リスト順に配置されていないため、ここではスキップ（このパスは失敗）
        //     return false;
        // }
        startX = requiredOrigin.X;
        startY = requiredOrigin.Y;

        // ピース定義リストから形状オブジェクトを探す
        PieceShape requiredShape = _availableShapes.FirstOrDefault(s => s.Name == requiredName);
        if (requiredShape == null)
        {
            Debug.LogError($"パターンシードに記載された形状名 '{requiredName}' が定義されていません。");
            return false;
        }

        // ★ 試行と再帰の実行 ★

        // 三角形セルの向きチェック (TryPlaceAndSolveと同様の処理が必要だが、ここではインラインで処理)
        if (_currentShapeType == ShapeType.Triangle)
        {
            bool isUpSide = ((requiredOrigin.X + requiredOrigin.Y) % 2) == 0;
            if (requiredShape.IsUpSide == 2 && isUpSide) return false;
            if (requiredShape.IsUpSide == 1 && !isUpSide) return false;
        }
        // 六角形の位置チェック
        if (_currentShapeType == ShapeType.Hex)
        {
            if (requiredShape.IsUpSide == 2 && requiredOrigin.X % 2 == 1) return false;
            if (requiredShape.IsUpSide == 1 && requiredOrigin.X % 2 == 0) return false;
        }

        if (CanPlace(requiredOrigin.X, requiredOrigin.Y, requiredShape))
        {
            // 1. 配置
            PlacePiece(requiredOrigin.X, requiredOrigin.Y, requiredShape);

            // 2. ピースの使用フラグをセット
            // requiredShape.UseCount++;
            
            // 3. パターン復元モードの場合、インデックスを進める
            _placementIndex++;

            // 4. 次のセルへ再帰
            if (Solve(startX, startY, enforceCount, true)) // isPatternSeedActive = true で再帰
            {
                return true; // 成功
            }

            // 5. 後戻り (Backtrack)
            // RemovePiece(requiredOrigin.X, requiredOrigin.Y, requiredShape);
            // requiredShape.UseCount--; 

            // パターン復元モードの場合、インデックスを元に戻す
            // _placementIndex--;
        }

        return false; // 配置できなかったか、配置後の再帰に失敗した
    }

    // CellSplitter2 クラス内に追加

    /// <summary>
    /// 1. 確率に基づいてセル数を抽選する (失敗済みのセル数は除外)
    /// </summary>
    private int SelectCellCountByProbability(List<int> availableCellCounts, HashSet<int> failedCellCounts)
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
    private List<PieceShape> GetPrioritizedCandidates(int cellCount, GridCoord currentOrigin)
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
    private PieceShape SelectRandomCandidate(List<PieceShape> candidates, HashSet<PieceShape> failedShapes)
    {
        var remainingCandidates = candidates.Where(s => !failedShapes.Contains(s)).ToList();
        
        if (remainingCandidates.Count == 0) return null;

        int index = _random.Next(remainingCandidates.Count);
        return remainingCandidates[index];
    }

    // 汎用シャッフル関数（Listの拡張メソッドとして定義することを推奨）
    private void ShuffleList<T>(List<T> list, System.Random rng)
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
    private List<int> GetShuffledCellCounts()
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
    private void AnalysisPatternSeed(string seed)
    {
        // Debug.Log($"CellSplitter2.AnalysisPatternSeed:1:{seed}");
        _isPatternSeedActive = false; // 初期化
        if (string.IsNullOrEmpty(seed)) return;

        // シード形式: "GX-GY-TC-Type|Name1:X1,Y1|Name2:X2,Y2|..."
        string[] headerAndData = seed.Split('|');

        // ヘッダー (GX-GY-TC-Type) の解析
        string[] headerParts = headerAndData[0].Split('=');
        // Debug.Log($"CellSplitter2.AnalysisPatternSeed:2: Length:{headerParts.Length}, {seed}");
        if (headerParts.Length != 4) return;

        // Debug.Log($"CellSplitter2.AnalysisPatternSeed:3: headerParts[0]:{headerParts[0]}, headerParts[1]:{headerParts[1]}, headerParts[3]:{headerParts[3]}, GridX:{GridX}, GridY:{GridY}, {_currentShapeType}");
        // パラメータの確認
        if (int.TryParse(headerParts[0], out int decodedX) && decodedX == GridX &&
            int.TryParse(headerParts[1], out int decodedY) && decodedY == GridY &&
            // int.TryParse(headerParts[2], out int decodedCount) && decodedCount == TargetPieceCount &&
            int.TryParse(headerParts[3], out int shapeInt) && (ShapeType)shapeInt == _currentShapeType)
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
            // TargetPieceCount = decodedCount;
        }
    }
    
    /// <summary>
    /// 成功した配置結果をパターンシード文字列にエンコードする
    /// </summary>
    private string EncodePlacement(List<PieceRecord> placements)
    {
        // ヘッダー: "GX-GY-TC-Type"
        int shapeInt = (int)_currentShapeType;
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
    private void ShuffleAvailableShapes()
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
    private bool CanPlace(int originX, int originY, PieceShape shape)
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
    private void PlacePiece(int originX, int originY, PieceShape shape)
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
    private void RemovePiece(int originX, int originY, PieceShape shape)
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
    private bool IsUniqueSeed(string newSeed, List<string> avoidSeeds)
    {
        return !avoidSeeds.Contains(newSeed);
    }
    
    /// <summary>
    /// GridX, GridY, TargetPieceCountの情報を埋め込んだ文字列シードを生成する
    /// </summary>
    public string EncodeSeed(int gridX, int gridY, int targetCount, ShapeType shapeType)
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
    private int GetRandomIntSeed()
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
    private int GetMinPieceSize()
    {
        if (_availableShapes == null || _availableShapes.Count == 0) return 1;
        
        // ピースリスト内の最小セル数を取得
        return _availableShapes.Min(s => s.Cells.Count);
    }
    
    // 現在のグリッド状態が実行可能か（孤立した空きセルがないか）をチェック
    private bool IsFeasible()
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
    private int CountConnectedEmptyArea(int startX, int startY, bool[,] visited)
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

    // CellSplitter2 クラス内に追加/修正

    /// <summary>
    /// ピース形状が占有するセルのうち、最も左上のセルを (0, 0) に合わせるためのシフト座標を計算する。
    /// </summary>
    /// <returns>シフト座標 (GridCoord)</returns>
    // CalculateOptimalShift メソッドの修正
    private GridCoord CalculateOptimalShift(PieceShape shape)
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
    private void MergeSmallPieces()
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

    // ========== ユーティリティ (GetAdjacentPieceRecords, GetAbsoluteCells, FindMatchingShape が必要) ==========

    // ターゲットピースレコードに隣接するピースレコードを取得
    private List<PieceRecord> GetAdjacentPieceRecords(PieceRecord targetRecord)
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
            GridCoord[] neighbors = GetNeighborOffsets(_currentShapeType, cell.X, cell.Y);

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
    private PieceRecord GetPieceRecordToGridCoord(int x, int y)
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
    private GridCoord[] GetNeighborOffsets(ShapeType type, int x, int y)
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
    private List<GridCoord> GetAbsoluteCells(PieceRecord record)
    {
        return record.Shape.Cells.Select(c => new GridCoord(record.Origin.X + c.X, record.Origin.Y + c.Y)).ToList();
    }

    // セル集合に一致する利用可能な形状を見つける（IsUpSide制約も考慮）
    private PieceShape FindMatchingShape(List<GridCoord> combinedCells)
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
    private bool IsUpSideCheckPassed(int originX, int originY, PieceShape shape)
    {
        if (_currentShapeType == ShapeType.Square) return true; // 四角形は常にOK

        if (_currentShapeType == ShapeType.Triangle)
        {
            bool isUpSide = ((originX + originY) % 2) == 0;
            if (shape.IsUpSide == 2 && isUpSide) return false; // 下向き指定なのに上向き
            if (shape.IsUpSide == 1 && !isUpSide) return false; // 上向き指定なのに下向き
        }
        else if (_currentShapeType == ShapeType.Hex)
        {
            if (shape.IsUpSide == 2 && originX % 2 == 1) return false; // X奇数用指定なのにX偶数
            if (shape.IsUpSide == 1 && originX % 2 == 0) return false; // X偶数用指定なのにX奇数
        }
        
        return true;
    }

    // ピースの置き換えを実行し、成功リストとグリッドを更新
    private void ReplacePieces(PieceRecord rec1, PieceRecord rec2, PieceShape newShape, List<GridCoord> newCells)
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