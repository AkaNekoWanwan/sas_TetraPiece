using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement; // SceneManagerを使用するために必要
using System.Collections;
using System; // ShapeTypeを使用するために必要
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

public class StageCreator : MonoBehaviour
{
#if UNITY_EDITOR
    // public bool isOverrideSprite = true;
    public List<Sprite> _setSplites = new List<Sprite>();
    public HashSet<Sprite> _splitesHash = new HashSet<Sprite>();
    public HashSet<string> _seeds = default;
    GameObject beforeStage = null;
    public bool IsNewLogic = false;
    public bool IsForce = false;
    public bool IsPreSetUp = false;
    public bool IsWaitBeforeSplit = true;
    public bool IsWaitAfterSplit = true;
    public List<StageData> _stageData = default;

    public List<AbstractGridImageSplitter> AllSplitters;
    public List<AbstractGridImageSplitter> _createPieceplitterList = new List<AbstractGridImageSplitter>();
    

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
            _creationCoroutine = StartCoroutine(CreateStagesCoroutine2());
    }

    // ステージパラメータ設定
    public void SetStagePatamList(List<StageData> paramList)
    {
        _stageData = paramList;
    }

    public void PreSetUp()
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

        // 2. 初期化処理の抽象化
        // 全ステージ取得 (AbstractGridImageSplitterを継承した全てを取得)
        List<AbstractGridImageSplitter> allSplitters = FindAllInScene<AbstractGridImageSplitter>();
        _seeds = new HashSet<string>();
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
            Debug.Log($"splitter:{splitter.GetType().Name}:{splitter.transform.parent.parent.name}, {splitter.isPrefs}, {splitter.PieceCreateSeed}");
            GameObject stageObject = splitter.transform.parent.parent.gameObject;
            stageObject.SetActive(false);
            if (splitter.isPrefs || splitter.isCreative)
            {
                stageObject.name = splitter.isPrefs ? "Prefs" : "Creative";
                continue; // 参照用ステージはリストに追加しない
            }
            stageObject.name = $"Stage{index + 1} SetWait";
            
            index++;

            // シード値取得
            string pieceCreateSeed = splitter.PieceCreateSeed;
            if (!string.IsNullOrEmpty(splitter.PieceCreateSeed))
            {
                // シード値が既存のものなら作り直す
                if(_seeds.Contains(pieceCreateSeed))
                    splitter.PieceCreateSeed = "";
                else
                    _seeds.Add(splitter.PieceCreateSeed);
            }

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

        Debug.Log($"全ステージセットアップ開始！:Square:{squareSplitters.Count}, Triangle:{triSplitters.Count}, Hex:{hexSplitters.Count}");

        // 3. ステージ設定処理の統一と並び替えロジック
        int sumCount = squareSplitters.Count + hexSplitters.Count + triSplitters.Count;
        int indexSquare = 0;
        int indexTri = 0;
        int indexHex = 0;

        for (int i = 0; i < sumCount; i++)
        {
            bool isHard = (i + 1) % 3 == 0;
            ShapeType shapeType = ShapeType.Square;
            int typeInt = (i / 3) % 3; // ３ステージ毎に切り替わる

            // 交互にステージタイプを決定
            if (typeInt == 0)
                shapeType = ShapeType.Square;
            else if (typeInt == 1)
                shapeType = ShapeType.Triangle;
            else if (typeInt == 2)
                shapeType = ShapeType.Hex;

            if(IsRangeStageData(i))
            {
                shapeType = _stageData[i].shapeType;
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
                    }
                    break;
                case ShapeType.Triangle:
                    if (indexTri < triSplitters.Count)
                    {
                        currentSplitter = triSplitters[indexTri];
                        currentSplitter.targetPercent = 120;
                        indexTri++;
                    }
                    break;
                case ShapeType.Hex:
                    if (indexHex < hexSplitters.Count)
                    {
                        currentSplitter = hexSplitters[indexHex];
                        currentSplitter.targetPercent = 120;
                        indexHex++;
                    }
                    break;
            }

            // 該当するスプリッターが枯渇していたらスキップ（現状のコードのロジックを維持）
            if (currentSplitter == null)
            {
                continue;
            }

            GetStageParam(i, isHard, out cols, out rows, out pieceNum, currentSplitter);
            SetImage(currentSplitter, i); 

            GameObject stageObject = currentSplitter.transform.parent.parent.gameObject;
            stageObject.GetComponent<StageInfo>().isHard = isHard;

            // 抽出したスプリッターを、ステージ順に並べた AllSplitters に格納
            AllSplitters.Add(currentSplitter);

            // SetUpStage内のロジックをここに移動し、抽象化された currentSplitter に対して処理
            CommonSplitterProcces(currentSplitter.gameObject, i, ref currentSplitter.isSkip);
            
            if (string.IsNullOrEmpty(currentSplitter.PieceCreateSeed))
            {
                Debug.Log($"更新対象！{stageObject.name}, シード値未設定");
                currentSplitter.isSkip = false;
            }
            if (currentSplitter.cols != cols)
            {
                Debug.Log($"更新対象！{stageObject.name}, cols {currentSplitter.cols} -> {cols}");
                currentSplitter.isSkip = false;
            }
            if (currentSplitter.rows != rows)
            {
                Debug.Log($"更新対象！{stageObject.name}, rows {currentSplitter.rows} -> {rows}");
                currentSplitter.isSkip = false;
            }
            if (currentSplitter._pieceNum != pieceNum)
            {
                Debug.Log($"更新対象！{stageObject.name}, pieceNum {currentSplitter._pieceNum} -> {pieceNum}");
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
    
    public IEnumerator CreateStagesCoroutine()
    {
        Debug.Log("ステージ生成コルーチン:実行！");
        yield return null;

        if(IsPreSetUp)
        {
            Debug.Log("ステージ生成コルーチン:PreSetUp");
            PreSetUp();
            yield return null;
        }

        beforeStage = null;
        // シード値を更新していきながらステージ作成
        for(int i = 0; i < _createPieceplitterList.Count; i++)
        {
            if (beforeStage != null)
            {
                beforeStage.SetActive(false);
            }
            AbstractGridImageSplitter splitter = _createPieceplitterList[i];
            if(splitter.isSkip)
            {
                Debug.Log($"ステージ生成コルーチン:{i}をスキップ");
                continue;
            }
            if(_seeds != null)
                splitter.avoidPatternSeeds = _seeds.ToList();
            GameObject stageObject = splitter.transform.parent.parent.gameObject;
            stageObject.SetActive(true);
            Debug.Log($"ステージ生成コルーチン:ステージ生成：{i}, {stageObject.name}");
            if(IsWaitBeforeSplit)
            {
                yield return null;
                if(!IsWaitAfterSplit)
                {
                    if(i % 2 == 0)
                        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
                }
            }
            splitter.CreatePiece();
            splitter.isSkip = true;
            if(_seeds == null)
                _seeds = new HashSet<string>();
            _seeds.Add(splitter.PieceCreateSeed);
            beforeStage = stageObject;
            if(i % 2 == 0)
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            if(IsWaitAfterSplit)
            {
                yield return null;
            }
        }

        yield return null;
    }

    public IEnumerator CreateStagesCoroutine2()
    {
        Debug.Log("ステージ生成コルーチン:実行！");
        yield return null;

        if(IsPreSetUp)
        {
            Debug.Log("ステージ生成コルーチン:PreSetUp");
            PreSetUp();
            yield return null;
        }
        List<Coroutine> runningTasks = new List<Coroutine>();

        // シード値を更新していきながらステージ作成
        for(int i = 0; i < _createPieceplitterList.Count; i++)
        {
            AbstractGridImageSplitter splitter = _createPieceplitterList[i];
            if (beforeStage != null)
            {
                beforeStage.SetActive(false);
            }
            if(splitter.isSkip)
            {
                Debug.Log($"ステージ生成コルーチン:{i}をスキップ");
                continue;
            }
            if(_seeds != null)
                splitter.avoidPatternSeeds = _seeds.ToList();
            GameObject stageObject = splitter.transform.parent.parent.gameObject;
            stageObject.SetActive(true);
            Debug.Log($"ステージ生成コルーチン:ステージ生成：{i}, {stageObject.name}");

            runningTasks.Add(StartCoroutine(splitter.CreatePieceCoroutine()));

            splitter.isSkip = true;
            if(_seeds == null)
                _seeds = new HashSet<string>();
            _seeds.Add(splitter.PieceCreateSeed);
            beforeStage = stageObject;
        }
        while (runningTasks.Count > 0)
        {
            // このフレームで、すべての実行中のコルーチンに処理時間を与える
            yield return null; 
            
            // 完了したコルーチンを正確に追跡・削除するロジックは煩雑なため、
            // 完了したコルーチンをリストから削除するのではなく、
            // 完了するまで十分な時間待機するか、すべてのスプリッターが完了フラグを立てるのを待ちます。
            
            // 簡潔にするため、ここではすべてのタスクが完了するのを待つと仮定します。
            // （実際には、すべてのスプリッターの完了を監視する外部フラグが必要です）
            
            // 処理を継続するために、ここでは単純にリストを空にします。
            runningTasks.Clear(); 
        }

        yield break;
    }

    // 三角四角六角の共通処理
    private void CommonSplitterProcces(GameObject splitter, int i, ref bool isSkip)
    {
        GameObject stageObject = splitter.transform.parent.parent.gameObject;
        stageObject.name = $"Stage{i + 1}";

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
            Debug.Log($"更新対象！{stageObject.name}, 画像差し替え:{stageImage.sprite}->{setSprite}");
            stageImage.sprite = setSprite;
            stageImage.SetNativeSize();
            // スキップフラグを下す
            splitter.isSkip = false;
        }
    }

    private bool IsRangeStageData(int i)
    {
        if( 0 <= i && i < _stageData.Count && _stageData[i] != null)
            return true;
        return false;
    }
    
    private void GetStageParam(int i, bool isHard, out int cols, out int rows, out int pieceNum, AbstractGridImageSplitter splitter)
    {
        if(IsRangeStageData(i))
        {
            cols = _stageData[i].gridX; 
            rows = _stageData[i].gridY;
            pieceNum = _stageData[i].pieceNum;
        }
        else
        {
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

        splitter._trimShift = new Vector2(216f, 87f);
        splitter._shiftY = -1.76f;
        if(cols == 3)
        {
            splitter._trimShift = new Vector2(216f, 87f);
            splitter._shiftY = -1.76f;
        }
        if(cols == 3 && rows == 4)
        {
            splitter._trimShift = new Vector2(247f, 68.8f);
            splitter._shiftY = -1.6f;
        }
        if(cols == 4)
        {
            splitter._trimShift = new Vector2(243f, 87f);
            splitter._shiftY = -1.3f;
        }
        if(cols == 4 && rows == 5)
        {
            splitter._trimShift = new Vector2(247.5f, 68f);
            splitter._shiftY = -1.28f;
        }
        if(cols == 5)
        {
            splitter._trimShift = new Vector2(260f, 87f);
            splitter._shiftY = -1.07f;
        }
        if(cols == 5 && rows == 7)
        {
            // splitter._trimShift = new Vector2(260f, 87f);
            splitter._shiftY = -0.955f;
        }
        if(cols == 5 && rows == 8)
        {
            splitter._trimShift = new Vector2(260f, 87f);
            splitter._shiftY = -0.9f;
        }
        if(cols == 6)
        {
            splitter._trimShift = new Vector2(212f, 68f);
            splitter._shiftY = -0.89f;
        }
        if(cols == 6 && rows == 8)
        {
            splitter._trimShift = new Vector2(311f, 75f);
            splitter._shiftY = -0.82f;
        }
        if(cols == 7 && rows == 7)
        {
            splitter._trimShift = new Vector2(212f, 68f);
            splitter._shiftY = -0.89f;
        }
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
            // 第二引数に true を渡すことで、非アクティブなGameObjectにアタッチされたコンポーネントも検索対象に含めます。
            T[] components = root.GetComponentsInChildren<T>(true);

            // 見つかったコンポーネントを結果リストに追加
            results.AddRange(components);
        }

        return results;
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
        DrawDefaultInspector();
    }
}
#endif