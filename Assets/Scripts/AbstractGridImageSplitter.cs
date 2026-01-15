using UnityEngine;
using UnityEngine.UI;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.SceneManagement;
using UnityEditor.AddressableAssets.Settings;
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
    [Range(0f, 2f)] public float fixTargetPercentCellSize = 1f;

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
    
    // プレハブアンパック前のパスを保持（再接続用）
    private string _originalPrefabPath = null;

    [Header("TriangleParam")]
    public Vector2 _trimShift = Vector2.zero;
    public int uniqueId = 0;
    public int index = 0;

    public Action<int, int, string> OnUpdateProgressBar;

    public string PrefabSavePath = "Assets/Prefabs/Stages"; // プレハブ保存先ディレクトリ

    [SerializeField, Tooltip("生成したセルのリスト")] List<AnswerGridPos> _cells = null;

    protected Image _splitImage;

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

        return folderPath;
    }

    public virtual void SplitImage()
    {
        _splitImage = this.gameObject.GetComponent<Image>();
        if(_splitImage == null)
        {
            Debug.LogError("Image コンポーネントが見つかりません。");
            return;
        }
        // spriteがnullの場合、AddressableImageLoaderから読み込んでみる(addressable経由でなく通常の読み込み)
        if(_splitImage.sprite == null)
        {
            AddressableImageLoader addressableImageLoader = this.gameObject.GetComponent<AddressableImageLoader>();
            if(addressableImageLoader != null)
            {
                string addressName = addressableImageLoader.addressName;
                if(!string.IsNullOrEmpty(addressName))
                {
                    Sprite loadedSprite = AssetDatabase.LoadAssetAtPath<Sprite>(addressName);
                    if(loadedSprite != null)
                    {
                        _splitImage.sprite = loadedSprite;
                    }
                }
            }
        }

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

    public void DeleteChildren()
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
    // isReSetPiecesOnly: trueの場合、既存セルを再利用してピース配置のみ再生成
    public void CreatePiece(bool isReSetPiecesOnly = false)
    {
        if(isReSetPiecesOnly)
        {
            // 既存のセルを再利用してピース配置のみ更新
            OnUpdateProgressBar?.Invoke(0, 100, "既存セルを収集中...");
            
            // GridPieceListController配下に移動済みのセルを再収集
            _gridPieceListController = GetGridPieceListController();
            if(_gridPieceListController != null)
            {
                // まずピース配下のセルを探す（Split後の状態）
                var pieceDragControllers = _gridPieceListController.GetComponentsInChildren<PieceDragController>(true);
                _cells = new List<AnswerGridPos>();
                foreach (var piece in pieceDragControllers)
                {
                    var cellsInPiece = piece.GetComponentsInChildren<AnswerGridPos>(true);
                    _cells.AddRange(cellsInPiece);
                }
                
                // ピース配下に見つからなければ、this.gameObject配下を探す（画像分割直後の状態）
                if(_cells.Count == 0)
                {
                    Debug.Log("ピース配下にセルが見つからなかったため、画像分割直後のセルを収集します");
                    _cells = this.gameObject.GetComponentsInChildren<AnswerGridPos>(true).ToList();
                }
                
                if(_cells.Count == 0)
                {
                    Debug.LogError("再配置可能なセルが見つかりませんでした。先に画像分割を実行してください。");
                    return;
                }
                
                Debug.Log($"既存セル {_cells.Count}個を再利用してピース再配置を実行します");
                
                _gridPieceListController.isCreative = isCreative;
                _gridPieceListController.gridParent = this.transform;
                _gridPieceListController.ShapeType = GetShapeType();
                _gridPieceListController.IsSetShapeType = true;
            }
        }
        else
        {
            // 画像からピースのセルを新規生成
            OnUpdateProgressBar?.Invoke(0, 100, "画像分割処理中...");
            SplitImageProcess();
        }
        
        // ピースセルをいい感じに組み合わせてピースとしてまとめる
        OnUpdateProgressBar?.Invoke(33, 100, "ピース生成中...");
        
        // ★ ピース配置処理の前にプレハブインスタンスをアンパック
        // （セルの親変更が必要なため、プレハブインスタンスのままでは実行できない）
        GameObject stageRoot = this.transform.parent.parent.gameObject;
        bool wasPrefabInstance = PrefabUtility.IsPartOfPrefabInstance(stageRoot);
        _originalPrefabPath = null;
        
        if (wasPrefabInstance)
        {
            var prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(stageRoot);
            _originalPrefabPath = AssetDatabase.GetAssetPath(prefabAsset);
            PrefabUtility.UnpackPrefabInstance(stageRoot, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            Debug.Log($"[CreatePiece] Prefabインスタンスをアンパックしました: {_originalPrefabPath}");
        }
        
        Split(isStatic: true, shouldClearCells: !isReSetPiecesOnly);
        
        // ピースのセットアップとPrefab保存
        OnUpdateProgressBar?.Invoke(67, 100, "ピース設定とプレハブ保存中...");
        AfterSplit(stageRoot);
    }

    public async Task CreatePieceAsync()
    {
        // 1. メインスレッドで実行する必要がある前処理 (Unity APIを含む)
        SplitImageProcess(); 
        
        // ★ プレハブインスタンスをアンパック
        GameObject stageRoot = this.transform.parent.parent.gameObject;
        bool wasPrefabInstance = PrefabUtility.IsPartOfPrefabInstance(stageRoot);
        _originalPrefabPath = null;
        
        if (wasPrefabInstance)
        {
            var prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(stageRoot);
            _originalPrefabPath = AssetDatabase.GetAssetPath(prefabAsset);
            PrefabUtility.UnpackPrefabInstance(stageRoot, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            Debug.Log($"[CreatePieceAsync] Prefabインスタンスをアンパックしました: {_originalPrefabPath}");
        }
        
        // 2. ピース配置計算に必要なデータを取得 (これもメインスレッドで行う)
        // List<AnswerGridPos> cells = this.gameObject.GetComponentsInChildren<AnswerGridPos>().ToList();
        
        // 3. 重い計算処理を別スレッドで実行
        await Task.Run(() => 
        {
            // 【純粋な計算】
            _myCellSplitter = new CellSplitter2(GetShapeType());
            // CellSplit(純粋計算)に、Unity APIの結果である cells を渡す
            _myCellSplitter.CellSplit( cols, rows, ref _pieceNum, PieceCreateSeed, avoidPatternSeeds );
        });
        _myCellSplitter.SetUpSplitPieceData( ref _pieceNum, _cells, _gridPieceListController);
        PieceCreateSeed = _myCellSplitter.PatternSeed;
        isSkip = true;

        // gridPieceListController.pieceNum = finalPieceCount;
        // RegisterCellsAsPieces(pieceList, cells);
        
        // 4. メインスレッドに戻り、後処理（UI更新、Prefab保存など）
        AfterSplit(stageRoot); 
    }

        // ステージ作成に必要な一連の流れを実行
    public IEnumerator CreatePieceCoroutine()
    {
        yield return null;
        SplitImageProcess();
        yield return null;
        
        // ★ プレハブインスタンスをアンパック
        GameObject stageRoot = this.transform.parent.parent.gameObject;
        bool wasPrefabInstance = PrefabUtility.IsPartOfPrefabInstance(stageRoot);
        _originalPrefabPath = null;
        
        if (wasPrefabInstance)
        {
            var prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(stageRoot);
            _originalPrefabPath = AssetDatabase.GetAssetPath(prefabAsset);
            PrefabUtility.UnpackPrefabInstance(stageRoot, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            Debug.Log($"[CreatePieceCoroutine] Prefabインスタンスをアンパックしました: {_originalPrefabPath}");
        }
        
        Split(false);
        yield return null;
        AfterSplit(stageRoot);
        yield break;
    }

    // セル(再)生成
    public void SplitImageProcess()
    {
        Deletepiece();
        // 設定されているピース数が大き過ぎたら修正
        int maxPieceNum = rows * cols;
        _pieceNum = Mathf.Min(_pieceNum, maxPieceNum);
        // 設定されているピース数がちいさすぎたら修正
        _pieceNum = Mathf.Max(_pieceNum, 2);

        OnUpdateProgressBar?.Invoke(0, 100, "DeleteChildren...");

        // 子オブジェクトを全削除
        DeleteChildren();

        OnUpdateProgressBar?.Invoke(5, 100, "image split pre setup...");
        _trimShift = new Vector2(0f, 0f);
        // 各ステージごとの微調整
        // できれば調整が不要になるようCreatePieceを改善したい
        // 現仕様では1~9ステージと、12~27ステージ(3ステージごと)を確認して調整すると全パターン対応可能
        // iD:3 = 3x4　1,4,7ステージなど
        // if(cols == 3 && rows == 4)
        // {
        //     if( GetShapeType() == ShapeType.Square)
        //     {
        //         fixTargetPercentCellSize = 0.995f;
        //         _trimShift = new Vector2(0f, 1f);
        //     }
        //     if( GetShapeType() == ShapeType.Triangle)
        //     {
        //         _trimShift = new Vector2(324f, 87f);
        //     }
        //     if( GetShapeType() == ShapeType.Hex)
        //     {
        //         targetPercent = 110;
        //         _trimShift = new Vector2(0f, -1.45f);
        //     }
        // }
        // // iD:4 = 4x5 2,5,8ステージなど
        // if(cols == 4 && rows == 5)
        // {
        //     if( GetShapeType() == ShapeType.Square)
        //     {
        //         fixTargetPercentCellSize = 0.995f;
        //         _trimShift = new Vector2(0f, 1f);
        //     }
        //     if( GetShapeType() == ShapeType.Triangle)
        //     {
        //         _trimShift = new Vector2(324f, 87f);
        //         fixTargetPercentCellSize = 0.997f;
        //     }
        //     if( GetShapeType() == ShapeType.Hex)
        //     {
        //         targetPercent = 115;
        //         _trimShift = new Vector2(0f, -1.2f);
        //     }
        // }
        // // iD:5 = 5x7(四角六角)　3,9ステージなど
        // if(cols == 5 && rows == 7)
        // {
        //     if( GetShapeType() == ShapeType.Hex)
        //     {
        //         targetPercent = 115;
        //         _trimShift = new Vector2(0f, -0.88f);
        //     }
        //     if( GetShapeType() == ShapeType.Square)
        //     {
        //         fixTargetPercentCellSize = 0.995f;
        //         _trimShift = new Vector2(0f, 1f);
        //     }
        // }
        // // iD:5 = 6x6(三角) 6sテージなど
        // if(cols == 6 && rows == 6)
        // {
        //     if( GetShapeType() == ShapeType.Triangle)
        //         _trimShift = new Vector2(267f, 87f);
        // }
        // // iD:6 = 6x8(四角六角) 12,18ステージなど
        // if(cols == 6 && rows == 8)
        // {
        //     if( GetShapeType() == ShapeType.Hex)
        //     {
        //         _trimShift = new Vector2(0f, -0.78f);
        //         fixTargetPercentCellSize = 0.995f;
        //     }
        //     // 四角は特に調整不要
        //     if( GetShapeType() == ShapeType.Square)
        //     {
        //         _trimShift = new Vector2(0f, 0f);
        //         targetPercent = 100;
        //         fixTargetPercentCellSize = 1f;
        //     }
        // }
        // // iD:6 = 7x7(三角) 15ステージなど
        // if(cols == 7 && rows == 7)
        // {
        //     if( GetShapeType() == ShapeType.Triangle)
        //         _trimShift = new Vector2(278f, 87f);
        // }
        // // iD:7 = 7x8(四角六角) 21,27ステージなど
        // if(cols == 7 && rows == 8)
        // {
        //     if( GetShapeType() == ShapeType.Square)
        //     {
        //         targetPercent = 88;
        //         fixTargetPercentCellSize = 0.995f;
        //         _trimShift = new Vector2(0f, 0f);
        //     }
        //     if( GetShapeType() == ShapeType.Hex)
        //     {
        //         targetPercent = 105;
        //         _trimShift = new Vector2(0f, -0.69f);
        //     }
        // }
        // // iD:7 = 8x7(三角) 24ステージなど
        // if(cols == 8 && rows == 7)
        // {
        //     if( GetShapeType() == ShapeType.Triangle)
        //     {
        //         _trimShift = new Vector2(312f, 175f);
        //         targetPercent = 131;
        //         fixTargetPercentCellSize = 0.9925f;
        //     }
        // }
        // // iD:8 = 7x9(四角六角) デイリーステージなど
        // if(cols == 7 && rows == 9)
        // {
        //     _trimShift = new Vector2(0f, 0f);
        //     if( GetShapeType() == ShapeType.Hex)
        //     {
        //         _trimShift = new Vector2(0f, -0.7f);
        //     }
        // }
        // // iD:8 = 8x8(三角) デイリーステージなど(現在未使用)
        // if(cols == 8 && rows == 8)
        // {
        //     _trimShift = new Vector2(324f, 87f);
        // }

        OnUpdateProgressBar?.Invoke(8, 100, "Image splitting...");
        // ピースセル生成のコア処理
        SplitImage();
        _cells = this.gameObject.GetComponentsInChildren<AnswerGridPos>().ToList();
        OnUpdateProgressBar?.Invoke(28, 100, "setup gridPieceListController...");

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
    private void Split(bool isStatic, bool shouldClearCells = true)
    {
        // isReSetPiecesOnlyがtrueの場合、CreatePiece()で既に_cellsが設定されているため、再収集をスキップ
        // それ以外の場合は通常通りthis.gameObject配下から収集
        if(_cells == null || _cells.Count == 0)
        {
            _cells = this.gameObject.GetComponentsInChildren<AnswerGridPos>().ToList();
            Debug.Log($"[Split] {_cells.Count}個のセルを収集しました");
        }
        else
        {
            Debug.Log($"[Split] 既存の{_cells.Count}個のセルを使用します（再収集スキップ）");
        }
        
        // _pieceNum = -1;
        if(isStatic)
        {
            // ピースセルをいい感じにピースリストに配置
            // shouldClearCells: 新規作成時はtrue（セル削除）、再利用時はfalse（セル保持）
            CellSplitter.CellSplit( cols, rows, ref _pieceNum, _cells, _gridPieceListController, GetShapeType(), PieceCreateSeed, avoidPatternSeeds, shouldClearCells );
            PieceCreateSeed = CellSplitter.PatternSeed;
        }
        else
        {
            // ピースセルをいい感じにピースリストに配置
            var cellSplitter2 = new CellSplitter2(GetShapeType());
            cellSplitter2.CellSplit( cols, rows, ref _pieceNum, _cells, _gridPieceListController, PieceCreateSeed, avoidPatternSeeds );
            PieceCreateSeed = cellSplitter2.PatternSeed;
        }
    }
    
    private void AfterSplit(GameObject stageRoot)
    {
        // CreatePieceで保存したprefabPathを使用（アンパック前のパス）
        string prefabPath = _originalPrefabPath;
        
        // _originalPrefabPathが設定されていない場合のみ、従来の方法で取得を試みる
        if (string.IsNullOrEmpty(prefabPath))
        {
            bool isPrefabInstance = PrefabUtility.IsPartOfPrefabInstance(stageRoot);
            
            if (isPrefabInstance)
            {
                // まだアンパックされていない場合（CreatePieceCoroutineから呼ばれた場合など）
                var prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(stageRoot);
                prefabPath = AssetDatabase.GetAssetPath(prefabAsset);
                PrefabUtility.UnpackPrefabInstance(stageRoot, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                Debug.Log($"[AfterSplit] Prefabインスタンスをアンパックしました: {prefabPath}");
            }
            else
            {
                Debug.LogWarning("[AfterSplit] Prefabパスを取得できませんでした。プレハブ再接続をスキップします。");
            }
        }
        else
        {
            Debug.Log($"[AfterSplit] 保存済みのPrefabパスを使用します: {prefabPath}");
        }
        
        // シーン上のインスタンスのピース設定を更新（セルの親子関係変更）
        _gridPieceListController.SetUpChildrenPieceDragController();
        
        // ★ RectTransformの座標計算を強制的に完了させる（プレハブ保存前に座標を確定）
        Canvas.ForceUpdateCanvases();
        UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
        
        // プレハブとして保存する必要がある場合
        if (!string.IsNullOrEmpty(prefabPath))
        {
            // 一時的なコピーを作成して画像をクリアして保存
            GameObject tempCopy = GameObject.Instantiate(stageRoot);
            tempCopy.name = stageRoot.name;
            
            // コピーの画像をクリア（AddressableImageLoaderがついているもののみ）
            SplitImageHelper.ClearAddressableImageSprites(tempCopy);
            
            // Prefabとして保存
            PrefabUtility.SaveAsPrefabAsset(tempCopy, prefabPath);
            Debug.Log($"Prefabに保存しました: {prefabPath}");
            
            // シーン上のオブジェクトをPrefabインスタンスとして再接続
            GameObject newInstance = PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath)) as GameObject;
            newInstance.transform.SetParent(stageRoot.transform.parent);
            newInstance.transform.SetSiblingIndex(stageRoot.transform.GetSiblingIndex());
            newInstance.transform.localPosition = stageRoot.transform.localPosition;
            newInstance.transform.localRotation = stageRoot.transform.localRotation;
            newInstance.transform.localScale = stageRoot.transform.localScale;
            
            // 古いオブジェクトを削除
            DestroyImmediate(stageRoot);
            
            // 新しいインスタンスの画像を再ロード
            ReloadAddressableImageSprites(newInstance);
            
            // 新しいインスタンスから_gridPieceListControllerと_cellsを再取得
            var newSplitter = newInstance.GetComponentsInChildren<AbstractGridImageSplitter>(true)
                .FirstOrDefault(s => s.uniqueId == this.uniqueId);
            if (newSplitter != null)
            {
                newSplitter._gridPieceListController = newSplitter.GetGridPieceListController();
                newSplitter.RecollectCellsFromPieces();
            }
            
            // 一時コピーを削除
            DestroyImmediate(tempCopy);
        }
        else
        {
            // Prefabとして保存する必要がない場合（通常のGameObject）
            Debug.Log("通常のGameObjectのため、プレハブ保存をスキップします");
        }
    }
    
    // シーン上のインスタンスのAddressableImageLoaderの画像を再ロード
    private void ReloadAddressableImageSprites(GameObject root)
    {
        var loaders = root.GetComponentsInChildren<AddressableImageLoader>(true);
        foreach (var loader in loaders)
        {
            if (!string.IsNullOrEmpty(loader.addressName))
            {
                loader.LoadExternal();
            }
        }
        Debug.Log($"[ReloadAddressableImageSprites] {loaders.Length}個の画像を再ロードしました");
    }

    // GridPieceListController配下のPieceDragControllerから全セルを再収集
    private void RecollectCellsFromPieces()
    {
        _cells = new List<AnswerGridPos>();
        
        if (_gridPieceListController != null)
        {
            var pieceDragControllers = _gridPieceListController.GetComponentsInChildren<PieceDragController>(true);
            
            foreach (var piece in pieceDragControllers)
            {
                var cellsInPiece = piece.GetComponentsInChildren<AnswerGridPos>(true);
                _cells.AddRange(cellsInPiece);
            }
            
            Debug.Log($"[RecollectCellsFromPieces] {_cells.Count} 個のセルを再収集しました");
        }
        else
        {
            Debug.LogWarning("[RecollectCellsFromPieces] GridPieceListControllerが見つかりません");
        }
    }

    public void Deletepiece()
    {
        DeleteChildren();
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

    // 画像をAddressable化してグループに登録する
    // isUseImageLoader: AddressableImageLoaderを使うかどうか　(画像をAddressable化してImageLoaderで読み込む場合はtrue、ステージ自体をAddressable化する場合はfalse)
    public void Addressable(bool isUseImageLoader = false)
    {
    #if UNITY_EDITOR
        OnUpdateProgressBar?.Invoke(0, 100, "Addressables Settings の取得中...");

        List<AddressableImageLoader> loadedImageLoaders = new List<AddressableImageLoader>();

        // 1. Addressables Settings の取得
        var settings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError("Addressable Asset Settingsが見つかりません。Addressablesを有効にしてください。");
            return;
        }

        OnUpdateProgressBar?.Invoke(1, 100, "自身のプレハブインスタンスの取得中...");
        // 2. シーン上のインスタンスからプレハブアセットを取得
        // 自身がプレハブインスタンスでない場合は処理を中止
        GameObject instanceRoot = this.transform.parent.parent.gameObject;
        var prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(instanceRoot);

        if (prefabAsset == null)
        {
            Debug.LogError("このゲームオブジェクトはプレハブインスタンスではありません。プレハブとして保存されているステージでのみ実行してください。");
            return;
        }
        
        // プレハブ名を取得 (これがグループ名になる)
        string groupName = prefabAsset.name;
        OnUpdateProgressBar?.Invoke(2, 100, $"Addressable Group '{groupName}' の取得または新規作成中...");
        // 3. グループの取得または新規作成
        AddressableAssetGroup group = settings.FindGroup(groupName);
        if (group == null)
        {
            // プレハブ名と同じ名前のAssetGroupsを作成
            Debug.Log($"Addressable Group '{groupName}' を新規作成しました。");
            var groupTemplate = settings.GetGroupTemplateObject(0) as AddressableAssetGroupTemplate;
            group = settings.CreateGroup(groupName, false, false, true, null, groupTemplate.GetTypes());
            groupTemplate.ApplyToAddressableAssetGroup(group);
        }
        else
        {
            // 既存グループの場合、一旦全てのエントリを削除し、再登録する
            Debug.Log($"既存の Addressable Group '{groupName}' をクリアしました。");
        }
        

        // プレハブ内の全ImageコンポーネントからSpriteを収集
        OnUpdateProgressBar?.Invoke(4, 100, "Imageコンポーネント取得中...");
        Image[] images = instanceRoot.GetComponentsInChildren<Image>(true);

        Dictionary<Sprite, List<Image>> imageDic = new Dictionary<Sprite, List<Image>>();
        int index = 0;
        foreach (var image in images)
        {
            if(_shadowSprite == image.sprite)
                continue;
            if(image.name == "shadow")
                continue;
            string assetPath = AssetDatabase.GetAssetPath(image.sprite);
            if (string.IsNullOrEmpty(assetPath)) continue;
            string fileName = Path.GetFileName(assetPath);
            if(fileName == "Button_Square04.png")
                continue;
            if(fileName == "Button_Square04")
                continue;

            if(!imageDic.ContainsKey(image.sprite))
                imageDic.Add(image.sprite, new List<Image>());

            imageDic[image.sprite].Add(image);

            // n% -> 50%進捗更新
            OnUpdateProgressBar?.Invoke(4 + index / 2, images.Length + 4, $"ImageからAddressable化対象のSpriteを収集中...{index + 1}/{images.Length}");
            index++;
        }

        float totalSprites = (float)imageDic.Keys.Count;
        index = 0;
        // 各SpriteアセットをAddressable化してグループに登録
        // 50% -> 95%進捗更新
        foreach (var sprite in imageDic.Keys)
        {
            string assetPath = AssetDatabase.GetAssetPath(sprite);
            if (string.IsNullOrEmpty(assetPath)) continue;
            // 画像の保存場所を変更
            string newDirectory = "Assets/Prefabs/Addressable/" + groupName;
            // 4. 新しいアセットの完全なパスを構築
            // 元のアセットのファイル名と拡張子を保持します。
            string fileName = Path.GetFileName(assetPath);
            string newAssetPath = newDirectory + "/" + fileName;

            if(isUseImageLoader)
            {
                float subIndex = 0f;
                foreach (var image in imageDic[sprite])
                {
                    // 画像をAddressable化した各オブジェクトに代わりにAddressableImageLoaderをつける(パスを記憶してawake時にロードする機能)
                    AddressableImageLoader addressableImageLoader = image.gameObject.GetComponent<AddressableImageLoader>();
                    bool loaderExisted = (addressableImageLoader != null);
                    
                    if(addressableImageLoader == null)
                    {
                        addressableImageLoader = image.gameObject.AddComponent<AddressableImageLoader>();
                    }
                    loadedImageLoaders.Add(addressableImageLoader);
                    addressableImageLoader.addressName = newAssetPath;
                    
                    // 既にLoaderがついていた場合（セル生成時に追加済み）は画像をクリアしない
                    if (!loaderExisted)
                    {
                        image.sprite = null;
                    }
                    
                    subIndex += 1f;
                    OnUpdateProgressBar?.Invoke((int)(50f + ((float)index / 2f + subIndex / (float)imageDic[sprite].Count) * 45f / totalSprites), 100, $"{fileName}のAddressable化中：Imageの更新中 ({subIndex}/{imageDic[sprite].Count}){index + 1}/{totalSprites} ");
                }
            }

            OnUpdateProgressBar?.Invoke((int)(50f + ((float)index + 0.5f) * 45f / totalSprites), 100, $"{fileName}のAddressable化中：ファイル移動中...{index + 1}/{totalSprites}");
            // ここでセーブ
            // 3. 新しいディレクトリが存在しない場合、作成
            if (!AssetDatabase.IsValidFolder(newDirectory))
            {
                Directory.CreateDirectory(newDirectory);
                // AssetDatabaseにフォルダ作成を通知
            }
            AssetDatabase.Refresh(); 

            // newAssetPathに既に同じ名前のアセットが存在する場合、上書きする
            if (File.Exists(newAssetPath) && assetPath != newAssetPath)
            {
                Debug.Log($"同名アセットが存在するため上書きします: {newAssetPath}");
                AssetDatabase.DeleteAsset(newAssetPath);
            }

            // 5. AssetDatabase.MoveAssetを使用してアセットを移動
            string result = AssetDatabase.MoveAsset(assetPath, newAssetPath);

            // AssetDatabase.Refresh(); 

            var guid = AssetDatabase.AssetPathToGUID(newAssetPath);
            AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group);

            if(entry != null)
            {
                // Debug.Log($"✅ SpriteのAddressable化成功: {newAssetPath}, {group}");
            }
            else
            {
                // Debug.Log($"❌ SpriteのAddressable化失敗: {newAssetPath}, {group}");
            }

            if (string.IsNullOrEmpty(result))
            {
                // Debug.Log($"✅ Spriteアセットを移動しました: {assetPath} -> {newAssetPath}");

                // 移動後、AssetDatabaseに変更を保存
                // AssetDatabase.SaveAssets(); 
                // EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(newAssetPath)); // 移動したアセットを強調表示（オプション）
            }
            else
            {
                // Debug.LogError($"❌ Spriteアセットの移動に失敗しました: {result}");
                // Debug.LogError($"パス: {assetPath} -> {newAssetPath}");
            }
            AssetDatabase.SaveAssets(); 
            index++;
        }

        // Debug.Log($"Addressable 1");
        OnUpdateProgressBar?.Invoke(95, 100, $"設定の保存と更新中...");
        // 6. 設定の保存と更新
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        SaveAsPrefab.Save(this.transform.parent.parent.gameObject, PrefabSavePath);

        Debug.Log($"🎉 ステージ'{groupName}'と関連アセットのAddressable設定が完了しました！\n登録されたアセット数: {group.entries.Count}件");

        // ImageのSpriteをnullにしてプレハブを保存した後、管理しやすいように改めて画像をロードする
        if(true)
        {
            var totalLoaders = loadedImageLoaders.Count;
            int currentLoaderIndex = 0;
            foreach (var loader in loadedImageLoaders)
            {
                OnUpdateProgressBar?.Invoke(96, 100, $"AddressableImageLoaderで画像を再読み込み中... ({currentLoaderIndex + 1}/{totalLoaders})");
                loader.LoadExternal();
                currentLoaderIndex++;
            }
        }

        OnUpdateProgressBar?.Invoke(100, 100, $"Addressable設定完了！");
    #else
        // エディタ外ではAddressable設定は実行できない
        Debug.LogWarning("Addressable設定はUnity Editor上でのみ実行可能です。");
    #endif
    }

        // 画像でなく該当プレハブをAddressable化する
    public void AddressableStage()
    {
#if UNITY_EDITOR
        // 1. Addressables Settings の取得
        var settings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError("Addressable Asset Settingsが見つかりません。Addressablesを有効にしてください。");
            return;
        }

        // 2. シーン上のインスタンスからプレハブアセットを取得
        // 自身がプレハブインスタンスでない場合は処理を中止
        GameObject instanceRoot = this.transform.parent.parent.gameObject;
        var prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(instanceRoot);

        if (prefabAsset == null)
        {
            Debug.LogError("このゲームオブジェクトはプレハブインスタンスではありません。プレハブとして保存されているステージでのみ実行してください。");
            return;
        }
        
        // プレハブ名を取得 (これがグループ名になる)
        string groupName = prefabAsset.name;

        // 3. グループの取得または新規作成
        AddressableAssetGroup group = settings.FindGroup(groupName);
        if (group == null)
        {
            // プレハブ名と同じ名前のAssetGroupsを作成
            Debug.Log($"Addressable Group '{groupName}' を新規作成しました。");
            var groupTemplate = settings.GetGroupTemplateObject(0) as AddressableAssetGroupTemplate;
            group = settings.CreateGroup(groupName, false, false, true, null, groupTemplate.GetTypes());
            groupTemplate.ApplyToAddressableAssetGroup(group);
        }

        // 4. Addressable化するアセットを収集
        Debug.Log($"Addressable Stage 1");
        // A. プレハブアセット自体
        string prefabPath = AssetDatabase.GetAssetPath(prefabAsset);
        
        var guid = AssetDatabase.AssetPathToGUID(prefabPath);
        AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group);

        if(entry != null)
        {
            Debug.Log($"✅ ステージのAddressable化成功: {prefabPath}, {group}");
        }
        else
        {
            Debug.Log($"❌ ステージのAddressable化失敗: {prefabPath}, {group}");
        }

        // 6
        // 設定の保存と更新
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        SaveAsPrefab.Save(this.transform.parent.parent.gameObject, PrefabSavePath);
#endif
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

        if (GUILayout.Button("画像分割（セル生成）"))
        {
            // script.Deletepiece();
            script.SplitImageProcess();
            script.isSkip = false;
        }
        if (GUILayout.Button("セル削除"))
        {
            script.Deletepiece();
            script.isSkip = false;
        }
        if (GUILayout.Button("セル生成＋ピース配置"))
        {
            script.OnUpdateProgressBar = null; 
            script.OnUpdateProgressBar += (current, total, message) =>
            {
                bool cancelled = EditorUtility.DisplayCancelableProgressBar("ステージ生成中", message, (float)current / total);
                if (cancelled)
                {
                    EditorUtility.ClearProgressBar();
                    throw new OperationCanceledException("ユーザーによってステージ生成がキャンセルされました。");
                }
                if (current >= total)
                {
                    EditorUtility.ClearProgressBar();
                }
            };
            
            try
            {
                script.CreatePiece();
            }
            catch (OperationCanceledException)
            {
                Debug.LogWarning("ステージ生成がキャンセルされました。");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                script.OnUpdateProgressBar = null;
            }
        }
        if (GUILayout.Button("画像のみAddressable化"))
        {
            script.OnUpdateProgressBar = null;
            script.OnUpdateProgressBar += (current, total, message) =>
            {
                bool cancelled = EditorUtility.DisplayCancelableProgressBar("Addressable化 実行中", message, (float)current / total);
                if (cancelled)
                {
                    EditorUtility.ClearProgressBar();
                    throw new OperationCanceledException("ユーザーによってAddressable化がキャンセルされました。");
                }
                if (current >= total)
                {
                    EditorUtility.ClearProgressBar();
                }
            };
            
            try
            {
                script.Addressable(true);
            }
            catch (OperationCanceledException)
            {
                Debug.LogWarning("Addressable化がキャンセルされました。");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                script.OnUpdateProgressBar = null;
            }
        }
        // if (GUILayout.Button("ステージのAddressable化"))
        // {
        //     script.AddressableStage();
        //     script.Addressable(false);  // AddressableStageとAddressableの共通処理をまとめる場合はこの行を削除してください
        // }
    }
}
#endif