using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Linq;
using System.IO;

/// <summary>
/// プレハブ内のセル座標異常を検出するツール
/// </summary>
public class CellPositionValidator : EditorWindow
{
    private Vector2 scrollPosition;
    private List<ValidationResult> results = new List<ValidationResult>();
    private bool isScanning = false;
    private bool isFixing = false;
    private float scanProgress = 0f;
    private string scanStatus = "";
    
    // 検出条件
    private float abnormalThreshold = 1000f; // この値を超えるlocalPositionは異常とみなす
    private float normalCellSize = 200f; // 通常のセルサイズの上限（ピクセル）
    
    private bool showOnlyAbnormal = true;
    private bool scanSubfolders = true;
    private string targetFolder = "Assets/Prefabs/Stages";

    [MenuItem("Tools/Cell Position Validator")]
    public static void ShowWindow()
    {
        GetWindow<CellPositionValidator>("Cell Position Validator");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("セル座標異常検出ツール", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // 検出設定
        EditorGUILayout.LabelField("検出設定", EditorStyles.boldLabel);
        abnormalThreshold = EditorGUILayout.FloatField("異常座標の閾値", abnormalThreshold);
        normalCellSize = EditorGUILayout.FloatField("通常セルサイズ上限", normalCellSize);
        EditorGUILayout.HelpBox($"localPositionの絶対値が {abnormalThreshold} を超える、またはセルサイズが {normalCellSize} を超えるものを検出します", MessageType.Info);
        
        EditorGUILayout.Space();
        
        // スキャン設定
        EditorGUILayout.LabelField("スキャン設定", EditorStyles.boldLabel);
        targetFolder = EditorGUILayout.TextField("対象フォルダ", targetFolder);
        scanSubfolders = EditorGUILayout.Toggle("サブフォルダを含む", scanSubfolders);
        
        EditorGUILayout.Space();

        // 表示設定
        EditorGUILayout.LabelField("表示設定", EditorStyles.boldLabel);
        showOnlyAbnormal = EditorGUILayout.Toggle("異常のみ表示", showOnlyAbnormal);
        
        EditorGUILayout.Space();

        // スキャンボタン
        GUI.enabled = !isScanning && !isFixing;
        if (GUILayout.Button("全プレハブをスキャン", GUILayout.Height(30)))
        {
            ScanAllPrefabs();
        }
        GUI.enabled = true;
        
        // 一括修正ボタン
        EditorGUILayout.Space();
        int abnormalCount = results.Count(r => r.hasAbnormalCells);
        GUI.enabled = !isScanning && !isFixing && abnormalCount > 0;
        GUI.backgroundColor = Color.yellow;
        if (GUILayout.Button($"異常ステージを一括修正 ({abnormalCount}件)", GUILayout.Height(40)))
        {
            if (EditorUtility.DisplayDialog("一括修正確認", 
                $"{abnormalCount}件の異常ステージを一括で「セル生成＋ピース配置」処理します。\n\n" +
                "この処理には時間がかかる場合があります。\n続行しますか？", 
                "実行", "キャンセル"))
            {
                FixAbnormalStages();
            }
        }
        GUI.backgroundColor = Color.white;
        GUI.enabled = true;

        // 進行状況
        if (isScanning)
        {
            EditorGUILayout.Space();
            EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(), scanProgress, scanStatus);
        }

        EditorGUILayout.Space();

        // 結果表示
        if (results.Count > 0)
        {
            EditorGUILayout.LabelField($"スキャン結果: {results.Count}件", EditorStyles.boldLabel);
            
            int totalAbnormalCells = results.Sum(r => r.abnormalCells.Count);
            
            EditorGUILayout.LabelField($"異常検出: {abnormalCount}プレハブ, {totalAbnormalCells}セル", EditorStyles.boldLabel);
            
            if (GUILayout.Button("CSVエクスポート"))
            {
                ExportToCSV();
            }
            
            EditorGUILayout.Space();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            foreach (var result in results)
            {
                if (showOnlyAbnormal && !result.hasAbnormalCells)
                    continue;

                DrawResultItem(result);
            }
            
            EditorGUILayout.EndScrollView();
        }
    }

    private void DrawResultItem(ValidationResult result)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        
        // プレハブ名とステータス
        EditorGUILayout.BeginHorizontal();
        
        GUIStyle labelStyle = new GUIStyle(EditorStyles.label);
        if (result.hasAbnormalCells)
        {
            labelStyle.normal.textColor = Color.red;
            EditorGUILayout.LabelField("⚠️", GUILayout.Width(20));
        }
        else
        {
            labelStyle.normal.textColor = Color.green;
            EditorGUILayout.LabelField("✓", GUILayout.Width(20));
        }
        
        if (GUILayout.Button(result.prefabName, EditorStyles.linkLabel))
        {
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(result.prefabPath);
            EditorGUIUtility.PingObject(Selection.activeObject);
        }
        
        EditorGUILayout.LabelField($"({result.totalCells}セル)", GUILayout.Width(80));
        
        EditorGUILayout.EndHorizontal();
        
        // 異常セルの詳細
        if (result.hasAbnormalCells)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField($"異常セル数: {result.abnormalCells.Count}", EditorStyles.boldLabel);
            
            foreach (var cell in result.abnormalCells)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"  Grid({cell.gridX}, {cell.gridY})", GUILayout.Width(100));
                EditorGUILayout.LabelField($"LocalPos: ({cell.localPosX:F2}, {cell.localPosY:F2})", GUILayout.Width(200));
                
                if (Mathf.Abs(cell.localPosX) > abnormalThreshold || Mathf.Abs(cell.localPosY) > abnormalThreshold)
                {
                    EditorGUILayout.LabelField("🔴 異常座標", GUILayout.Width(100));
                }
                
                if (cell.sizeDeltaX > normalCellSize || cell.sizeDeltaY > normalCellSize)
                {
                    EditorGUILayout.LabelField($"🔴 異常サイズ: ({cell.sizeDeltaX:F0}, {cell.sizeDeltaY:F0})", GUILayout.Width(150));
                }
                
                EditorGUILayout.EndHorizontal();
            }
            EditorGUI.indentLevel--;
        }
        
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(5);
    }

    private void ScanAllPrefabs()
    {
        results.Clear();
        isScanning = true;
        scanProgress = 0f;
        
        try
        {
            // プレハブファイルを検索
            string searchPattern = scanSubfolders ? "t:Prefab" : "";
            string[] guids = AssetDatabase.FindAssets(searchPattern, new[] { targetFolder });
            
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                scanProgress = (float)i / guids.Length;
                scanStatus = $"スキャン中... {i + 1}/{guids.Length}";
                
                // プレハブをロード
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;
                
                // 検証
                var result = ValidatePrefab(prefab, path);
                if (result != null)
                {
                    results.Add(result);
                }
                
                // UI更新
                if (i % 10 == 0)
                {
                    Repaint();
                }
            }
            
            // 結果をソート（異常があるものを先に）
            results = results.OrderByDescending(r => r.hasAbnormalCells)
                           .ThenByDescending(r => r.abnormalCells.Count)
                           .ToList();
            
            scanStatus = $"スキャン完了: {results.Count}プレハブ";
            Debug.Log($"[CellPositionValidator] スキャン完了: {results.Count}プレハブ, 異常検出: {results.Count(r => r.hasAbnormalCells)}プレハブ");
        }
        finally
        {
            isScanning = false;
            scanProgress = 1f;
            Repaint();
        }
    }

    private ValidationResult ValidatePrefab(GameObject prefab, string path)
    {
        var answerGridPoses = prefab.GetComponentsInChildren<AnswerGridPos>(true);
        if (answerGridPoses.Length == 0)
            return null;
        
        var result = new ValidationResult
        {
            prefabName = prefab.name,
            prefabPath = path,
            totalCells = answerGridPoses.Length,
            abnormalCells = new List<AbnormalCellInfo>()
        };
        
        foreach (var cell in answerGridPoses)
        {
            var rectTransform = cell.GetComponent<RectTransform>();
            if (rectTransform == null) continue;
            
            Vector3 localPos = rectTransform.localPosition;
            Vector2 sizeDelta = rectTransform.sizeDelta;
            
            // 異常判定
            bool isAbnormal = false;
            
            // 座標が異常に大きい
            if (Mathf.Abs(localPos.x) > abnormalThreshold || Mathf.Abs(localPos.y) > abnormalThreshold)
            {
                isAbnormal = true;
            }
            
            // サイズが異常に大きい
            if (sizeDelta.x > normalCellSize || sizeDelta.y > normalCellSize)
            {
                isAbnormal = true;
            }
            
            if (isAbnormal)
            {
                result.abnormalCells.Add(new AbnormalCellInfo
                {
                    cellName = cell.gameObject.name,
                    gridX = cell.x,
                    gridY = cell.y,
                    localPosX = localPos.x,
                    localPosY = localPos.y,
                    localPosZ = localPos.z,
                    sizeDeltaX = sizeDelta.x,
                    sizeDeltaY = sizeDelta.y
                });
            }
        }
        
        result.hasAbnormalCells = result.abnormalCells.Count > 0;
        return result;
    }

    private void ExportToCSV()
    {
        string path = EditorUtility.SaveFilePanel("CSVエクスポート", "", "cell_validation_result.csv", "csv");
        if (string.IsNullOrEmpty(path))
            return;
        
        using (StreamWriter writer = new StreamWriter(path, false, System.Text.Encoding.UTF8))
        {
            // ヘッダー
            writer.WriteLine("Prefab名,Prefabパス,総セル数,異常セル数,セル名,GridX,GridY,LocalPosX,LocalPosY,LocalPosZ,SizeDeltaX,SizeDeltaY");
            
            foreach (var result in results)
            {
                if (result.abnormalCells.Count == 0)
                {
                    writer.WriteLine($"{result.prefabName},{result.prefabPath},{result.totalCells},0,,,,,,,");
                }
                else
                {
                    foreach (var cell in result.abnormalCells)
                    {
                        writer.WriteLine($"{result.prefabName},{result.prefabPath},{result.totalCells},{result.abnormalCells.Count}," +
                                       $"{cell.cellName},{cell.gridX},{cell.gridY}," +
                                       $"{cell.localPosX:F2},{cell.localPosY:F2},{cell.localPosZ:F2}," +
                                       $"{cell.sizeDeltaX:F2},{cell.sizeDeltaY:F2}");
                    }
                }
            }
        }
        
        EditorUtility.DisplayDialog("エクスポート完了", $"CSVファイルを保存しました:\n{path}", "OK");
        EditorUtility.RevealInFinder(path);
    }

    private class ValidationResult
    {
        public string prefabName;
        public string prefabPath;
        public int totalCells;
        public bool hasAbnormalCells;
        public List<AbnormalCellInfo> abnormalCells;
    }

    private class AbnormalCellInfo
    {
        public string cellName;
        public int gridX;
        public int gridY;
        public float localPosX;
        public float localPosY;
        public float localPosZ;
        public float sizeDeltaX;
        public float sizeDeltaY;
    }
    
    /// <summary>
    /// 異常ステージを一括で修正する
    /// </summary>
    private void FixAbnormalStages()
    {
        isFixing = true;
        scanProgress = 0f;
        
        // 異常があるステージのリスト
        var abnormalStages = results.Where(r => r.hasAbnormalCells).ToList();
        int totalCount = abnormalStages.Count;
        int successCount = 0;
        int errorCount = 0;
        
        // 現在のシーンを保存するか確認
        if (EditorSceneManager.GetActiveScene().isDirty)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                isFixing = false;
                return;
            }
        }
        
        // 一時シーンを作成
        Scene tempScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        
        try
        {
            for (int i = 0; i < totalCount; i++)
            {
                var stage = abnormalStages[i];
                scanProgress = (float)i / totalCount;
                scanStatus = $"修正中... {i + 1}/{totalCount} - {stage.prefabName}";
                Repaint();
                
                try
                {
                    // プレハブをシーンにインスタンス化
                    GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(stage.prefabPath);
                    GameObject instance = PrefabUtility.InstantiatePrefab(prefabAsset) as GameObject;
                    
                    if (instance == null)
                    {
                        Debug.LogError($"[CellPositionValidator] プレハブのインスタンス化に失敗: {stage.prefabName}");
                        errorCount++;
                        continue;
                    }
                    
                    // StageInfoコンポーネントを取得
                    StageInfo stageInfo = instance.GetComponent<StageInfo>();
                    if (stageInfo == null)
                    {
                        Debug.LogError($"[CellPositionValidator] StageInfoコンポーネントが見つかりません: {stage.prefabName}");
                        DestroyImmediate(instance);
                        errorCount++;
                        continue;
                    }
                    
                    // セル生成＋ピース配置を実行
                    Debug.Log($"[CellPositionValidator] 修正開始: {stage.prefabName}");
                    stageInfo.SetUpStage(false);
                    
                    // プレハブに変更を適用
                    PrefabUtility.ApplyPrefabInstance(instance, InteractionMode.AutomatedAction);
                    
                    // インスタンスを削除
                    DestroyImmediate(instance);
                    
                    successCount++;
                    Debug.Log($"[CellPositionValidator] 修正完了: {stage.prefabName}");
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[CellPositionValidator] 修正エラー: {stage.prefabName}\n{ex.Message}");
                    errorCount++;
                }
            }
            
            // 再スキャンして結果を更新
            scanStatus = "再スキャン中...";
            Repaint();
            ScanAllPrefabs();
            
            // 結果を表示
            string message = $"一括修正が完了しました。\n\n" +
                           $"成功: {successCount}件\n" +
                           $"エラー: {errorCount}件";
            EditorUtility.DisplayDialog("一括修正完了", message, "OK");
            
            Debug.Log($"[CellPositionValidator] 一括修正完了 - 成功: {successCount}, エラー: {errorCount}");
        }
        finally
        {
            isFixing = false;
            scanProgress = 1f;
            scanStatus = "完了";
            Repaint();
        }
    }
}
