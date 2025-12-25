using UnityEngine;
using UnityEngine.UI;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
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

    [Header("TriangleParam")]
    public Vector2 _trimShift = Vector2.zero;
    public int uniqueId = 0;
    public int index = 0;

    public string PrefabSavePath = "Assets/Prefabs/Stages"; // プレハブ保存先ディレクトリ

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

        _trimShift = new Vector2(0f, 0f);
        // 各ステージごとの微調整
        // できれば調整が不要になるようCreatePieceを改善したい
        // 現仕様では1~9ステージと、12~27ステージ(3ステージごと)を確認して調整すると全パターン対応可能
        // iD:3 = 3x4　1,4,7ステージなど
        if(cols == 3 && rows == 4)
        {
            if( GetShapeType() == ShapeType.Square)
            {
                fixTargetPercentCellSize = 0.995f;
                _trimShift = new Vector2(0f, 1f);
            }
            if( GetShapeType() == ShapeType.Triangle)
            {
                _trimShift = new Vector2(324f, 87f);
            }
            if( GetShapeType() == ShapeType.Hex)
            {
                targetPercent = 110;
                _trimShift = new Vector2(0f, -1.45f);
            }
        }
        // iD:4 = 4x5 2,5,8ステージなど
        if(cols == 4 && rows == 5)
        {
            if( GetShapeType() == ShapeType.Square)
            {
                fixTargetPercentCellSize = 0.995f;
                _trimShift = new Vector2(0f, 1f);
            }
            if( GetShapeType() == ShapeType.Triangle)
            {
                _trimShift = new Vector2(324f, 87f);
                fixTargetPercentCellSize = 0.997f;
            }
            if( GetShapeType() == ShapeType.Hex)
            {
                targetPercent = 115;
                _trimShift = new Vector2(0f, -1.2f);
            }
        }
        // iD:5 = 5x7(四角六角)　3,9ステージなど
        if(cols == 5 && rows == 7)
        {
            if( GetShapeType() == ShapeType.Hex)
            {
                targetPercent = 115;
                _trimShift = new Vector2(0f, -0.88f);
            }
            if( GetShapeType() == ShapeType.Square)
            {
                fixTargetPercentCellSize = 0.995f;
                _trimShift = new Vector2(0f, 1f);
            }
        }
        // iD:5 = 6x6(三角) 6sテージなど
        if(cols == 6 && rows == 6)
        {
            if( GetShapeType() == ShapeType.Triangle)
                _trimShift = new Vector2(267f, 87f);
        }
        // iD:6 = 6x8(四角六角) 12,18ステージなど
        if(cols == 6 && rows == 8)
        {
            if( GetShapeType() == ShapeType.Hex)
            {
                _trimShift = new Vector2(0f, -0.78f);
                fixTargetPercentCellSize = 0.995f;
            }
            // 四角は特に調整不要
            if( GetShapeType() == ShapeType.Square)
            {
                _trimShift = new Vector2(0f, 0f);
                targetPercent = 100;
                fixTargetPercentCellSize = 1f;
            }
        }
        // iD:6 = 7x7(三角) 15ステージなど
        if(cols == 7 && rows == 7)
        {
            if( GetShapeType() == ShapeType.Triangle)
                _trimShift = new Vector2(278f, 87f);
        }
        // iD:7 = 7x8(四角六角) 21,27ステージなど
        if(cols == 7 && rows == 8)
        {
            if( GetShapeType() == ShapeType.Square)
            {
                targetPercent = 88;
                fixTargetPercentCellSize = 0.995f;
                _trimShift = new Vector2(0f, 0f);
            }
            if( GetShapeType() == ShapeType.Hex)
            {
                targetPercent = 105;
                _trimShift = new Vector2(0f, -0.69f);
            }
        }
        // iD:7 = 8x7(三角) 24ステージなど
        if(cols == 8 && rows == 7)
        {
            if( GetShapeType() == ShapeType.Triangle)
            {
                _trimShift = new Vector2(312f, 175f);
                targetPercent = 131;
                fixTargetPercentCellSize = 0.9925f;
            }
        }
        // iD:8 = 7x9(四角六角) デイリーステージなど
        if(cols == 7 && rows == 9)
        {
            _trimShift = new Vector2(0f, 0f);
            if( GetShapeType() == ShapeType.Hex)
            {
                _trimShift = new Vector2(0f, -0.7f);
            }
        }
        // iD:8 = 8x8(三角) デイリーステージなど(現在未使用)
        if(cols == 8 && rows == 8)
        {
            _trimShift = new Vector2(324f, 87f);
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

    // 画像をAddressable化してグループに登録する
    // isUseImageLoader: AddressableImageLoaderを使うかどうか　(画像をAddressable化してImageLoaderで読み込む場合はtrue、ステージ自体をAddressable化する場合はfalse)
    public void Addressable(bool isUseImageLoader = false)
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
        else
        {
            // 既存グループの場合、一旦全てのエントリを削除し、再登録する
            Debug.Log($"既存の Addressable Group '{groupName}' をクリアしました。");
        }
        
        // 4. Addressable化するアセットを収集
        Debug.Log($"Addressable 1");
        // A. プレハブアセット自体
        string prefabPath = AssetDatabase.GetAssetPath(prefabAsset);
        
        // B. プレハブ内で使われているSpriteアセットを収集
        // List<Object> assetsToAddressable = new List<Object>();

        // プレハブ内の全ImageコンポーネントからSpriteを収集
        Image[] images = instanceRoot.GetComponentsInChildren<Image>(true);

        Dictionary<Sprite, List<Image>> imageDic = new Dictionary<Sprite, List<Image>>();
        foreach (var image in images)
        {
            if(_shadowSprite == image.sprite)
                continue;
            if(image.name == "shadow")
                continue;

            if(!imageDic.ContainsKey(image.sprite))
                imageDic.Add(image.sprite, new List<Image>());

            imageDic[image.sprite].Add(image);
        }

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
                foreach (var image in imageDic[sprite])
                {

                    // 画像をAddressable化した各オブジェクトに代わりにAddressableImageLoaderをつける(パスを記憶してawake時にロードする機能)
                    AddressableImageLoader addressableImageLoader = image.gameObject.GetComponent<AddressableImageLoader>();
                    if(addressableImageLoader == null)
                    {
                        addressableImageLoader = image.gameObject.AddComponent<AddressableImageLoader>();
                    }
                    addressableImageLoader.addressName = newAssetPath;
                    image.sprite = null;
                }
            }
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
        }

        Debug.Log($"Addressable 1");

        // 6. 設定の保存と更新
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        SaveAsPrefab.Save(this.transform.parent.parent.gameObject, PrefabSavePath);

        Debug.Log($"🎉 ステージ'{groupName}'と関連アセットのAddressable設定が完了しました！\n登録されたアセット数: {group.entries.Count}件");

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
        if (GUILayout.Button("画像のみAddressable化"))
        {
            script.Addressable(true);
        }
        if (GUILayout.Button("ステージのAddressable化"))
        {
            script.AddressableStage();
            script.Addressable(false);  // AddressableStageとAddressableの共通処理をまとめる場合はこの行を削除してください
        }
    }
}
#endif