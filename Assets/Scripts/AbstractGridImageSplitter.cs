using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

[ExecuteInEditMode]
[RequireComponent(typeof(Image))]
public abstract class AbstractGridImageSplitter : MonoBehaviour
{
    [Header("Grid Settings")]
    [Range(1, 20)] public int rows = 2;
    [Range(1, 20)] public int cols = 2;
    [Range(2, 30)] public int _pieceNum = 5;

    [Header("Target Range (Center % of image)")]
    [Range(10, 1000)] public int targetPercent = 80;

    [Header("Output Settings")]
    public string outputFolder = "Assets/Textures/Square";

    [Header("Cell Copy Settings")]
    public Material cellCopyMaterial;
    public SpritterParam _param;

    public bool isSkip = true;   // 全更新時にスキップするか
    public bool isPrefs = false;   // 全更新時にスキップするか
    public bool isCreative = false;
    public string PieceCreateSeed = ""; // ピース作成のシード値
    public string backUpPieceCreateSeed = ""; // ピース作成のシード値のバックアップ
    public List<string> avoidPatternSeeds = default;

    protected Coroutine _createPieceCoriutine = null;

    [Header("HexParam")]
    public float _shiftY = 0f;
    [Header("TriangleParam")]
    public Vector2 _trimShift = Vector2.zero;

#if UNITY_EDITOR
    protected string GetUniqueFolder(string basePath, string imageName)
    {
        // 1. ShapeTypeに応じた接尾辞を取得
        string shapeTypeName = GetShapeType().ToString(); // ShapeType.Square -> "Square"

        // 2. 最終的なフォルダ名を構築: "Assets/Textures" + "画像名" + "ShapeType名"
        string finalFolderName = $"{imageName}_{shapeTypeName}";
        string folderPath = Path.Combine(basePath, finalFolderName);

        // 3. 同名フォルダが存在するかチェック
        if (Directory.Exists(folderPath))
        {
            // 4. 存在する場合、フォルダの中身を全て削除
            Debug.Log($"同名フォルダが存在するため中身を全削除します: {folderPath}");
            
            // Directory.Delete はフォルダ自体も削除できるが、中身を再帰的に削除するために true を使用
            // 削除後、すぐに再作成するため、ここではフォルダ自体も一度削除してから作成する
            Directory.Delete(folderPath, true);
        }

        // 5. 新規でフォルダを作成（削除されていれば再作成）
        Directory.CreateDirectory(folderPath);
        
        // Unityエディタが新しいフォルダを認識できるようにアセットデータベースを更新
        AssetDatabase.Refresh();

        return folderPath;
    }

    public virtual void SplitImage()
    {
    
    }

    public int text = 0;
    protected bool IsDummyAnswerOnly(int x, int y)
    {
        Debug.Log($"info : {text}");
        text++;
        if(!isCreative)
            return false;
        if( x < 0 || y < 0 || cols <= x || rows <= y)
            return true;
        return false;
    }

    public void DeleteChilden()
    {
        for (int j = this.transform.childCount - 1; j >= 0; j--)
        {
            Transform child = this.transform.GetChild(j);
            if (child != null)
            {
                DestroyImmediate(child.gameObject, true);
            }
        }
    }

    // 【新規追加】自身のCellSplitterインスタンスを保持
    private CellSplitter2 _myCellSplitter;
    // ステージ作成に必要な一連の流れを実行
    public void CreatePiece()
    {
        // 設定されているピース数が大き過ぎたら修正
        int maxPieceNum = rows * cols;
        _pieceNum = Mathf.Min(_pieceNum, maxPieceNum);
        // 設定されているピース数がちいさすぎたら修正
        _pieceNum = Mathf.Max(_pieceNum, 2);

        // 子オブジェクトを全削除
        DeleteChilden();
        // ピースセル生成
        SplitImage();
        

        // 同じ階層のGridPieceListControllerを取得
        GridPieceListController gridPieceListController = GetGridPieceListController();
        gridPieceListController.isCreative = isCreative;
        gridPieceListController.gridParent = this.transform;
        gridPieceListController.ShapeType = GetShapeType();
        gridPieceListController.IsSetShapeType = true;

        // ピースセルをいい感じにピースリストに配置
        List<AnswerGridPos> cells = this.gameObject.GetComponentsInChildren<AnswerGridPos>().ToList();
        // CellSplitter.CellSplit( cols, rows, ref _pieceNum, cells, gridPieceListController, GetShapeType(), PieceCreateSeed, avoidPatternSeeds );
        // ★★★ 修正点 1: CellSplitterをインスタンス化 ★★★
        _myCellSplitter = new CellSplitter2(cols, rows, _pieceNum, GetShapeType(), PieceCreateSeed);
        // ★★★ 修正点 2: インスタンスメソッドを呼び出し ★★★
        _myCellSplitter.SplitAndRegisterCells(cells, gridPieceListController, null); 
        // Note: _pieceNumはCellSplitter内部で参照されるため、refは不要

        // ★★★ 修正点 3: 結果のPatternSeedをインスタンスから取得 ★★★
        PieceCreateSeed = _myCellSplitter.PatternSeed;
        if (string.IsNullOrEmpty(backUpPieceCreateSeed))
            backUpPieceCreateSeed = PieceCreateSeed;

        // CellSplitter.CellSplit( cols, rows, ref _pieceNum, cells, gridPieceListController, GetShapeType(), PieceCreateSeed, null );
        // PieceCreateSeed = CellSplitter.PatternSeed;
        // if (string.IsNullOrEmpty(backUpPieceCreateSeed))
        //     backUpPieceCreateSeed = PieceCreateSeed;

        // ピースのセットアップ
        gridPieceListController.SetUpChildrenPieceDragController();
    }

        // ステージ作成に必要な一連の流れを実行
    public IEnumerator CreatePieceCoroutine()
    {
        // 設定されているピース数が大き過ぎたら修正
        int maxPieceNum = rows * cols;
        _pieceNum = Mathf.Min(_pieceNum, maxPieceNum);
        // 設定されているピース数がちいさすぎたら修正
        _pieceNum = Mathf.Max(_pieceNum, 2);

        // 子オブジェクトを全削除
        DeleteChilden();
        // ピースセル生成
        SplitImage();

        // 同じ階層のGridPieceListControllerを取得
        GridPieceListController gridPieceListController = GetGridPieceListController();
        gridPieceListController.isCreative = isCreative;
        gridPieceListController.gridParent = this.transform;
        
        // ピースセルをいい感じにピースリストに配置
        List<AnswerGridPos> cells = this.gameObject.GetComponentsInChildren<AnswerGridPos>().ToList();

        _myCellSplitter = new CellSplitter2(cols, rows, _pieceNum, GetShapeType(), PieceCreateSeed);

        yield return null;
        
        _myCellSplitter.SplitAndRegisterCells(cells, gridPieceListController, null);
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        PieceCreateSeed = _myCellSplitter.PatternSeed;
        isSkip = true;

        // ピースのセットアップ
        gridPieceListController.SetUpChildrenPieceDragController();

        yield break;
    }

    public void Deletepiece()
    {
        DeleteChilden();
        GridPieceListController gridPieceListController = GetGridPieceListController();
        gridPieceListController.PreSetPieceDragControllers();
    }

    public GridPieceListController GetGridPieceListController()
    {
        return this.transform.parent.gameObject.GetComponentInChildren<GridPieceListController>();
    }

    // セルサイズ270x270を基準に、それより小さいほどピースサイズを大きくして補正する
    public void SetCellScale(float size)
    {
        GridPieceListController gridPieceListController = GetGridPieceListController();
        gridPieceListController._PieceDragControllersScale = 0.45f * (270f / size);
    }

    public void SetShapeType()
    {
        
    }
#endif
    public virtual ShapeType GetShapeType()
    {
        return ShapeType.Square;
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(AbstractGridImageSplitter), true)]
public class AbstractGridImageSplitterEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        AbstractGridImageSplitter script = (AbstractGridImageSplitter)target;

        GUILayout.Space(10);

        if (GUILayout.Button("Split Image"))
        {
            script.Deletepiece();
            script.SplitImage();
            script.isSkip = false;
        }
        if (GUILayout.Button("Delete piece"))
        {
            script.Deletepiece();
            script.isSkip = false;
        }
        if (GUILayout.Button("Auto Create piece"))
        {
            script.CreatePiece();
        }
    }
}
#endif