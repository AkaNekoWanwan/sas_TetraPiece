using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Linq;

public class GridCellIsUpSideUpdater : EditorWindow
{
    private Vector2 scrollPos;
    private List<UpdateResult> results = new List<UpdateResult>();
    private bool isProcessing = false;

    private class UpdateResult
    {
        public string splitterName;
        public int updatedCount;
        public List<string> details = new List<string>();
    }

    [MenuItem("Tools/Grid Cell IsUpSide Updater")]
    static void ShowWindow()
    {
        var window = GetWindow<GridCellIsUpSideUpdater>("GridCell IsUpSide Updater");
        window.Show();
    }

    void OnGUI()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Triangle GridCell IsUpSide Updater", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "全てのTriangle GridImageSplitter配下のGridCellの'isUpSide'を更新します。\n" +
            "ルール: (x + y) % 2 == 0 の場合に isUpSide = true",
            MessageType.Info
        );

        EditorGUILayout.Space();

        GUI.enabled = !isProcessing;
        if (GUILayout.Button("現在のシーンで更新", GUILayout.Height(40)))
        {
            UpdateCurrentScene();
        }
        GUI.enabled = true;

        EditorGUILayout.Space();

        if (results.Count > 0)
        {
            EditorGUILayout.LabelField($"更新結果: {results.Count}件のSplitter", EditorStyles.boldLabel);
            
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            
            int totalUpdated = 0;
            foreach (var result in results)
            {
                totalUpdated += result.updatedCount;
                
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"✓ {result.splitterName}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"   更新数: {result.updatedCount}個のGridCell");
                
                if (result.details.Count > 0)
                {
                    EditorGUI.indentLevel++;
                    foreach (var detail in result.details.Take(5))
                    {
                        EditorGUILayout.LabelField(detail, EditorStyles.miniLabel);
                    }
                    if (result.details.Count > 5)
                    {
                        EditorGUILayout.LabelField($"   ... 他 {result.details.Count - 5}件", EditorStyles.miniLabel);
                    }
                    EditorGUI.indentLevel--;
                }
                
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }
            
            EditorGUILayout.EndScrollView();
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"合計: {totalUpdated}個のGridCellを更新しました", EditorStyles.boldLabel);
        }
    }

    void UpdateCurrentScene()
    {
        isProcessing = true;
        results.Clear();

        try
        {
            var scene = SceneManager.GetActiveScene();
            var allSplitters = Resources.FindObjectsOfTypeAll<AbstractGridImageSplitter>()
                .Where(s => s.gameObject.scene == scene)
                .ToList();

            Debug.Log($"🔍 シーン内のGridImageSplitter数: {allSplitters.Count}");

            foreach (var splitter in allSplitters)
            {
                if (splitter.GetShapeType() == ShapeType.Triangle)
                {
                    var result = UpdateSplitter(splitter);
                    if (result.updatedCount > 0)
                    {
                        results.Add(result);
                    }
                }
            }

            results = results.OrderBy(r => r.splitterName).ToList();

            if (results.Count > 0)
            {
                EditorUtility.DisplayDialog(
                    "更新完了",
                    $"{results.Count}個のTriangle Splitterで\n" +
                    $"合計 {results.Sum(r => r.updatedCount)}個のGridCellを更新しました。",
                    "OK"
                );
            }
            else
            {
                EditorUtility.DisplayDialog(
                    "更新対象なし",
                    "Triangle GridImageSplitterが見つかりませんでした。",
                    "OK"
                );
            }
        }
        finally
        {
            isProcessing = false;
        }

        Repaint();
    }

    UpdateResult UpdateSplitter(AbstractGridImageSplitter splitter)
    {
        var result = new UpdateResult
        {
            splitterName = splitter.gameObject.name
        };

        // Splitter配下の全てのGridCellを取得
        var gridCells = splitter.GetComponentsInChildren<GridCell>(true);
        
        Debug.Log($"📋 {splitter.gameObject.name}: {gridCells.Length}個のGridCell");

        int updatedCount = 0;

        foreach (var gridCell in gridCells)
        {
            int x = gridCell.gridX;
            int y = gridCell.gridY;
            bool expectedIsUpSide = ((x + y) % 2 == 0);

            // 現在の値と期待値が異なる場合のみ更新
            if (gridCell.isUpSide != expectedIsUpSide)
            {
                // SerializedObjectを使って変更を記録
                Undo.RecordObject(gridCell, "Update GridCell IsUpSide");
                gridCell.isUpSide = expectedIsUpSide;
                EditorUtility.SetDirty(gridCell);

                result.details.Add($"   ({x},{y}): {!expectedIsUpSide} → {expectedIsUpSide}");
                updatedCount++;
            }
        }

        result.updatedCount = updatedCount;

        if (updatedCount > 0)
        {
            Debug.Log($"✅ {splitter.gameObject.name}: {updatedCount}個のGridCellを更新");
        }

        return result;
    }
}
