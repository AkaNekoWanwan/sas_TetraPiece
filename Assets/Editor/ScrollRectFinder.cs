using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.IO;

/// <summary>
/// プレハブ内のScrollRectオブジェクトを検出するツール
/// </summary>
public class ScrollRectFinder : EditorWindow
{
    private Vector2 scrollPosition;
    private List<FindResult> results = new List<FindResult>();
    private bool isScanning = false;
    private float scanProgress = 0f;
    private string scanStatus = "";
    
    private bool scanSubfolders = true;
    private string targetFolder = "Assets/Prefabs/Stages";
    private bool showOnlyMatched = true;

    [MenuItem("Tools/ScrollRect Finder")]
    public static void ShowWindow()
    {
        GetWindow<ScrollRectFinder>("ScrollRect Finder");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("ScrollRect検出ツール", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // スキャン設定
        EditorGUILayout.LabelField("スキャン設定", EditorStyles.boldLabel);
        targetFolder = EditorGUILayout.TextField("対象フォルダ", targetFolder);
        scanSubfolders = EditorGUILayout.Toggle("サブフォルダを含む", scanSubfolders);
        
        EditorGUILayout.Space();

        // 表示設定
        EditorGUILayout.LabelField("表示設定", EditorStyles.boldLabel);
        showOnlyMatched = EditorGUILayout.Toggle("ScrollRect有りのみ表示", showOnlyMatched);
        
        EditorGUILayout.Space();

        // スキャンボタン
        GUI.enabled = !isScanning;
        if (GUILayout.Button("全プレハブをスキャン", GUILayout.Height(30)))
        {
            ScanAllPrefabs();
        }
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
            EditorGUILayout.LabelField($"スキャン結果: {results.Count}プレハブ", EditorStyles.boldLabel);
            
            int matchedCount = results.Count(r => r.hasScrollRect);
            int totalScrollRects = results.Sum(r => r.scrollRectObjects.Count);
            
            EditorGUILayout.LabelField($"ScrollRect検出: {matchedCount}プレハブ, {totalScrollRects}オブジェクト", EditorStyles.boldLabel);
            
            if (GUILayout.Button("CSVエクスポート"))
            {
                ExportToCSV();
            }
            
            EditorGUILayout.Space();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            foreach (var result in results)
            {
                if (showOnlyMatched && !result.hasScrollRect)
                    continue;

                DrawResultItem(result);
            }
            
            EditorGUILayout.EndScrollView();
        }
    }

    private void DrawResultItem(FindResult result)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        
        // プレハブ名とステータス
        EditorGUILayout.BeginHorizontal();
        
        if (result.hasScrollRect)
        {
            EditorGUILayout.LabelField("🔍", GUILayout.Width(20));
            GUI.color = Color.yellow;
        }
        else
        {
            EditorGUILayout.LabelField("　", GUILayout.Width(20));
            GUI.color = Color.white;
        }
        
        if (GUILayout.Button(result.prefabName, EditorStyles.linkLabel))
        {
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(result.prefabPath);
            EditorGUIUtility.PingObject(Selection.activeObject);
        }
        
        GUI.color = Color.white;
        
        if (result.hasScrollRect)
        {
            EditorGUILayout.LabelField($"({result.scrollRectObjects.Count}件)", GUILayout.Width(60));
        }
        
        EditorGUILayout.EndHorizontal();
        
        // ScrollRectオブジェクトの詳細
        if (result.hasScrollRect)
        {
            EditorGUI.indentLevel++;
            
            foreach (var obj in result.scrollRectObjects)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"  📜 {obj.objectName}", GUILayout.Width(250));
                EditorGUILayout.LabelField($"パス: {obj.hierarchyPath}", EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
                
                if (obj.hasScrollRectComponent)
                {
                    EditorGUILayout.LabelField($"      ⚙️ ScrollRectコンポーネント有り", EditorStyles.miniLabel);
                }
            }
            
            EditorGUI.indentLevel--;
        }
        
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(3);
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
                
                // 検索
                var result = FindScrollRectObjects(prefab, path);
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
            
            // 結果をソート（ScrollRectがあるものを先に）
            results = results.OrderByDescending(r => r.hasScrollRect)
                           .ThenByDescending(r => r.scrollRectObjects.Count)
                           .ThenBy(r => r.prefabName)
                           .ToList();
            
            scanStatus = $"スキャン完了: {results.Count}プレハブ";
            Debug.Log($"[ScrollRectFinder] スキャン完了: {results.Count}プレハブ, ScrollRect検出: {results.Count(r => r.hasScrollRect)}プレハブ");
        }
        finally
        {
            isScanning = false;
            scanProgress = 1f;
            Repaint();
        }
    }

    private FindResult FindScrollRectObjects(GameObject prefab, string path)
    {
        var result = new FindResult
        {
            prefabName = prefab.name,
            prefabPath = path,
            scrollRectObjects = new List<ScrollRectObjectInfo>()
        };
        
        // 名前が "ScrollRect" を含むオブジェクトを検索
        Transform[] allTransforms = prefab.GetComponentsInChildren<Transform>(true);
        
        foreach (var transform in allTransforms)
        {
            // 名前に "ScrollRect" が含まれるか
            if (transform.name.Contains("ScrollRect"))
            {
                string hierarchyPath = GetHierarchyPath(transform);
                bool hasComponent = transform.GetComponent<UnityEngine.UI.ScrollRect>() != null;
                
                result.scrollRectObjects.Add(new ScrollRectObjectInfo
                {
                    objectName = transform.name,
                    hierarchyPath = hierarchyPath,
                    hasScrollRectComponent = hasComponent
                });
            }
        }
        
        result.hasScrollRect = result.scrollRectObjects.Count > 0;
        return result;
    }
    
    private string GetHierarchyPath(Transform transform)
    {
        List<string> path = new List<string>();
        Transform current = transform;
        
        while (current != null)
        {
            path.Insert(0, current.name);
            current = current.parent;
        }
        
        return string.Join("/", path);
    }

    private void ExportToCSV()
    {
        string path = EditorUtility.SaveFilePanel("CSVエクスポート", "", "scrollrect_finder_result.csv", "csv");
        if (string.IsNullOrEmpty(path))
            return;
        
        using (StreamWriter writer = new StreamWriter(path, false, System.Text.Encoding.UTF8))
        {
            // ヘッダー
            writer.WriteLine("Prefab名,Prefabパス,ScrollRect有無,ScrollRect件数,オブジェクト名,階層パス,ScrollRectコンポーネント有無");
            
            foreach (var result in results)
            {
                if (result.scrollRectObjects.Count == 0)
                {
                    writer.WriteLine($"{result.prefabName},{result.prefabPath},無,0,,,");
                }
                else
                {
                    foreach (var obj in result.scrollRectObjects)
                    {
                        writer.WriteLine($"{result.prefabName},{result.prefabPath},有,{result.scrollRectObjects.Count}," +
                                       $"{obj.objectName},\"{obj.hierarchyPath}\",{(obj.hasScrollRectComponent ? "有" : "無")}");
                    }
                }
            }
        }
        
        EditorUtility.DisplayDialog("エクスポート完了", $"CSVファイルを保存しました:\n{path}", "OK");
        EditorUtility.RevealInFinder(path);
    }

    private class FindResult
    {
        public string prefabName;
        public string prefabPath;
        public bool hasScrollRect;
        public List<ScrollRectObjectInfo> scrollRectObjects;
    }

    private class ScrollRectObjectInfo
    {
        public string objectName;
        public string hierarchyPath;
        public bool hasScrollRectComponent;
    }
}
