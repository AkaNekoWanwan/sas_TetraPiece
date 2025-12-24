using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement; // SceneManagerを使用するために必要
using System; // ShapeTypeを使用するために必要
using System.Threading.Tasks;
using System.Threading;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

public class StageCreator : MonoBehaviour
{
#if UNITY_EDITOR
    // public bool isOverrideSprite = true;
    public List<Sprite> _setSplites = new List<Sprite>();
    public List<Sprite> _setDailySplites = new List<Sprite>();
    public HashSet<Sprite> _splitesHash = new HashSet<Sprite>();
    public HashSet<string> _seeds = default;
    GameObject beforeStage = null;
    public bool IsNewLogic = false;
    public bool IsForce = false;
    public bool IsPreSetUp = false;
    public bool IsWaitBeforeSplit = true;
    public bool IsWaitAfterSplit = true;
    public bool IsDailyStage = true;
    public bool IsLog = true;
    public List<StageData> _stageData = default;

    public List<StageInfo> _targetStages = default;
    public List<AbstractGridImageSplitter> _targetSplitters = default;

    public List<AbstractGridImageSplitter> AllSplitters;
    public List<AbstractGridImageSplitter> _createPieceplitterList = new List<AbstractGridImageSplitter>();

    public List<int> pieceListCounts = default;
    public Sprite _shadowSpriteSquare   = default;
    public Sprite _shadowSpriteHex      = default;
    public Sprite _shadowSpriteTriangle = default;
    
    // --- 【追加 1】プレハブ保存のフラグとパス設定 ---
    [Header("Prefab Saving Settings")]
    public bool IsSavePrefabAfterSplit = false; // プレハブ保存のON/OFFフラグ
    [Tooltip("プレハブを保存するAssets/以下の相対パス。例: Prefabs/Stages")]
    public string PrefabSavePath = "Assets/Prefabs/Stages"; // プレハブ保存先ディレクトリ
    // ------------------------------------------------
    private void OnValidate() {
        return;
        if(!IsLog)
            return;
        pieceListCounts = new List<int>();
        Debug.Log("わわわ");
        int i = 0;
        foreach(AbstractGridImageSplitter spritter in AllSplitters)
        {
            pieceListCounts.Add(spritter._pieceNum);
            Debug.Log($"{i}:{spritter._pieceNum}");
            i++;
        }
    }

    private Coroutine _creationCoroutine = null;

    public void StopCreateStages()
    {
        if (_creationCoroutine != null)
        {
            StopCoroutine(_creationCoroutine);
        }
    }
    
    public void CreateStages()
    {
        StopCreateStages();
        if(!IsNewLogic)
            _creationCoroutine = StartCoroutine(CreateStagesCoroutine());
        else
        {
            // _creationCoroutine = StartCoroutine(CreateStagesCoroutine2());
            CreateStagesAsync();
        }
    }

    // ステージパラメータ設定
    public void SetStagePatamList(List<StageData> paramList)
    {
        _stageData = paramList;
    }

    public void PreSetUp()
    {
        UpdateSetSprites();

        // 2. 初期化処理の抽象化
        // 全ステージ取得 (AbstractGridImageSplitterを継承した全てを取得)
        List<AbstractGridImageSplitter> allSplitters = GetAllSplitters();
        // _seeds = new HashSet<string>();
        AllSplitters = new List<AbstractGridImageSplitter>();

        // スプリッターを種類ごとに分類し、参照用ステージを弾き、シード値を取得する処理を統一
        // フィルタリング後のリストを、種類ごとに保持
        List<AbstractGridImageSplitter> squareSplitters = new List<AbstractGridImageSplitter>();
        List<AbstractGridImageSplitter> triSplitters = new List<AbstractGridImageSplitter>();
        List<AbstractGridImageSplitter> hexSplitters = new List<AbstractGridImageSplitter>();
        _createPieceplitterList = new List<AbstractGridImageSplitter>();
        
        int index = 0;
        foreach (var splitter in allSplitters)
        {
            // 参考用のステージは弾く
            Debug.Log($"StageCreator:splitter:{splitter.GetType().Name}:{splitter.transform.parent.parent.name}, {splitter.isPrefs}, {splitter.PieceCreateSeed}");
            GameObject stageObject = splitter.transform.parent.parent.gameObject;
            stageObject.SetActive(false);
            if (splitter.isPrefs || splitter.isCreative)
            {
                stageObject.name = splitter.isPrefs ? "Prefs" : "Creative";
                continue; // 参照用ステージはリストに追加しない
            }
            stageObject.name = $"Stage{index + 1:D3} SetWait";
            
            index++;

            // シード値取得
            // string pieceCreateSeed = splitter.PieceCreateSeed;
            // if (!string.IsNullOrEmpty(splitter.PieceCreateSeed))
            // {
            //     // シード値が既存のものなら作り直す
            //     if(_seeds.Contains(pieceCreateSeed))
            //         splitter.PieceCreateSeed = "";
            //     else
            //         _seeds.Add(splitter.PieceCreateSeed);
            // }

            // 種類ごとに分類 (ここではGetType()やis演算子で判断)
            if (splitter is GridImageSplitter)
                squareSplitters.Add(splitter);
            else if (splitter is GridImageSplitterTriangle)
                triSplitters.Add(splitter);
            else if (splitter is GridImageSplitterHex)
                hexSplitters.Add(splitter);
            else
                Debug.LogError($"未定義のSplitter型が検出されました: {splitter.GetType().Name}");
        }

        Debug.Log($"StageCreator:全ステージセットアップ開始！:Square:{squareSplitters.Count}, Triangle:{triSplitters.Count}, Hex:{hexSplitters.Count}");

        // 3. ステージ設定処理の統一と並び替えロジック
        int sumCount = squareSplitters.Count + hexSplitters.Count + triSplitters.Count;
        int indexSquare = 0;
        int indexTri = 0;
        int indexHex = 0;

        for (int i = 0; i < sumCount; i++)
        {
            bool isHard = (i + 1) % 3 == 0;
            if(IsDailyStage)
                isHard = false;
            ShapeType shapeType = ShapeType.Square;
            int typeInt = (i / 3) % 3; // ３ステージ毎に切り替わる
            // IsDailyStageならShapeType.Square(0) と ShapeType.Hex(2) のみで
            if(IsDailyStage)
            {
                typeInt = i % 2;
                if(typeInt == 1)
                    typeInt = 2;
            }

            // 交互にステージタイプを決定
            if (typeInt == 0)
                shapeType = ShapeType.Square;
            else if (typeInt == 1)
                shapeType = ShapeType.Triangle;
            else if (typeInt == 2)
                shapeType = ShapeType.Hex;

            StageData stageData = GetStageData(i);
            if(stageData != null && !IsDailyStage)
            {
                Debug.Log($"StageCreator:StageCreator:shapeType:{i}, {stageData.shapeType}");
                shapeType = stageData.shapeType;
            }

            int cols = 3;
            int rows = 3;
            int pieceNum = 4;
            // ステージの進捗に応じて難易度調整
            AbstractGridImageSplitter currentSplitter = null;

            // 3. SetUpStageを直接ループ内に組み込み、単一リストのインデックス操作を統一
            switch (shapeType)
            {
                case ShapeType.Square:
                    if (indexSquare < squareSplitters.Count)
                    {
                        currentSplitter = squareSplitters[indexSquare];
                        currentSplitter.targetPercent = 100;
                        indexSquare++;
                        currentSplitter._shadowSprite = _shadowSpriteSquare;
                    }
                    break;
                case ShapeType.Triangle:
                    if (indexTri < triSplitters.Count)
                    {
                        currentSplitter = triSplitters[indexTri];
                        currentSplitter.targetPercent = 120;
                        indexTri++;
                        currentSplitter._shadowSprite = _shadowSpriteTriangle;
                    }
                    break;
                case ShapeType.Hex:
                    if (indexHex < hexSplitters.Count)
                    {
                        currentSplitter = hexSplitters[indexHex];
                        currentSplitter.targetPercent = 120;
                        indexHex++;
                        currentSplitter._shadowSprite = _shadowSpriteHex;
                    }
                    break;
            }

            // 該当するスプリッターが枯渇していたらスキップ（現状のコードのロジックを維持）
            if (currentSplitter == null)
            {
                continue;
            }

            GetStageParam(i, isHard, out cols, out rows, out pieceNum, currentSplitter, shapeType);
            SetImage(currentSplitter, i); 

            GameObject stageObject = currentSplitter.transform.parent.parent.gameObject;
            stageObject.GetComponent<StageInfo>().isHard = isHard;

            // 抽出したスプリッターを、ステージ順に並べた AllSplitters に格納
            AllSplitters.Add(currentSplitter);

            // SetUpStage内のロジックをここに移動し、抽象化された currentSplitter に対して処理
            CommonSplitterProcces(currentSplitter.gameObject, i, ref currentSplitter.isSkip);
            
            if (string.IsNullOrEmpty(currentSplitter.PieceCreateSeed))
            {
                Debug.Log($"StageCreator:更新対象！{stageObject.name}, シード値未設定");
                currentSplitter.isSkip = false;
            }
            if (currentSplitter.cols != cols)
            {
                Debug.Log($"StageCreator:更新対象！{stageObject.name}, cols {currentSplitter.cols} -> {cols}");
                currentSplitter.isSkip = false;
            }
            if (currentSplitter.rows != rows)
            {
                Debug.Log($"StageCreator:更新対象！{stageObject.name}, rows {currentSplitter.rows} -> {rows}");
                currentSplitter.isSkip = false;
            }
            if (currentSplitter._pieceNum != pieceNum)
            {
                Debug.Log($"StageCreator:更新対象！{stageObject.name}, pieceNum {currentSplitter._pieceNum} -> {pieceNum}");
                currentSplitter.isSkip = false;
            }
            if(IsForce)
                currentSplitter.isSkip = false;
            // if (!currentSplitter.avoidPatternSeeds.SequenceEqual(_seeds))
            // {
            //     currentSplitter.isSkip = false;
            // }

            if (!currentSplitter.isSkip)
            {
                currentSplitter.cols = cols;
                currentSplitter.rows = rows;
                currentSplitter._pieceNum = pieceNum;
                // currentSplitter.CreatePiece();
                _createPieceplitterList.Add(currentSplitter);
            }
        }
    }

    private void SetTargetSplitters()
    {
        _targetSplitters = new List<AbstractGridImageSplitter>();
        foreach(var stageInfo in _targetStages)
        {
            AbstractGridImageSplitter splitter = stageInfo.GetComponentInChildren<AbstractGridImageSplitter>();
            if(splitter != null)
            {
                splitter.PrefabSavePath = PrefabSavePath;
                _targetSplitters.Add(splitter);
            }
        }
    }
    private List<AbstractGridImageSplitter> GetAllSplitters()
    {
        List<AbstractGridImageSplitter> allSplitters;
        SetTargetSplitters();
        if(_targetSplitters != null && 0 < _targetSplitters.Count)
        {
            allSplitters = _targetSplitters;
            Debug.Log($"StageCreator:ターゲット指定:{_targetSplitters.Count}個");
        }
        else
        {
            allSplitters = FindAllInScene<AbstractGridImageSplitter>();
            Debug.Log($"StageCreator:ターゲット未指定:シーン内全取得:{allSplitters.Count}個");
        }
        return allSplitters;
    }
    
    // ステージ作成コルーチン:各ステージの生成処理を一斉に開始する
    public IEnumerator CreateStagesCoroutine2()
    {
        Debug.Log($"StageCreator:ステージ生成コルーチン:実行！");
        yield return null;

        if(IsPreSetUp)
        {
            Debug.Log($"StageCreator:ステージ生成コルーチン:PreSetUp");
            PreSetUp();
            yield return null;
        }
        List<Coroutine> runningTasks = new List<Coroutine>();
        List<AbstractGridImageSplitter> activeSplitters = new List<AbstractGridImageSplitter>(); // 実行中のスプリッターを追跡

        for(int i = 0; i < _createPieceplitterList.Count; i++)
        {
            AbstractGridImageSplitter splitter = _createPieceplitterList[i];
            if(splitter.isSkip)
            {
                Debug.Log($"StageCreator:ステージ生成コルーチン:{i}をスキップ");
                continue;
            }
            GameObject stageObject = splitter.transform.parent.parent.gameObject;
            Debug.Log($"StageCreator:ステージ生成コルーチン:ステージ生成：{i}, {stageObject.name}");
            StartCoroutine(splitter.CreatePieceCoroutine());
            activeSplitters.Add(splitter); // 実行中のスプリッターとしてリストに追加

            splitter.isSkip = true;
            beforeStage = stageObject;
        }
        yield break;
    }

    // 使用している画像の名前を更新する
    // ファイル名の先頭に00X_を付与する。すでに付与されている場合はそれを更新する
    // ( 例: 001_Hoge.png -> 001_Hoge.png, 001Hoge_1080x1350.png -> 001_Hoge_1080x1350.png, Hoge.png -> 001_Hoge.png, Hoge_1080x1350.png -> 001_Hoge_1080x1350.png )
    public void UpdateSetSpriteFileName()
    {
        UpdateSetSprites(); // スプライトリストを更新
        
        string prefix = IsDailyStage ? "Daily" : "";
        SpriteFileNameUtil.UpdateSpriteFileNames(_setSplites, prefix, startNumber: 1, enableLog: true);
    }

    // --- 【追加 3】プレハブ保存処理メソッド ---
    /// <summary>
    /// 指定されたゲームオブジェクトを、PrefabSavePathに、その名前でプレハブとして保存（または上書き）します。
    /// </summary>
    private void SaveAsPrefab(GameObject targetObject)
    {
        if (!IsSavePrefabAfterSplit) return;
        
        // プレハブの完全なパスを構築
        string path = Path.Combine(PrefabSavePath, targetObject.name + ".prefab");
        
        // パスを標準化し、Assets/で始まっていることを確認
        if (!path.StartsWith("Assets/"))
        {
            path = Path.Combine("Assets", PrefabSavePath, targetObject.name + ".prefab");
        }
        
        // ディレクトリが存在しない場合は作成
        string directory = Path.GetDirectoryName(path);
        if (!AssetDatabase.IsValidFolder(directory))
        {
            // ディレクトリを再帰的に作成（Assets/Prefabs/Stages のような構造に対応）
            string currentPath = "Assets";
            string[] subDirs = PrefabSavePath.Split(new char[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string subDir in subDirs)
            {
                string newPath = Path.Combine(currentPath, subDir);
                if (!AssetDatabase.IsValidFolder(newPath))
                {
                    AssetDatabase.CreateFolder(currentPath, subDir);
                }
                currentPath = newPath;
            }
            AssetDatabase.Refresh();
        }

        // プレハブを作成または上書き
        // targetObjectはシーン内のGameObject
        GameObject prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(targetObject, path, InteractionMode.UserAction);

        if (prefab != null)
        {
            Debug.Log($"StageCreator:【Prefab Saved】: {targetObject.name} を {path} に保存/上書きしました。", prefab);
        }
        else
        {
            Debug.LogError($"【Prefab Save Failed】: {targetObject.name} のプレハブ保存に失敗しました。", targetObject);
        }
    }
    // ----------------------------------------------------

    // 三角四角六角の共通処理
    private void CommonSplitterProcces(GameObject splitter, int i, ref bool isSkip)
    {
        GameObject stageObject = splitter.transform.parent.parent.gameObject;
        if(!IsDailyStage)
            stageObject.name = $"Stage{i + 1:D3}";
        else
            stageObject.name = $"DailyStage{i + 1:D3}";

        // stageObject.SetActive(true);
        if (0 < i)
        {
            int siblingIndex = beforeStage.transform.GetSiblingIndex();
            stageObject.transform.SetSiblingIndex(siblingIndex + 1);
            // beforeStage.SetActive(false);
        }

        beforeStage = stageObject;
    }

    private void SetImage(AbstractGridImageSplitter splitter, int i)
    {
        Image stageImage = splitter.GetComponent<Image>();
        Sprite sprite = stageImage.sprite;
        Sprite setSprite = _setSplites[i % _setSplites.Count];
        
        if (stageImage.sprite != setSprite)
        {
            GameObject stageObject = splitter.transform.parent.parent.gameObject;
            Debug.Log($"StageCreator:更新対象！{stageObject.name}, 画像差し替え:{stageImage.sprite}->{setSprite}");
            stageImage.sprite = setSprite;
            stageImage.SetNativeSize();
            // スキップフラグを下す
            splitter.isSkip = false;
        }
    }

    // private int GetStageIndex(int index)
    // {
    //     int ret = -1;
    //     return ret;
    // }
    private StageData GetStageData(int index)
    {
        int i = index;
        StageData ret = null;
        if(_stageData != null && 0 <= index)
        {
            if( index < _stageData.Count )
            {
                ret = _stageData[index];
            }
            else if( 9 <= _stageData.Count)
            {
                index = _stageData.Count / 9 * 9 + index % 9;
                if(_stageData.Count <= index)
                    index -= 9;
                ret = _stageData[index];
            }
        }
        Debug.Log($"StageCreator:StageCreator:GetStageData:{i} -> {index}: _stageData?{_stageData != null}");
        return ret;
    }
    
    private void GetStageParam(int i, bool isHard, out int cols, out int rows, out int pieceNum, AbstractGridImageSplitter splitter, ShapeType shapeType)
    {
        StageData stageData = GetStageData(i);
        if(IsDailyStage)
        {
            cols = 7; 
            rows = 9;
            pieceNum = 21;
            if(shapeType == ShapeType.Triangle)
            {
                cols = 8; 
                rows = 8;
                pieceNum = 24;
            }
        }
        else if( stageData != null )
        {
            cols = stageData.gridX; 
            rows = stageData.gridY;
            // pieceNum = stageData.pieceNum;
            pieceNum = -1;
            bool isSetToId = false;
            if(!string.IsNullOrEmpty(stageData.gridIds))
            {
                Vector2Int grid = GetGridToId(stageData.gridIds, i, shapeType);
                if(grid != Vector2Int.zero)
                {
                    cols = grid.x; 
                    rows = grid.y;
                    isSetToId = true;
                }
            }

            if(isSetToId)
                Debug.Log($"StageCreator:StageCreator:ステージ{i}をgridIdから設定:{stageData.gridIds} -> ({cols}, {rows})");
            else
                Debug.Log($"StageCreator:StageCreator:ステージ{i}をgridIdから設定できませんでした:{stageData.gridIds} -> ({cols}, {rows})");
        }
        // 決め打ち
        else
        {
            Debug.LogWarning($"StageCreator:ステージ{i}を決め打ちで生成しました！！！");
            int paramType = 0;
            if (i < 15)
            {
                if (isHard)
                {
                    paramType = 1;
                }
            }
            // ~24ステージ -> 3x3、4x4、5x5の繰り返し
            else if (i < 24)
            {
                if (i % 3 == 0)
                { }
                if (i % 3 == 1)
                {
                    paramType = 1;
                }
                if (i % 3 == 2) // = isHard
                {
                    paramType = 2;
                }
            }
            // それ以降~ -> 4x4、4x4、6x6の繰り返し
            else
            {
                if (!isHard)
                {
                    paramType = 1;
                }
                else
                {
                    paramType = 3;
                }
            }

            switch(paramType)
            {
                case 0:
                default:
                    cols = 3; rows = 3;
                    pieceNum = 4;
                    break;
                case 1:
                    cols = 4; rows = 4;
                    pieceNum = 6;
                    break;
                case 2:
                    cols = 5; rows = 5;
                    pieceNum = 8;
                    break;
                case 3:
                    cols = 6; rows = 6;
                    pieceNum = 12;
                    break;
            }
        }

        // splitter._trimShift = new Vector2(216f, 87f);
        // splitter._shiftY = -1.76f;

        // 各ステージごとの微調整
        // できれば調整が不要になるようCreatePieceを改善したい
        // 現仕様では1~9ステージと、12~27ステージ(3ステージごと)を確認して調整すると全パターン対応可能
        // iD:3 = 3x4　1,4,7ステージなど
        if(cols == 3 && rows == 4)
        {
            if( shapeType == ShapeType.Square)
            {
                splitter.fixTargetPercentCellSize = 0.995f;
                splitter._trimShift = new Vector2(0f, 1f);
            }
            if( shapeType == ShapeType.Triangle)
            {
                splitter._trimShift = new Vector2(324f, 87f);
            }
            if( shapeType == ShapeType.Hex)
            {
                splitter.targetPercent = 110;
                splitter._trimShift = new Vector2(0f, -1.45f);
            }
        }
        // iD:4 = 4x5 2,5,8ステージなど
        if(cols == 4 && rows == 5)
        {
            if( shapeType == ShapeType.Square)
            {
                splitter.fixTargetPercentCellSize = 0.995f;
                splitter._trimShift = new Vector2(0f, 1f);
            }
            if( shapeType == ShapeType.Triangle)
            {
                splitter._trimShift = new Vector2(324f, 87f);
                splitter.fixTargetPercentCellSize = 0.997f;
            }
            if( shapeType == ShapeType.Hex)
            {
                splitter.targetPercent = 115;
                splitter._trimShift = new Vector2(0f, -1.2f);
            }
        }
        // iD:5 = 5x7(四角六角)　3,9ステージなど
        if(cols == 5 && rows == 7)
        {
            if( shapeType == ShapeType.Hex)
            {
                splitter.targetPercent = 115;
                splitter._trimShift = new Vector2(0f, -0.88f);
            }
            if( shapeType == ShapeType.Square)
            {
                splitter.fixTargetPercentCellSize = 0.995f;
                splitter._trimShift = new Vector2(0f, 1f);
            }
        }
        // iD:5 = 6x6(三角) 6sテージなど
        if(cols == 6 && rows == 6)
        {
            splitter._trimShift = new Vector2(267f, 87f);
        }
        // iD:6 = 6x8(四角六角) 12,18ステージなど
        if(cols == 6 && rows == 8)
        {
            if( shapeType == ShapeType.Hex)
            {
                splitter._trimShift = new Vector2(0f, -0.78f);
                splitter.fixTargetPercentCellSize = 0.995f;
            }
            // 四角は特に調整不要
            if( shapeType == ShapeType.Square)
            {
                
            }
        }
        // iD:6 = 7x7(三角) 15ステージなど
        if(cols == 7 && rows == 7)
        {
            splitter._trimShift = new Vector2(278f, 87f);
        }
        // iD:7 = 7x8(四角六角) 21,27ステージなど
        if(cols == 7 && rows == 8)
        {
            if( shapeType == ShapeType.Square)
            {
                splitter.targetPercent = 88;
                splitter.fixTargetPercentCellSize = 0.995f;
            }
            if( shapeType == ShapeType.Hex)
            {
                splitter.targetPercent = 105;
                splitter._trimShift = new Vector2(0f, -0.69f);
            }
        }
        // iD:7 = 8x7(三角) 24ステージなど
        if(cols == 8 && rows == 7)
        {
            splitter._trimShift = new Vector2(312f, 175f);
            splitter.targetPercent = 131;
            splitter.fixTargetPercentCellSize = 0.9925f;
        }
        // iD:8 = 7x9(四角六角) デイリーステージなど
        if(cols == 7 && rows == 9)
        {
            if( shapeType == ShapeType.Hex)
            {
                splitter._trimShift = new Vector2(0f, -0.7f);
            }
        }
        // iD:8 = 8x8(三角) デイリーステージなど(現在未使用)
        if(cols == 8 && rows == 8)
        {
            splitter._trimShift = new Vector2(324f, 87f);
        }
    }

    // IDからグリッド数の取得を試みる
    private Vector2Int GetGridToId(string gridIds, int debugIndex, ShapeType shapeType)
    {
        Vector2Int ret = Vector2Int.zero;
        if(string.IsNullOrEmpty(gridIds))
        {
            Debug.Log($"StageCreator:StageCreator:GetGridToId:1:{debugIndex}, {shapeType}");
            return ret;
        }
        string[] values = gridIds.Split(" or ");
        int rendomIndex = UnityEngine.Random.Range(0, values.Length);
        string value = values[rendomIndex];
        int gridId = -1;
        if(int.TryParse(value, out gridId))
        {
            Debug.Log($"StageCreator:StageCreator:GetGridToId:2:{debugIndex}, {shapeType}");
            if(gridId == 3){ ret.x = 3; ret.y = 4; }
            if(gridId == 4){ ret.x = 4; ret.y = 5; }
            if(gridId == 5)
            { 
                if(shapeType == ShapeType.Triangle){ ret.x = 6; ret.y = 6;  }
                else{ ret.x = 5; ret.y = 7; }
            }
            if(gridId == 6)
            { 
                if(shapeType == ShapeType.Triangle){ ret.x = 7; ret.y = 7;  }
                else{ ret.x = 6; ret.y = 8; }
            }
            if(gridId == 7)
            { 
                if(shapeType == ShapeType.Triangle){ ret.x = 8; ret.y = 7;  }
                else{ ret.x = 7; ret.y = 8; }
            }
            if(gridId == 8)
            { 
                if(shapeType == ShapeType.Triangle){ ret.x = 8; ret.y = 8;  }
                else{ ret.x = 7; ret.y = 9; }
            }
        }
        else
        {
            Debug.Log($"StageCreator:StageCreator:GetGridToId:3:{debugIndex}, {shapeType}");
        }
        return ret;
    }

    private void UpdateSetSprites()
    {
        // 1. 順序を保持しつつ、重複を削除
        List<Sprite> distinctSplites = _setSplites
            .Where(sprite => sprite != null) // ★ null ではない要素のみをフィルタリング
            .Distinct()                      // ★ 重複を削除
            .ToList();                       // ★ リストに変換
        // 2. _setSplitesを更新
        _setSplites = distinctSplites;
        // 3. _splitesHashも、_setSplitesの内容で初期化し直す
        _splitesHash = new HashSet<Sprite>(_setSplites);
    }
    

    /// <summary>
    /// 現在のシーンにある指定された型の全コンポーネント（アクティブ/非アクティブ問わず）を取得します。
    /// </summary>
    /// <typeparam name="T">検索するMonoBehaviourの型</typeparam>
    /// <returns>シーン全体で見つかったT型のコンポーネントのリスト</returns>
    private List<T> FindAllInScene<T>() where T : MonoBehaviour
    {
        // 結果を格納するリスト
        List<T> results = new List<T>();

        // 現在アクティブなシーンを取得
        Scene activeScene = SceneManager.GetActiveScene();

        // 1. シーン内の全てのルート（最上位）のGameObjectを取得
        GameObject[] rootGameObjects = activeScene.GetRootGameObjects();

        // 2. 各ルートGameObjectから子孫も含めたコンポーネントを検索
        foreach (GameObject root in rootGameObjects)
        {
            // GetComponentsInChildren<T>(true) が重要なポイントです。
            // 第二引数に true を渡すことで、非アクティブなGameObjecitにアタッチされたコンポーネントも検索対象に含めます。
            T[] components = root.GetComponentsInChildren<T>(true);

            // 見つかったコンポーネントを結果リストに追加
            results.AddRange(components);
        }

        return results;
    }

    public void SetSkipFlg(bool isSkip)
    {
        List<AbstractGridImageSplitter> allSplitters = GetAllSplitters();
        foreach (var splitter in allSplitters)
        {
            splitter.isSkip = isSkip;
        }
    }
#endif

#if UNITY_EDITOR
    // --- 追加: 進捗バーの表示 ---
    private void UpdateProgressBar(string title, string info, float progress)
    {
        // キャンセルボタン付きの進捗バーを表示
        bool isCancelled = EditorUtility.DisplayCancelableProgressBar(title, info, progress);
        if (isCancelled)
        {
            Debug.LogWarning("StageCreator: ユーザーによって処理がキャンセルされました。");
            StopCreateStages(); // コルーチン停止
            // Async用のキャンセルトークンなどがあればここで振る（今回は簡易的に例外等で制御検討）
            throw new OperationCanceledException("User cancelled the operation.");
        }
    }

    // ステージ作成コルーチン（修正版：エラー回避済み）
    public IEnumerator CreateStagesCoroutine()
    {
        Debug.Log($"StageCreator:ステージ生成コルーチン:実行！");
        
        // 最後に必ずバーを消すためのフラグ管理
        bool isCompleted = false;

        if(IsPreSetUp)
        {
            EditorUtility.DisplayProgressBar("Stage Creation", "Pre-Setting Up...", 0.05f);
            PreSetUp();
            yield return null;
        }

        beforeStage = null;
        int total = _createPieceplitterList.Count;

        for(int i = 0; i < total; i++)
        {
            float progress = (float)i / total;
            AbstractGridImageSplitter splitter = _createPieceplitterList[i];
            GameObject stageObject = splitter.transform.parent.parent.gameObject;

            // キャンセルチェック
            bool isCancelled = EditorUtility.DisplayCancelableProgressBar(
                "Stage Creation", 
                $"Processing {stageObject.name} ({i + 1}/{total})", 
                progress
            );

            if (isCancelled)
            {
                Debug.LogWarning("StageCreator: ユーザーによって処理がキャンセルされました。");
                break; // ループを抜けて下の ClearProgressBar へ
            }

            if (beforeStage != null && (IsWaitBeforeSplit || IsWaitAfterSplit))
            {
                beforeStage.SetActive(false);
            }

            if(splitter.isSkip) continue;

            if(IsWaitBeforeSplit || IsWaitAfterSplit)
                stageObject.SetActive(true);

            if(IsWaitBeforeSplit) yield return null;

            splitter.CreatePiece();
            splitter.isSkip = true;
            beforeStage = stageObject;

            if(IsWaitAfterSplit) yield return null;
        }

        // 全ループ終了、または break (キャンセル) 時に必ず実行
        EditorUtility.ClearProgressBar();
        yield return null;
    }

    // 追加分: CreateStagesAsync（修正版）
    public async Task CreateStagesAsync()
    {
        Debug.Log($"StageCreator:ステージ生成Async:実行！");
        
        try {
            if(IsPreSetUp)
            {
                EditorUtility.DisplayProgressBar("Stage Creation", "Pre-Setting Up...", 0f);
                PreSetUp();
            }

            const int MAX_PARALLELISM = 8; 
            using var semaphore = new SemaphoreSlim(MAX_PARALLELISM); 
            List<Task> runningTasks = new List<Task>();
            
            int total = _createPieceplitterList.Count;
            int completedCount = 0;

            for(int i = 0; i < total; i++)
            {
                // Asyncの場合、ループ内でキャンセルチェック
                float progress = (float)completedCount / total;
                if (EditorUtility.DisplayCancelableProgressBar("Stage Creation (Async)", $"Waiting for queue... ({completedCount}/{total})", progress))
                {
                    throw new OperationCanceledException();
                }

                AbstractGridImageSplitter splitter = _createPieceplitterList[i];
                if(splitter.isSkip) { completedCount++; continue; }

                await semaphore.WaitAsync();

                Task pieceTask = splitter.CreatePieceAsync().ContinueWith(t => 
                {
                    semaphore.Release();
                    Interlocked.Increment(ref completedCount);
                    // メインスレッドではない場合があるため、ここでのUI更新は避けるか、注意が必要
                }, TaskScheduler.Default);
                
                runningTasks.Add(pieceTask);
            }
            
            // 全完了を待機している間もバーを出すためのループ（簡易版）
            while (completedCount < runningTasks.Count)
            {
                float progress = (float)completedCount / total;
                if (EditorUtility.DisplayCancelableProgressBar("Stage Creation (Async)", $"Processing tasks... ({completedCount}/{total})", progress))
                {
                    // 実行中のタスクを止めるのは難しいため、表示を消して中断
                    break; 
                }
                await Task.Delay(100);
            }
            await Task.WhenAll(runningTasks);
        }
        catch (OperationCanceledException) {
            Debug.LogWarning("StageCreator: Async処理がキャンセルされました。");
        }
        finally {
            EditorUtility.ClearProgressBar();
        }

        Debug.Log($"StageCreator:ステージ生成Async:終了");
    }
#endif
}

#if UNITY_EDITOR
[CustomEditor(typeof(StageCreator))]
public class StageCreatorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        StageCreator script = (StageCreator)target;

        GUILayout.Space(10);

        if (GUILayout.Button("CreateStages"))
        {
            script.IsPreSetUp = true;
            script.IsForce = false;
            script.CreateStages();
        }
        if (GUILayout.Button("ForceCreateStages"))
        {
            script.IsPreSetUp = true;
            script.IsForce = true;
            script.CreateStages();
        }
        if (GUILayout.Button("PreSetUpOnly"))
        {
            script.PreSetUp();
        }
        if (GUILayout.Button("CreateStagesUnPreSet"))
        {
            script.IsPreSetUp = false;
            script.CreateStages();
        }
        if (GUILayout.Button("StopCreateStages"))
        {
            script.IsForce = false;
            script.StopCreateStages();
        }

        if (GUILayout.Button("All Skip"))
        {
            script.SetSkipFlg(true);
        }
        if (GUILayout.Button("All UnSkip"))
        {
            script.SetSkipFlg(false);
        }
        if (GUILayout.Button("Update Set Sprite FileName"))
        {
            script.UpdateSetSpriteFileName();
        }

        DrawDefaultInspector();
    }
}
#endif
