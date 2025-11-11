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

public class StageCreator : MonoBehaviour
{
#if UNITY_EDITOR
    // public bool isOverrideSprite = true;
    public List<Sprite> _setSplites = new List<Sprite>();
    public HashSet<Sprite> _splitesHash = new HashSet<Sprite>();
    public HashSet<string> _seeds = default;
    GameObject beforeStage = null;
    public bool IsForce = false;

    public List<AbstractGridImageSplitter> AllSplitters;

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
        _creationCoroutine = StartCoroutine(CreateStagesCoroutine());
    }
    
    public IEnumerator CreateStagesCoroutine()
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
        List<AbstractGridImageSplitter> createPieceplitterList = new List<AbstractGridImageSplitter>();
        
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
        yield return null;

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
                        indexSquare++;
                    }
                    break;
                case ShapeType.Triangle:
                    if (indexTri < triSplitters.Count)
                    {
                        currentSplitter = triSplitters[indexTri];
                        indexTri++;
                    }
                    break;
                case ShapeType.Hex:
                    if (indexHex < hexSplitters.Count)
                    {
                        currentSplitter = hexSplitters[indexHex];
                        indexHex++;
                    }
                    break;
            }

            // 該当するスプリッターが枯渇していたらスキップ（現状のコードのロジックを維持）
            if (currentSplitter == null)
            {
                yield return null;
                continue;
            }

            GetStageParam(i, isHard, out cols, out rows, out pieceNum, currentSplitter);
            SetImage(currentSplitter, i); 

            GameObject stageObject = currentSplitter.transform.parent.parent.gameObject;

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
                createPieceplitterList.Add(currentSplitter);
                currentSplitter.isSkip = true;
            }
            // yield return null;
        }

        // シード値を更新していきながらステージ作成
        for(int i = 0; i < createPieceplitterList.Count; i++)
        {
            if (0 < i)
            {
                beforeStage.SetActive(false);
            }
            AbstractGridImageSplitter splitter = createPieceplitterList[i];
            splitter.avoidPatternSeeds = _seeds.ToList();
            GameObject stageObject = splitter.transform.parent.parent.gameObject;
            stageObject.SetActive(true);
            Debug.Log($"ステージ生成：{stageObject.name}");
            yield return null;
            splitter.CreatePiece();
            _seeds.Add(splitter.PieceCreateSeed);
            beforeStage = stageObject;
            yield return null;
        }

        yield return null;
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
    
    private void GetStageParam(int i, bool isHard, out int cols, out int rows, out int pieceNum, AbstractGridImageSplitter splitter)
    {
        int paramType = 0;
        // cols = 3;
        // rows = 3;
        // pieceNum = 4;
        // splitter._trimShift = new Vector2(216f, 87f);
        // splitter._shiftY = -1.76f;
        // 1~15ステージ -> 基本的に3x3。３ステージごとに4x4
        if (i < 15)
        {
            if (isHard)
            {
                paramType = 1;
                // cols = 4;
                // rows = 4;
                // pieceNum = 6;
                // splitter._trimShift = new Vector2(216f, 87f);
                // splitter._shiftY = -1.3f;
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
                // cols = 4;
                // rows = 4;
                // pieceNum = 6;
                // splitter._trimShift = new Vector2(216f, 87f);
                // splitter._shiftY = -1.3f;
            }
            if (i % 3 == 2) // = isHard
            {
                paramType = 2;
                // cols = 5;
                // rows = 5;
                // pieceNum = 8;
                // splitter._trimShift = new Vector2(216f, 87f);
                // splitter._shiftY = -1.76f;
            }
        }
        // それ以降~ -> 4x4、4x4、6x6の繰り返し
        else
        {
            if (!isHard)
            {
                paramType = 1;
                // cols = 4;
                // rows = 4;
                // pieceNum = 6;
                // splitter._trimShift = new Vector2(216f, 87f);
                // splitter._shiftY = -1.3f;
            }
            else
            {
                paramType = 3;
                // cols = 6;
                // rows = 6;
                // pieceNum = 12;
                // splitter._trimShift = new Vector2(216f, 87f);
                // splitter._shiftY = -1.76f;
            }
        }

        switch(paramType)
        {
            case 0:
            default:
                cols = 3; rows = 3;
                pieceNum = 4;
                splitter._trimShift = new Vector2(216f, 87f);
                splitter._shiftY = -1.76f;
                break;
            case 1:
                cols = 4; rows = 4;
                pieceNum = 6;
                splitter._trimShift = new Vector2(243f, 87f);
                splitter._shiftY = -1.3f;
                break;
            case 2:
                cols = 5; rows = 5;
                pieceNum = 8;
                splitter._trimShift = new Vector2(260f, 87f);
                splitter._shiftY = -1.07f;
                break;
            case 3:
                cols = 6; rows = 6;
                pieceNum = 12;
                splitter._trimShift = new Vector2(270f, 87f);
                splitter._shiftY = -0.89f;
                break;
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
            script.IsForce = false;
            script.CreateStages();
            // script.StartCoroutine(script.CreateStagesCoroutine());
        }
        if (GUILayout.Button("ForceCreateStages"))
        {
            script.IsForce = true;
            script.CreateStages();
            // script.StartCoroutine(script.CreateStagesCoroutine());
        }
        if (GUILayout.Button("StopCreateStages"))
        {
            script.IsForce = false;
            script.StopCreateStages();
            // script.StartCoroutine(script.CreateStagesCoroutine());
        }
        DrawDefaultInspector();
    }
}
#endif