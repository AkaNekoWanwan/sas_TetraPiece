using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.IO;

public class GridImageAlignmentChecker : EditorWindow
{
    private string scenePath = "";
    private Vector2 scrollPosition;
    private List<AlignmentResult> results = new List<AlignmentResult>();
    private float pixelMatchThreshold = 0.95f; // ピクセル一致率の閾値（95%以上）
    private bool showOnlyErrors = true;
    private bool isChecking = false;
    private bool useSceneInstances = true; // シーンインスタンスを使用するか

    private class AlignmentResult
    {
        public string prefabName;
        public string prefabPath;
        public bool hasImageMismatch;
        public bool hasImageError;
        public float minMatchRate;
        public int totalCells;
        public int checkedCells;
        public string errorDetails;
        public ShapeType shapeType;
    }

    [MenuItem("Tools/Grid Image Alignment Checker")]
    public static void ShowWindow()
    {
        GetWindow<GridImageAlignmentChecker>("画像配置検証ツール");
    }

    private void OnGUI()
    {
        GUILayout.Label("画像配置検証ツール", EditorStyles.boldLabel);
        GUILayout.Space(10);

        // 設定エリア
        EditorGUILayout.BeginVertical("box");
        
        useSceneInstances = EditorGUILayout.Toggle("シーンインスタンスを使用:", useSceneInstances);
        
        if (useSceneInstances)
        {
            EditorGUILayout.HelpBox("現在開いているシーン上のステージインスタンスを検証します。\n（高速・画像ロード済み）", MessageType.Info);
        }
        else
        {
            scenePath = EditorGUILayout.TextField("シーンパス:", scenePath);
            EditorGUILayout.HelpBox("指定シーンを開いてステージインスタンスを検証します。\n（複数シーン対応・やや低速）", MessageType.Info);
        }
        
        pixelMatchThreshold = EditorGUILayout.Slider("ピクセル一致率閾値:", pixelMatchThreshold, 0.8f, 1.0f);
        EditorGUILayout.HelpBox("GridCellのスプライトと元画像の該当範囲を比較します。\n一致率が閾値未満の場合、ずれとして検出されます。", MessageType.Info);
        showOnlyErrors = EditorGUILayout.Toggle("エラーのみ表示:", showOnlyErrors);
        EditorGUILayout.EndVertical();

        GUILayout.Space(10);

        // 実行ボタン
        GUI.enabled = !isChecking;
        if (GUILayout.Button("検証開始", GUILayout.Height(30)))
        {
            if (useSceneInstances)
            {
                CheckCurrentScene();
            }
            else
            {
                CheckSpecifiedScene();
            }
        }
        
        if (GUILayout.Button("目視確認用: シーンインスタンスを加工", GUILayout.Height(30)))
        {
            SimplifySceneInstancesForInspection();
        }
        GUI.enabled = true;

        if (isChecking)
        {
            GUILayout.Label("検証中...", EditorStyles.boldLabel);
            return;
        }

        GUILayout.Space(10);

        // 結果サマリー
        if (results.Count > 0)
        {
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("検証結果サマリー", EditorStyles.boldLabel);
            int errorCount = results.Count(r => r.hasImageMismatch || r.hasImageError);
            int okCount = results.Count - errorCount;
            
            EditorGUILayout.LabelField($"総プレハブ数: {results.Count}");
            EditorGUILayout.LabelField($"正常: {okCount}", new GUIStyle(EditorStyles.label) { normal = { textColor = Color.green } });
            EditorGUILayout.LabelField($"エラー: {errorCount}", new GUIStyle(EditorStyles.label) { normal = { textColor = Color.red } });
            EditorGUILayout.EndVertical();

            GUILayout.Space(5);

            if (GUILayout.Button("結果をCSV出力"))
            {
                ExportToCSV();
            }
        }

        GUILayout.Space(10);

        // 結果リスト
        if (results.Count > 0)
        {
            GUILayout.Label("詳細結果:", EditorStyles.boldLabel);
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            var displayResults = showOnlyErrors 
                ? results.Where(r => r.hasImageMismatch || r.hasImageError).ToList() 
                : results;

            foreach (var result in displayResults)
            {
                bool hasError = result.hasImageMismatch || result.hasImageError;
                Color bgColor = hasError ? new Color(1f, 0.8f, 0.8f) : new Color(0.8f, 1f, 0.8f);
                
                GUI.backgroundColor = bgColor;
                EditorGUILayout.BeginVertical("box");
                GUI.backgroundColor = Color.white;

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(result.prefabName, EditorStyles.boldLabel, GUILayout.Width(200));
                EditorGUILayout.LabelField($"[{result.shapeType}]", GUILayout.Width(80));
                
                if (hasError)
                {
                    GUILayout.Label("❌ エラー", new GUIStyle(EditorStyles.label) { normal = { textColor = Color.red } });
                }
                else
                {
                    GUILayout.Label("✓ 正常", new GUIStyle(EditorStyles.label) { normal = { textColor = Color.green } });
                }
                
                if (GUILayout.Button("開く", GUILayout.Width(60)))
                {
                    Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(result.prefabPath);
                    EditorGUIUtility.PingObject(Selection.activeObject);
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.LabelField($"セル数: {result.totalCells} (検証: {result.checkedCells})");
                
                if (result.hasImageMismatch)
                {
                    EditorGUILayout.LabelField($"最小一致率: {result.minMatchRate:P1}", new GUIStyle(EditorStyles.label) { normal = { textColor = Color.red } });
                }
                
                if (!string.IsNullOrEmpty(result.errorDetails))
                {
                    EditorGUILayout.LabelField("詳細:", EditorStyles.miniLabel);
                    EditorGUILayout.TextArea(result.errorDetails, GUILayout.Height(40));
                }

                EditorGUILayout.EndVertical();
                GUILayout.Space(3);
            }

            EditorGUILayout.EndScrollView();
        }
    }

    /// <summary>
    /// 目視確認用：シーンインスタンスを加工する（プレハブは保存しない）
    /// </summary>
    private void SimplifySceneInstancesForInspection()
    {
        if (!EditorUtility.DisplayDialog("確認", 
            "シーン上のステージを目視確認しやすいように加工します。\n\n" +
            "・各種PieceDragControllerのlocalScaleをVector3.oneに\n" +
            "・cell_copyを全て非アクティブに\n" +
            "・shadowを全て非アクティブに\n" +
            "・GridCellを全て非アクティブに\n\n" +
            "※プレハブは保存されません（シーンのみ変更）\n" +
            "※この操作は元に戻せないため、必要に応じてシーンを再読み込みしてください", 
            "実行", "キャンセル"))
        {
            return;
        }

        int dragControllerCount = 0;
        int cellCopyCount = 0;
        int shadowCount = 0;
        int gridCellCount = 0;

        // 1. シーン全体から各種PieceDragControllerを取得してlocalScaleをVector3.oneに
        var dragControllers = GameObject.FindObjectsOfType<PieceDragController>(true);
        foreach (var controller in dragControllers)
        {
            controller.transform.localScale = Vector3.one;
            dragControllerCount++;
        }

        // 2. シーン全体から"cell_copy"と"shadow"を検索して非アクティブに
        var allTransforms = Resources.FindObjectsOfTypeAll<Transform>();
        foreach (Transform t in allTransforms)
        {
            // シーン内のオブジェクトのみ対象（プレハブアセットは除外）
            if (t.gameObject.scene.IsValid())
            {
                if (t.name == "cell_copy")
                {
                    t.gameObject.SetActive(false);
                    cellCopyCount++;
                }
                else if (t.name == "shadow")
                {
                    t.gameObject.SetActive(false);
                    shadowCount++;
                }
            }
        }

        // 3. シーン全体からGridCellを全て取得して非アクティブに
        var gridCells = GameObject.FindObjectsOfType<GridCell>(true);
        foreach (var gridCell in gridCells)
        {
            gridCell.gameObject.SetActive(false);
            gridCellCount++;
        }

        EditorUtility.DisplayDialog("完了", 
            $"加工完了:\n" +
            $"・PieceDragController: {dragControllerCount}個\n" +
            $"・cell_copy: {cellCopyCount}個\n" +
            $"・shadow: {shadowCount}個\n" +
            $"・GridCell: {gridCellCount}個\n\n" +
            "元に戻すには、シーンを再読み込みしてください。", 
            "OK");
        
        // シーンを編集済みとしてマーク
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
    }

    private void CheckCurrentScene()
    {
        results.Clear();
        isChecking = true;

        try
        {
            // 現在のシーン上のAbstractGridImageSplitterを全取得
            var splitters = GameObject.FindObjectsOfType<AbstractGridImageSplitter>(true);
            
            if (splitters.Length == 0)
            {
                EditorUtility.DisplayDialog("検証結果", "現在のシーンにステージが見つかりませんでした。", "OK");
                return;
            }

            int total = splitters.Length;
            int current = 0;

            foreach (var splitter in splitters)
            {
                current++;
                
                if (EditorUtility.DisplayCancelableProgressBar("検証中", $"{current}/{total}: {splitter.gameObject.name}", (float)current / total))
                {
                    break;
                }

                // Prefabパスを取得
                string prefabPath = GetPrefabPath(splitter.gameObject);
                var result = CheckSplitter(splitter, prefabPath);
                if (result != null)
                {
                    results.Add(result);
                }
            }
            
            // 結果をプレハブ名順にソート
            results.Sort((a, b) => string.Compare(a.prefabName, b.prefabName, System.StringComparison.Ordinal));
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            isChecking = false;
            Repaint();
        }
    }

    private void CheckSpecifiedScene()
    {
        if (string.IsNullOrEmpty(scenePath))
        {
            EditorUtility.DisplayDialog("エラー", "シーンパスを指定してください。", "OK");
            return;
        }

        results.Clear();
        isChecking = true;

        try
        {
            // シーンを開く
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
            
            // シーン上のAbstractGridImageSplitterを全取得
            var splitters = GameObject.FindObjectsOfType<AbstractGridImageSplitter>(true);
            
            if (splitters.Length == 0)
            {
                EditorUtility.DisplayDialog("検証結果", "指定シーンにステージが見つかりませんでした。", "OK");
                return;
            }

            int total = splitters.Length;
            int current = 0;

            foreach (var splitter in splitters)
            {
                current++;
                
                if (EditorUtility.DisplayCancelableProgressBar("検証中", $"{current}/{total}: {splitter.gameObject.name}", (float)current / total))
                {
                    break;
                }

                string prefabPath = GetPrefabPath(splitter.gameObject);
                var result = CheckSplitter(splitter, prefabPath);
                if (result != null)
                {
                    results.Add(result);
                }
            }
            
            // 結果をプレハブ名順にソート
            results.Sort((a, b) => string.Compare(a.prefabName, b.prefabName, System.StringComparison.Ordinal));
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            isChecking = false;
            Repaint();
        }
    }

    private string GetPrefabPath(GameObject obj)
    {
        // ルートまで遡る
        Transform root = obj.transform;
        while (root.parent != null)
        {
            root = root.parent;
        }

        // Prefabパスを取得
        var prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(root.gameObject);
        if (prefabAsset != null)
        {
            return AssetDatabase.GetAssetPath(prefabAsset);
        }

        return "Unknown";
    }

    private void CheckAllPrefabs()
    {
        // 旧実装（削除予定）
        EditorUtility.DisplayDialog("エラー", "この機能は廃止されました。シーンインスタンス方式を使用してください。", "OK");
    }

    private AlignmentResult CheckSplitter(AbstractGridImageSplitter splitter, string prefabPath)
    {
        var result = new AlignmentResult
        {
            prefabName = Path.GetFileNameWithoutExtension(prefabPath),
            prefabPath = prefabPath,
            shapeType = splitter.GetShapeType(),
            errorDetails = "",
            minMatchRate = 1.0f
        };

        // GridCellを取得（固定位置で元画像を保持）
        var gridCells = splitter.GetComponentsInChildren<GridCell>(true);
        result.totalCells = gridCells.Length;

        if (gridCells.Length == 0)
        {
            result.errorDetails += "GridCellが見つかりません。\n";
            result.hasImageError = true;
            return result;
        }

        // 元画像を取得（AddressableImageLoaderは非同期のため、addressNameから同期取得）
        Sprite sourceSprite = null;
        var loader = splitter.GetComponent<AddressableImageLoader>();
        
        if (loader != null && !string.IsNullOrEmpty(loader.addressName))
        {
            sourceSprite = AssetDatabase.LoadAssetAtPath<Sprite>(loader.addressName);
        }

        if (sourceSprite == null)
        {
            result.errorDetails += "元画像が見つかりません。\n";
            result.hasImageError = true;
            return result;
        }

        Texture2D sourceTex = sourceSprite.texture;
        
        if (sourceTex == null)
        {
            result.errorDetails += "元画像のテクスチャが取得できません。\n";
            result.hasImageError = true;
            return result;
        }
        
        if (!sourceTex.isReadable)
        {
            result.errorDetails += "元画像が読み取り不可です（Read/Write Enabledが無効）。\n";
            result.hasImageError = true;
            return result;
        }

        // 各GridCellの画像を検証
        int checkedCount = 0;
        int mismatchCount = 0;
        int skippedNoLoader = 0;
        int skippedNoSprite = 0;
        int skippedNoTexture = 0;

        foreach (var gridCell in gridCells)
        {
            // セル画像を取得（AddressableImageLoaderは非同期のため、addressNameから同期取得）
            Sprite cellSprite = null;
            var cellLoader = gridCell.GetComponent<AddressableImageLoader>();
            
            if (cellLoader == null || string.IsNullOrEmpty(cellLoader.addressName))
            {
                skippedNoLoader++;
                continue;
            }
            
            cellSprite = AssetDatabase.LoadAssetAtPath<Sprite>(cellLoader.addressName);

            if (cellSprite == null)
            {
                skippedNoSprite++;
                continue;
            }

            // セル画像のテクスチャを取得
            Texture2D cellTex = cellSprite.texture;
            if (cellTex == null || !cellTex.isReadable)
            {
                skippedNoTexture++;
                continue;
            }

            // 形状タイプに応じて元画像の該当範囲を計算
            Rect expectedRect = CalculateExpectedSourceRect(splitter, gridCell, sourceSprite.rect);
            
            if (expectedRect.width <= 0 || expectedRect.height <= 0)
            {
                continue;
            }

            // ピクセル比較
            float matchRate = ComparePixels(sourceTex, expectedRect, cellTex, cellSprite.rect);
            
            checkedCount++;
            result.minMatchRate = Mathf.Min(result.minMatchRate, matchRate);

            if (matchRate < pixelMatchThreshold)
            {
                mismatchCount++;
                result.errorDetails += $"Cell({gridCell.gridX},{gridCell.gridY}): 一致率{matchRate:P1}\n";
            }
        }

        result.checkedCells = checkedCount;
        
        // スキップ理由を記録
        if (skippedNoLoader > 0)
            result.errorDetails += $"AddressableImageLoaderなし: {skippedNoLoader}セル\n";
        if (skippedNoSprite > 0)
            result.errorDetails += $"Sprite取得失敗: {skippedNoSprite}セル\n";
        if (skippedNoTexture > 0)
            result.errorDetails += $"Texture取得失敗/読取不可: {skippedNoTexture}セル\n";

        if (mismatchCount > 0)
        {
            result.hasImageMismatch = true;
        }

        return result;
    }

    // 形状タイプに応じて元画像の該当範囲を計算（各Splitterのロジックから逆算）
    private Rect CalculateExpectedSourceRect(AbstractGridImageSplitter splitter, GridCell gridCell, Rect sourceRect)
    {
        int x = gridCell.gridX;
        int y = gridCell.gridY;
        int cols = splitter.cols;
        int rows = splitter.rows;
        float targetPercent = splitter.targetPercent;

        int fullW = (int)sourceRect.width;
        int fullH = (int)sourceRect.height;

        switch (splitter.GetShapeType())
        {
            case ShapeType.Square:
                return CalculateSquareRect(x, y, cols, rows, targetPercent, sourceRect);
            
            case ShapeType.Triangle:
                return CalculateTriangleRect(x, y, cols, rows, targetPercent, sourceRect, splitter._trimShift);
            
            case ShapeType.Hex:
                return CalculateHexRect(x, y, cols, rows, targetPercent, sourceRect);
            
            default:
                return new Rect(0, 0, 0, 0);
        }
    }

    private Rect CalculateSquareRect(int x, int y, int cols, int rows, float targetPercent, Rect sourceRect)
    {
        int fullW = (int)sourceRect.width;
        int fullH = (int)sourceRect.height;
        
        float targetW = fullW * (targetPercent / 100f);
        float targetH = fullH * (targetPercent / 100f);
        int cellSizeByWidth = Mathf.RoundToInt(targetW / cols);
        int cellSizeByHeight = Mathf.RoundToInt(targetH / rows);
        int cellSize = Mathf.Min(cellSizeByWidth, cellSizeByHeight);
        
        int usedWidth = cellSize * cols;
        int usedHeight = cellSize * rows;
        
        int startX = (int)(sourceRect.x + (fullW - usedWidth) / 2f);
        int startY = (int)(sourceRect.y + (fullH - usedHeight) / 2f);
        
        int px = startX + x * cellSize;
        int py = startY + y * cellSize;
        
        return new Rect(px, py, cellSize, cellSize);
    }

    private Rect CalculateTriangleRect(int x, int y, int cols, int rows, float targetPercent, Rect sourceRect, Vector2 trimShift)
    {
        int fullW = (int)sourceRect.width;
        int fullH = (int)sourceRect.height;

        float targetW = fullW * (targetPercent / 100f);
        float targetH = fullH * (targetPercent / 100f);
        int squareSize = Mathf.RoundToInt(Mathf.Min(targetW, targetH));

        int startX = (int)(sourceRect.x + (fullW - squareSize) / 2f + trimShift.x);
        int startY = (int)(sourceRect.y + (fullH - squareSize) / 2f + trimShift.y);

        float triSize = squareSize / Mathf.Max(rows, cols);
        float triHeight = Mathf.Sqrt(3f) / 2f * triSize;
        
        float usedWidth = (cols - 1) * (triSize / 2f) + triSize;
        float usedHeight = rows * triHeight;
        
        int offsetX = startX + Mathf.RoundToInt((squareSize - usedWidth) / 2f);
        int offsetY = startY + Mathf.RoundToInt((squareSize - usedHeight) / 2f);

        int px = offsetX + Mathf.RoundToInt(x * (triSize / 2f));
        int py = offsetY + Mathf.RoundToInt(y * triHeight);

        int w = Mathf.RoundToInt(triSize);
        int h = Mathf.RoundToInt(triHeight);

        return new Rect(px, py, w, h);
    }

    private Rect CalculateHexRect(int x, int y, int cols, int rows, float targetPercent, Rect sourceRect)
    {
        int fullW = (int)sourceRect.width;
        int fullH = (int)sourceRect.height;

        float targetW = fullW * (targetPercent / 100f);
        float targetH = fullH * (targetPercent / 100f);
        int squareSize = Mathf.RoundToInt(Mathf.Min(targetW, targetH));

        bool hasOddColumn = cols > 1;
        float heightRatio = hasOddColumn ? (rows + 0.5f) : rows;
        int cellSize = Mathf.RoundToInt(squareSize / Mathf.Max(heightRatio, cols));
        
        float radius = cellSize / 2f;
        float hexWidth = 2f * radius;
        float hexHeight = Mathf.Sqrt(3f) * radius;

        int usedWidth = Mathf.RoundToInt((cols - 1) * 1.5f * radius + hexWidth);
        int usedHeight = Mathf.RoundToInt(rows * hexHeight + (hasOddColumn ? hexHeight / 2f : 0));
        
        int startX = (int)(sourceRect.x + (fullW - usedWidth) / 2f);
        int startY = (int)(sourceRect.y + (fullH - usedHeight) / 2f);

        int px = startX + Mathf.RoundToInt(x * 1.5f * radius);
        int py = startY + Mathf.RoundToInt(y * hexHeight + (x % 2 == 1 ? hexHeight / 2f : 0));
        int w = Mathf.RoundToInt(hexWidth);
        int h = Mathf.RoundToInt(hexHeight);
        
        return new Rect(px, py, w, h); 
    }

    // ピクセル比較（一致率を返す）
    private float ComparePixels(Texture2D sourceTex, Rect sourceArea, Texture2D cellTex, Rect cellArea)
    {
        int sourceX = Mathf.RoundToInt(sourceArea.x);
        int sourceY = Mathf.RoundToInt(sourceArea.y);
        int sourceW = Mathf.RoundToInt(sourceArea.width);
        int sourceH = Mathf.RoundToInt(sourceArea.height);

        int cellX = Mathf.RoundToInt(cellArea.x);
        int cellY = Mathf.RoundToInt(cellArea.y);
        int cellW = Mathf.RoundToInt(cellArea.width);
        int cellH = Mathf.RoundToInt(cellArea.height);

        // 範囲チェック
        if (sourceX < 0 || sourceY < 0 || sourceX + sourceW > sourceTex.width || sourceY + sourceH > sourceTex.height)
            return 0f;
        if (cellX < 0 || cellY < 0 || cellX + cellW > cellTex.width || cellY + cellH > cellTex.height)
            return 0f;

        // サイズが異なる場合は一致率0
        if (sourceW != cellW || sourceH != cellH)
            return 0f;

        // ピクセル取得
        Color[] sourcePixels = sourceTex.GetPixels(sourceX, sourceY, sourceW, sourceH);
        Color[] cellPixels = cellTex.GetPixels(cellX, cellY, cellW, cellH);

        if (sourcePixels.Length != cellPixels.Length)
            return 0f;

        // ピクセル比較（アルファ値も考慮）
        int matchCount = 0;
        int totalPixels = sourcePixels.Length;
        float colorThreshold = 0.05f; // 色の許容誤差（RGBA合計）

        for (int i = 0; i < totalPixels; i++)
        {
            Color sc = sourcePixels[i];
            Color cc = cellPixels[i];

            // アルファ値が両方とも0に近い場合は一致とみなす
            if (sc.a < 0.01f && cc.a < 0.01f)
            {
                matchCount++;
                continue;
            }

            // 色とアルファの差を計算
            float diff = Mathf.Abs(sc.r - cc.r) + Mathf.Abs(sc.g - cc.g) + Mathf.Abs(sc.b - cc.b) + Mathf.Abs(sc.a - cc.a);
            
            if (diff < colorThreshold)
            {
                matchCount++;
            }
        }

        return (float)matchCount / totalPixels;
    }

    private void ExportToCSV()
    {
        string path = EditorUtility.SaveFilePanel("CSV出力", "", "alignment_check_results.csv", "csv");
        if (string.IsNullOrEmpty(path)) return;

        using (StreamWriter writer = new StreamWriter(path))
        {
            writer.WriteLine("プレハブ名,形状,セル数,検証数,画像ずれ,画像エラー,最小一致率,詳細");
            
            foreach (var result in results)
            {
                writer.WriteLine($"{result.prefabName},{result.shapeType},{result.totalCells},{result.checkedCells}," +
                    $"{(result.hasImageMismatch ? "あり" : "なし")}," +
                    $"{(result.hasImageError ? "あり" : "なし")}," +
                    $"{result.minMatchRate:P1}," +
                    $"\"{result.errorDetails.Replace("\n", " ")}\"");
            }
        }

        EditorUtility.DisplayDialog("完了", $"CSV出力完了:\n{path}", "OK");
    }
}
