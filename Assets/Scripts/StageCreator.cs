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

        // 全ステージ取得 (AbstractGridImageSplitterを継承した全てを取得)
        List<AbstractGridImageSplitter> allSplitters = GetAllSplitters();
        AllSplitters = new List<AbstractGridImageSplitter>();

        // ★ 簡略化: 型による分類は不要、全てAbstractGridImageSplitterとして統一処理
        _createPieceplitterList = new List<AbstractGridImageSplitter>();
        
        int index = 0;
        foreach (var splitter in allSplitters)
        {
            GameObject stageObject = splitter.transform.parent.parent.gameObject;
            stageObject.SetActive(false);
            
            // 参考用のステージは弾く
            if (splitter.isPrefs || splitter.isCreative)
            {
                stageObject.name = splitter.isPrefs ? "Prefs" : "Creative";
                continue;
            }
            
            stageObject.name = $"Stage{index + 1:D3} SetWait";
            index++;

            // AllSplittersに追加（型関係なく統一処理）
            AllSplitters.Add(splitter);
        }

        Debug.Log($"StageCreator:全ステージセットアップ開始！:合計 {AllSplitters.Count}個");

        // ステージ設定処理
        for (int i = 0; i < AllSplitters.Count; i++)
        {
            AbstractGridImageSplitter currentSplitter = AllSplitters[i];
            
            bool isHard = (i + 1) % 3 == 0;
            if(IsDailyStage)
                isHard = false;
            
            // ShapeTypeを決定（ステージデータから、またはデフォルトパターン）
            ShapeType shapeType = DetermineShapeType(i);
            
            // ★ ShapeTypeを設定（GridImageSplitterのみ対応）
            if (currentSplitter is GridImageSplitter gridSplitter)
            {
                gridSplitter.SetShapeType(shapeType);
            }
            
            // Strategyから推奨値を取得
            IShapeStrategy strategy = ShapeStrategyFactory.GetStrategy(shapeType);
            currentSplitter.targetPercent = strategy.GetTargetPercent();
            
            // Shadow spriteを設定
            currentSplitter._shadowSprite = GetShadowSpriteForShape(shapeType);

            // ステージパラメータ取得と設定
            GetStageParam(i, isHard, out int cols, out int rows, out int pieceNum, currentSplitter, shapeType);
            SetImage(currentSplitter, i); 

            GameObject stageObject = currentSplitter.transform.parent.parent.gameObject;
            stageObject.GetComponent<StageInfo>().isHard = isHard;

            // 共通処理
            CommonSplitterProcces(currentSplitter.gameObject, i, ref currentSplitter.isSkip);
            
            // スキップ判定
            if (string.IsNullOrEmpty(currentSplitter.PieceCreateSeed) ||
                currentSplitter.cols != cols ||
                currentSplitter.rows != rows ||
                currentSplitter._pieceNum != pieceNum ||
                IsForce)
            {
                currentSplitter.isSkip = false;
            }

            if (!currentSplitter.isSkip)
            {
                currentSplitter.cols = cols;
                currentSplitter.rows = rows;
                currentSplitter._pieceNum = pieceNum;
                _createPieceplitterList.Add(currentSplitter);
            }
        }
    }

    /// <summary>
    /// ステージインデックスからShapeTypeを決定
    /// </summary>
    private ShapeType DetermineShapeType(int stageIndex)
    {
        StageData stageData = GetStageData(stageIndex);
        if (stageData != null && !IsDailyStage)
        {
            return stageData.shapeType;
        }

        // デフォルトパターン
        int typeInt = (stageIndex / 3) % 3;
        if (IsDailyStage)
        {
            typeInt = stageIndex % 2;
            if (typeInt == 1) typeInt = 2;
        }

        return typeInt switch
        {
            0 => ShapeType.Square,
            1 => ShapeType.Triangle,
            2 => ShapeType.Hex,
            _ => ShapeType.Square
        };
    }

    /// <summary>
    /// ShapeTypeに応じたShadowSpriteを取得
    /// </summary>
    private Sprite GetShadowSpriteForShape(ShapeType shapeType)
    {
        return shapeType switch
        {
            ShapeType.Square => _shadowSpriteSquare,
            ShapeType.Triangle => _shadowSpriteTriangle,
            ShapeType.Hex => _shadowSpriteHex,
            _ => _shadowSpriteSquare
        };
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
        
        // まず重複した番号プレフィックスを削除
        CleanupDuplicateNumberPrefixes(_setSplites);
        
        string prefix = IsDailyStage ? "Daily" : "Stage";
        SpriteFileNameUtil.UpdateSpriteFileNames(_setSplites, prefix, startNumber: 1, enableLog: true);
    }
    
    /// <summary>
    /// スプライトのファイル名から重複した番号プレフィックス（例：001_001_Hoge）を削除します
    /// </summary>
    private void CleanupDuplicateNumberPrefixes(List<Sprite> sprites)
    {
        foreach (var sprite in sprites)
        {
            if (sprite == null) continue;
            
            string assetPath = AssetDatabase.GetAssetPath(sprite);
            if (string.IsNullOrEmpty(assetPath)) continue;
            
            string fileName = Path.GetFileNameWithoutExtension(assetPath);
            string directory = Path.GetDirectoryName(assetPath);
            string extension = Path.GetExtension(assetPath);
            
            // 重複した番号プレフィックスを検出して削除（例：001_042_Hoge -> Hoge）
            // パターン: 先頭の数字3桁+アンダースコアが複数回繰り返される
            string cleanedName = System.Text.RegularExpressions.Regex.Replace(
                fileName, 
                @"^(\d{3}_)+", // 先頭の「数字3桁_」を1回以上マッチ
                "" // 全て削除
            );
            
            // ファイル名が変更された場合のみリネーム
            if (cleanedName != fileName)
            {
                string newPath = Path.Combine(directory, cleanedName + extension);
                string result = AssetDatabase.RenameAsset(assetPath, cleanedName + extension);
                
                if (string.IsNullOrEmpty(result))
                {
                    Debug.Log($"<color=cyan>[Cleanup]</color> {fileName}{extension} -> {cleanedName}{extension}");
                }
                else
                {
                    Debug.LogWarning($"<color=yellow>[Cleanup Failed]</color> {fileName}: {result}");
                }
            }
        }
        
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
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
            pieceNum = stageData.pieceNum;
            // pieceNum = -1;
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
        if(UnityEditor.EditorApplication.isPlaying)
            return;
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
