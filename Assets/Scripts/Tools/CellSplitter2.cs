using UnityEngine;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System; // Guidを使うために必要
using System.Security.Cryptography; // シード値生成のために追加

// 生成したセルをオートでいい感じにピースに分けるクラス

/// <summary>
// 本体クラス
/// </summary>
public class CellSplitter2
{
// 以前Staticだったフィールドをインスタンスフィールドに変更
    private int _gridX = 6; 
    private int _gridY = 6;
    private int _targetPieceCount = 10;
    private int[,] _grid;
    private List<PieceShape> _availableShapes;
    private int _pieceIdCounter; 
    private List<PieceRecord> _successfulPlacements = new List<PieceRecord>();
    private ShapeType _currentShapeType;
    private System.Random _random;
    private int _randomSeed;
    private string _patternSeed;

    private bool _isPatternSeedActive = false;
    private List<string> _pieceNameSequence;
    private List<GridCoord> _originCoordSequence;
    private int _placementIndex = 0;
    
    // PatternSeedのGetterをインスタンスメソッドとして提供
    public string PatternSeed => _patternSeed;
    
    // CellSplitメソッドをインスタンスメソッドに変更し、初期値をコンストラクタで受け取る
    // public void CellSplit(int cols, int rows, ref int orderPieceNum, List<AnswerGridPos> cells, GridPieceListController gridPieceListController, ShapeType type, string patternSeed = null, List<string> avoidPatternSeeds = null)
    // 簡略化のため、新しいシグネチャを提案します。

    // 【コンストラクタの追加】
    public CellSplitter2(int cols, int rows, int targetPieceNum, ShapeType type, string patternSeed)
    {
        _gridX = cols;
        _gridY = rows;
        _targetPieceCount = targetPieceNum;
        _currentShapeType = type;
        _patternSeed = patternSeed;
    }
    
    // 配置されたピースの情報
    public struct PieceRecord
    {
        public PieceShape Shape;
        public GridCoord Origin;
        public int PieceId;
    }
    // CellSplitメソッドの修正 (Staticな変数へのアクセスをインスタンス変数へ変更)
    public void SplitAndRegisterCells(List<AnswerGridPos> cells, GridPieceListController gridPieceListController, List<string> avoidPatternSeeds = null)
    {
        // ピース生成のパラメーターセットはコンストラクタで完了済み
        
        // 乱数生成器の数値シードを決定
        _randomSeed = GetRandomIntSeed(); 
        _random = new System.Random(_randomSeed);

        // ピース情報の生成 (インスタンス変数を参照するように修正)
        CreatePiecePlacements(_patternSeed, avoidPatternSeeds);

        // 作成したピース情報をもとにピースオブジェクトに反映させる
        // コントローラーの前準備
        // (注意: ref _pieceNumの処理は不要となり、戻り値やプロパティで処理します)
        int orderPieceNum = _successfulPlacements.Count;
        gridPieceListController.pieceNum = orderPieceNum;
        bool backupFlg = gridPieceListController.isOverrayPieceNum;
        gridPieceListController.isOverrayPieceNum = false;
        gridPieceListController.PreSetPieceDragControllers();
        gridPieceListController.isOverrayPieceNum = backupFlg;
        
        // セルを対応するピースの子オブジェクトにする
        List<PieceDragController> pieceList = gridPieceListController.gameObject.GetComponentsInChildren<PieceDragController>().ToList();
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
    private AnswerGridPos FindCell(List<AnswerGridPos> cells, int searchX, int searchY)
    {
        AnswerGridPos cell = cells.FirstOrDefault(c => c.x == searchX && c.y == searchY);
        return cell;
    }

    private void CreatePiecePlacements(string patternSeed = null, List<string> avoidPatternSeeds = null)
    {
        // 1. ピース形状の定義と生成
        switch (_currentShapeType)
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
                        _patternSeed = newPatternSeed;
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
            Debug.Log($"<color=green>敷き詰め完了！</color> 最終ピース数: {_pieceIdCounter - 1}, 使用パターンシード: {_patternSeed}");
            if(isRandom)
            {
                // ランダム探索で成功した場合、新しいパターンシードを生成・更新
                _patternSeed = EncodePlacement(_successfulPlacements); // インスタンス変数に代入
            }
        }
        else
        {
            Debug.LogError($"全パス失敗。グリッドサイズ ({_gridX}x{_gridY}) は敷き詰め不可能です。");
        }
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
        _grid = new int[_gridX, _gridY];
        _pieceIdCounter = 1;
        _successfulPlacements.Clear();
        ResetPieceUsage(); // ピースの使用フラグをリセット
        _placementIndex = 0; // **インデックスをリセット**
        
        // ランダム探索がブレないよう、乱数生成器もリセット（再初期化）
        _random = new System.Random(_randomSeed);
    }


    // ========== ピース形状の定義例（ポリオミノ） ==========
    // 四角形セル用
    private void InitializeShapesSquare()
    {
        _availableShapes = new List<PieceShape>();

        // 1セル (I-1)
        int maxUseI1 = _random.Next(0, 2);
        _availableShapes.Add(new PieceShape("1A", new List<GridCoord> { new GridCoord(0, 0) }, maxUseI1));

        // 2セル (I-2)
        _availableShapes.Add(new PieceShape("I2-A", new List<GridCoord> { new GridCoord(0, 0), new GridCoord(1, 0) }));
        _availableShapes.Add(new PieceShape("I2-B", new List<GridCoord> { new GridCoord(0, 0), new GridCoord(0, 1) }));

        // 3セル (I-3, L-3, V-3)
        _availableShapes.Add(new PieceShape("I3-A", new List<GridCoord> { new GridCoord(0, 0), new GridCoord(1, 0), new GridCoord(2, 0) }));
        _availableShapes.Add(new PieceShape("I3-B", new List<GridCoord> { new GridCoord(0, 0), new GridCoord(0, 1), new GridCoord(0, 2) }));
        _availableShapes.Add(new PieceShape("L3-A", new List<GridCoord> { new GridCoord(0, 0), new GridCoord(1, 0), new GridCoord(0, 1) }));
        _availableShapes.Add(new PieceShape("」3-B", new List<GridCoord> { new GridCoord(0, 0), new GridCoord(1, 0), new GridCoord(1, 1) }));
        _availableShapes.Add(new PieceShape("「3-C", new List<GridCoord> { new GridCoord(0, 0), new GridCoord(0, 1), new GridCoord(1, 1) }));
        _availableShapes.Add(new PieceShape("73-D", new List<GridCoord> { new GridCoord(1, 0), new GridCoord(0, 1), new GridCoord(1, 1) }));
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
    private void InitializeShapesHex()
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
    private void InitializeShapesTriangle()
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
        _availableShapes.Add(new PieceShape("/3-B", new List<GridCoord> { new GridCoord(0, 0), new GridCoord(1, 0), new GridCoord(1, 1) }, -1, 2));
        _availableShapes.Add(new PieceShape("/3-C", new List<GridCoord> { new GridCoord(1, 0), new GridCoord(1, 1), new GridCoord(0, 1) }, -1, 2));
        _availableShapes.Add(new PieceShape("/3-D", new List<GridCoord> { new GridCoord(2, 0), new GridCoord(1, 0), new GridCoord(1, 1) }, -1, 2));

        _availableShapes.Add(new PieceShape("/4-A", new List<GridCoord> { new GridCoord(0, 0), new GridCoord(1, 0), new GridCoord(1, 1), new GridCoord(2, 1) }, -1, 2));
        _availableShapes.Add(new PieceShape("/4-B", new List<GridCoord> { new GridCoord(0, 1), new GridCoord(1, 1), new GridCoord(1, 0), new GridCoord(2, 0) }, -1, 2));
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

    private int SubMaxUse(int value, int subNum)
    {
        int ret = Mathf.Max(value - subNum, 0);
        return ret;
    }

    // ========== バックトラッキングの中核ロジック ==========

    // 次の空きセルを探す (左上から順に)
    private bool FindNextEmptyCell(out int nextX, out int nextY)
    {
        for (int y = 0; y < _gridY; y++)
        {
            for (int x = 0; x < _gridX; x++)
            {
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
    private bool Solve(int startX, int startY, bool enforceCount, bool enforceUnique, bool isPatternSeedActive = false) // 引数を追加
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
                    if (enforceCount && _pieceIdCounter - 1 != _targetPieceCount) return false;
                    
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
            if (enforceCount && _pieceIdCounter - 1 != _targetPieceCount) return false;
            // ランダム探索で成功した場合、CreatePiecePlacementsでPatternSeedを更新する
            return true;
        }

        // Count厳守モードで、ピース数が上限を超えた場合は失敗 (早期終了)
        if (enforceCount && _pieceIdCounter - 1 >= _targetPieceCount)
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
            if (_currentShapeType == ShapeType.Triangle)
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
                // ★★★ 枝刈りチェックを追加 ★★★
                // if (IsFeasible())
                {
                    // 2. 次のセルへ再帰
                    if (Solve(startX, startY, enforceCount, enforceUnique, isPatternSeedActive))
                    {
                        return true; // 成功
                    }
                }
                // ★★★ 実行不可能なら、即座に後戻り（枝刈り）★★★

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
    private void AnalysisPatternSeed(string seed)
    {
        _isPatternSeedActive = false; // 初期化
        if (string.IsNullOrEmpty(seed)) return;

        // シード形式: "GX-GY-TC-Type|Name1:X1,Y1|Name2:X2,Y2|..."
        string[] headerAndData = seed.Split('|');

        // ヘッダー (GX-GY-TC-Type) の解析
        string[] headerParts = headerAndData[0].Split('-');
        if (headerParts.Length != 4) return;

        // パラメータの確認
        if (int.TryParse(headerParts[0], out int decodedX) && decodedX == _gridX &&
            int.TryParse(headerParts[1], out int decodedY) && decodedY == _gridY &&
            int.TryParse(headerParts[2], out int decodedCount) && decodedCount == _targetPieceCount &&
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
        }
    }
    
    /// <summary>
    /// 成功した配置結果をパターンシード文字列にエンコードする
    /// </summary>
    private string EncodePlacement(List<PieceRecord> placements)
    {
        // ヘッダー: "GX-GY-TC-Type"
        int shapeInt = (int)_currentShapeType;
        string seed = $"{_gridX}-{_gridY}-{_targetPieceCount}-{shapeInt}";
        
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
    
    // ピースが(x, y)に配置可能かチェック
    private bool CanPlace(int originX, int originY, PieceShape shape)
    {
        // ピースを構成するセルの位置
        foreach (var cell in shape.Cells)
        {
            int x = originX + cell.X;
            int y = originY + cell.Y;
            
            // グリッド範囲外、またはすでに埋まっているセルと重複する場合は配置不可
            if (x < 0 || x >= _gridX || y < 0 || y >= _gridY || _grid[x, y] != 0)
            {
                return false;
            }
        }
        
        // TODO: 2x2などの大きな長方形の形成チェックをここに追加する（非常に複雑）
        // 現時点では、ピースの定義段階でその制約を満たしていることを前提としています。
        
        return true;
    }

    // ピースをグリッドに配置
    private void PlacePiece(int originX, int originY, PieceShape shape)
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
    private void RemovePiece(int originX, int originY, PieceShape shape)
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
    private bool IsUniqueSeed(string newSeed, List<string> avoidSeeds)
    {
        return !avoidSeeds.Contains(newSeed);
    }
    
    /// <summary>
    /// _gridX, _gridY, TargetPieceCountの情報を埋め込んだ文字列シードを生成する
    /// </summary>
    public string EncodeSeed(int gridX, int gridY, int targetCount, ShapeType shapeType)
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
        bool[,] visited = new bool[_gridX, _gridY];

        for (int y = 0; y < _gridY; y++)
        {
            for (int x = 0; x < _gridX; x++)
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

                if (nx >= 0 && nx < _gridX && ny >= 0 && ny < _gridY && 
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