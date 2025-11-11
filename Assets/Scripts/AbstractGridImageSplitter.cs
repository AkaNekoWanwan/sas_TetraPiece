using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

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
        string folderPath = Path.Combine(basePath, imageName);

        if (!Directory.Exists(folderPath))
            return folderPath;

        int counter = 1;
        while (Directory.Exists($"{folderPath}_{counter}")) counter++;
        return $"{folderPath}_{counter}";
    }

    public virtual void SplitImage()
    {
    
    }
    public virtual ShapeType GetShapeType()
    {
        return ShapeType.Square;
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
        GridPieceListController gridPieceListController = this.transform.parent.gameObject.GetComponentInChildren<GridPieceListController>();
        gridPieceListController.isCreative = isCreative;
        gridPieceListController.gridParent = this.transform;

        // ピースセルをいい感じにピースリストに配置
        List<AnswerGridPos> cells = this.gameObject.GetComponentsInChildren<AnswerGridPos>().ToList();
        CellSplitter.CellSplit( cols, rows, ref _pieceNum, cells, gridPieceListController, GetShapeType(), PieceCreateSeed, avoidPatternSeeds );
        PieceCreateSeed = CellSplitter.PatternSeed;
        if (string.IsNullOrEmpty(backUpPieceCreateSeed))
            backUpPieceCreateSeed = PieceCreateSeed;

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
        GridPieceListController gridPieceListController = this.transform.parent.gameObject.GetComponentInChildren<GridPieceListController>();
        gridPieceListController.isCreative = isCreative;
        gridPieceListController.gridParent = this.transform;
        
        // ピースセルをいい感じにピースリストに配置
        List<AnswerGridPos> cells = this.gameObject.GetComponentsInChildren<AnswerGridPos>().ToList();

        yield return null;
        CellSplitter.CellSplit( cols, rows, ref _pieceNum, cells, gridPieceListController, GetShapeType(), PieceCreateSeed, avoidPatternSeeds );
        PieceCreateSeed = CellSplitter.PatternSeed;
        if (string.IsNullOrEmpty(backUpPieceCreateSeed))
            backUpPieceCreateSeed = PieceCreateSeed;

        // ピースのセットアップ
        gridPieceListController.SetUpChildrenPieceDragController();
    }

    public void Deletepiece()
    {
        DeleteChilden();
        GridPieceListController gridPieceListController = this.transform.parent.gameObject.GetComponentInChildren<GridPieceListController>();
        gridPieceListController.PreSetPieceDragControllers();
    }

    public void SetShapeType()
    {
        
    }
#endif
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
            script.SplitImage();
        }
        if (GUILayout.Button("Delete all childen"))
        {
            script.DeleteChilden();
        }
        if (GUILayout.Button("Delete piece"))
        {
            script.Deletepiece();
        }
        if (GUILayout.Button("Auto Create piece"))
        {
            script.CreatePiece();
        }
    }
}
#endif