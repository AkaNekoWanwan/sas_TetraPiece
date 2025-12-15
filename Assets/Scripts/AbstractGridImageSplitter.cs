using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

[ExecuteInEditMode]
[RequireComponent(typeof(Image))]
public abstract class AbstractGridImageSplitter : MonoBehaviour
{
    [Header("Grid Settings")]
    [Range(1, 20)] public int cols = 2;
    [Range(1, 20)] public int rows = 2;
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
    public Sprite _shadowSprite = default;

    private GridPieceListController _gridPieceListController = default;

    protected Coroutine _createPieceCoriutine = null;

    [Header("HexParam")]
    public float _shiftY = 0f;
    [Header("TriangleParam")]
    public Vector2 _trimShift = Vector2.zero;
    public int uniqueId = 0;
    public int index = 0;

    public string PrefabSavePath = "Assets/Prefabs/Stages"; // プレハブ保存先ディレクトリ
    
    public List<Sprite> _cellSprites = default;

#if UNITY_EDITOR
    private void OnValidate() {
        if(UnityEditor.EditorApplication.isPlaying)
            return;
            
        // Vector3 pos = this.transform.localPosition;
        // // pos.y = 3.7f;
        // this.transform.localPosition = pos;

        // GameObject shadow = SiblingFinder.FindSiblingByName(this.gameObject, "shadow");
        // if(shadow != null)
        // {
        //     pos = shadow.transform.localPosition;
        //     pos.y = 2f;
        //     shadow.transform.localPosition = pos;
        // }
    }

    protected string GetUniqueFolder(string basePath, string imageName)
    {
        // ユニークIDを割り当てる
        GridImageSplitterUniqueIdManager UniqueIdManager = (GridImageSplitterUniqueIdManager)FindAnyObjectByType (typeof(GridImageSplitterUniqueIdManager));
        if(UniqueIdManager != null)
            UniqueIdManager.AssignUniqueIds(this);

        // 1. ShapeTypeに応じた接尾辞を取得
        string shapeTypeName = GetShapeType().ToString(); // ShapeType.Square -> "Square"

        basePath = $"Assets/Textures/PieceCells";
        // 2. 最終的なフォルダ名を構築: "Assets/Textures" + "画像名" + "ShapeType名"
        string finalFolderName = $"{uniqueId}";
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
        
        // _cellSprites.Add(sp);

        return folderPath;
    }

    public virtual void SplitImage()
    {

    }

    // public int text = 0;
    protected bool IsDummyAnswerOnly(int x, int y)
    {
        // Debug.Log($"info : {text}");
        // text++;
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
        BeforeSplit();
        Split(true);
        AfterSplit();
    }

    public async Task CreatePieceAsync()
    {
        // 1. メインスレッドで実行する必要がある前処理 (Unity APIを含む)
        BeforeSplit(); 
        
        // 2. ピース配置計算に必要なデータを取得 (これもメインスレッドで行う)
        List<AnswerGridPos> cells = this.gameObject.GetComponentsInChildren<AnswerGridPos>().ToList();
        
        // 3. 重い計算処理を別スレッドで実行
        await Task.Run(() => 
        {
            // 【純粋な計算】
            _myCellSplitter = new CellSplitter2(GetShapeType());
            // CellSplit(純粋計算)に、Unity APIの結果である cells を渡す
            _myCellSplitter.CellSplit( cols, rows, ref _pieceNum, PieceCreateSeed, avoidPatternSeeds );
        });
        _myCellSplitter.SetUpSplitPieceData( ref _pieceNum, cells, _gridPieceListController);
        PieceCreateSeed = _myCellSplitter.PatternSeed;
        isSkip = true;

        // gridPieceListController.pieceNum = finalPieceCount;
        // RegisterCellsAsPieces(pieceList, cells);
        
        // 4. メインスレッドに戻り、後処理（UI更新、Prefab保存など）
        AfterSplit(); 
    }

        // ステージ作成に必要な一連の流れを実行
    public IEnumerator CreatePieceCoroutine()
    {
        yield return null;
        BeforeSplit();
        yield return null;
        Split(false);
        yield return null;
        AfterSplit();
        yield break;
    }

    // メイン処理の前処理
    private void BeforeSplit()
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
        _gridPieceListController = GetGridPieceListController();
        if(_gridPieceListController != null)
        {
            _gridPieceListController.isCreative = isCreative;
            _gridPieceListController.gridParent = this.transform;
            _gridPieceListController.ShapeType = GetShapeType();
            _gridPieceListController.IsSetShapeType = true;
        }
    }
    private void Split(bool isStatic)
    {
        List<AnswerGridPos> cells = this.gameObject.GetComponentsInChildren<AnswerGridPos>().ToList();
        _pieceNum = -1;
        if(isStatic)
        {
            // ピースセルをいい感じにピースリストに配置
            CellSplitter.CellSplit( cols, rows, ref _pieceNum, cells, _gridPieceListController, GetShapeType(), PieceCreateSeed, avoidPatternSeeds );
            PieceCreateSeed = CellSplitter.PatternSeed;
        }
        else
        {
            // ピースセルをいい感じにピースリストに配置
            _myCellSplitter = new CellSplitter2(GetShapeType());
            _myCellSplitter.CellSplit( cols, rows, ref _pieceNum, cells, _gridPieceListController, PieceCreateSeed, avoidPatternSeeds );
            PieceCreateSeed = _myCellSplitter.PatternSeed;
        }
    }
    // メイン処理の後処理
    private void AfterSplit()
    {
        if (string.IsNullOrEmpty(backUpPieceCreateSeed))
            backUpPieceCreateSeed = PieceCreateSeed;

        isSkip = true;
        // ピースのセットアップ
        _gridPieceListController.SetUpChildrenPieceDragController();
        SaveAsPrefab.Save(this.transform.parent.parent.gameObject, PrefabSavePath);
    }

    public void Deletepiece()
    {
        DeleteChilden();
        _gridPieceListController = GetGridPieceListController();
        if(_gridPieceListController != null)
            _gridPieceListController.PreSetPieceDragControllers();
    }

    public GridPieceListController GetGridPieceListController()
    {
        return this.transform.parent.gameObject.GetComponentInChildren<GridPieceListController>();
    }

    // セルサイズ270x270を基準に、それより小さいほどピースサイズを大きくして補正する
    // public void SetCellScale(float size)
    // {
    //     GridPieceListController _gridPieceListController = GetGridPieceListController();
    //     _gridPieceListController._PieceDragControllersScale = 0.45f * (270f / size);
    // }

    public void SetCellScale(float size)
    {
        _gridPieceListController = GetGridPieceListController();
        // _gridPieceListController._PieceDragControllersScale = 0.45f * (270f / size);
        if(_gridPieceListController != null)
        {
            _gridPieceListController._PieceDragControllersScale = 0.67f * 185f / size;
            if(GetShapeType() == ShapeType.Square)
                _gridPieceListController._PieceDragControllersScale *= 0.75f;
            else
                _gridPieceListController._PieceDragControllersScale *= 1f;
        }
    }

    public void SetShapeType()
    {
        
    }
#endif
    public virtual ShapeType GetShapeType()
    {
        return ShapeType.Square;
    }

    protected void CreateShadow(AnswerGridPos answerGridPos, Vector2 size)
    {
        GameObject shadowObj = new GameObject("shadow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        shadowObj.transform.SetParent(answerGridPos.transform, false);

        Image shadowImg = shadowObj.GetComponent<Image>();
        shadowImg.sprite = _shadowSprite;
        shadowImg.color = new Color32(0, 0, 0, 50);
        shadowImg.material = _param.ShadowMaterial;

        RectTransform shadowRT = shadowObj.GetComponent<RectTransform>();
        shadowRT.localPosition = new Vector3(0f, -24f, 0f);
        shadowRT.localScale = Vector3.one;
        shadowRT.sizeDelta = new Vector2( size.x * 355f / 270f, size.y * 355f / 270f);
        answerGridPos.shadowTransform = shadowObj.transform;

        if(answerGridPos.isUpSide)
        {
            shadowRT.localScale = new Vector3(1f, -1f, 1f);
        }
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