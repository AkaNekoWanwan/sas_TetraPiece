using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using System.IO;

public class GridCellBatchUpdater : EditorWindow
{
    private Vector2 scrollPosition;
    private int processedPrefabCount = 0;
    private int updatedCellCount = 0;

    [MenuItem("Tools/GridCell Batch Updater")]
    public static void ShowWindow()
    {
        GetWindow<GridCellBatchUpdater>("GridCell Batch Updater");
    }

    private void OnGUI()
    {
        GUILayout.Label("GridCell Batch Updater", EditorStyles.boldLabel);
        GUILayout.Space(10);

        EditorGUILayout.HelpBox(
            "このツールは、プロジェクト内の全プレハブに含まれるGridCellコンポーネントに対して、" +
            "OnValidateと同じ更新処理を適用し、プレハブを保存します。",
            MessageType.Info
        );

        GUILayout.Space(10);

        if (GUILayout.Button("全プレハブのGridCellを更新", GUILayout.Height(40)))
        {
            UpdateAllPrefabs();
        }

        GUILayout.Space(10);

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        EditorGUILayout.LabelField($"処理済みプレハブ数: {processedPrefabCount}");
        EditorGUILayout.LabelField($"更新されたGridCell数: {updatedCellCount}");
        EditorGUILayout.EndScrollView();
    }

    private void UpdateAllPrefabs()
    {
        processedPrefabCount = 0;
        updatedCellCount = 0;

        // プロジェクト内の全プレハブを検索
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        int totalPrefabs = prefabGuids.Length;

        try
        {
            for (int i = 0; i < totalPrefabs; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);

                if (EditorUtility.DisplayCancelableProgressBar(
                    "GridCell更新中",
                    $"処理中: {path} ({i + 1}/{totalPrefabs})",
                    (float)i / totalPrefabs))
                {
                    Debug.Log("処理がキャンセルされました");
                    break;
                }

                UpdatePrefabGridCells(path);
            }

            EditorUtility.ClearProgressBar();
            
            Debug.Log($"<color=green>GridCell更新完了: {processedPrefabCount}個のプレハブ、{updatedCellCount}個のGridCellを更新しました</color>");
            EditorUtility.DisplayDialog(
                "完了",
                $"{processedPrefabCount}個のプレハブ内の{updatedCellCount}個のGridCellを更新しました",
                "OK"
            );
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        Repaint();
    }

    private void UpdatePrefabGridCells(string prefabPath)
    {
        // プレハブをロード
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
        if (prefabRoot == null)
        {
            return;
        }

        try
        {
            // プレハブ内の全GridCellコンポーネントを取得
            GridCell[] gridCells = prefabRoot.GetComponentsInChildren<GridCell>(true);
            
            if (gridCells.Length == 0)
            {
                return;
            }

            bool prefabModified = false;
            int cellsUpdatedInThisPrefab = 0;

            foreach (var gridCell in gridCells)
            {
                if (ApplyGridCellUpdate(gridCell))
                {
                    prefabModified = true;
                    cellsUpdatedInThisPrefab++;
                }
            }

            if (prefabModified)
            {
                // プレハブを保存
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                processedPrefabCount++;
                updatedCellCount += cellsUpdatedInThisPrefab;
                
                Debug.Log($"<color=cyan>更新: {prefabPath} - {cellsUpdatedInThisPrefab}個のGridCellを更新</color>");
            }
        }
        finally
        {
            // プレハブをアンロード
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private bool ApplyGridCellUpdate(GridCell gridCell)
    {
        if (gridCell == null)
        {
            return false;
        }

        bool modified = false;

        // AbstractGridImageSplitterを取得
        AbstractGridImageSplitter spritter = gridCell.GetComponentInParent<AbstractGridImageSplitter>(true);
        if (spritter == null)
        {
            // Debug.LogWarning($"GridCell更新スキップ（Spritterなし）: {GetFullPath(gridCell.transform)}");
            return false;
        }

        SpritterParam param = spritter._param;
        if (param == null)
        {
            Debug.LogWarning($"GridCell更新スキップ（Paramなし）: {GetFullPath(gridCell.transform)}");
            return false;
        }

        // Outlineコンポーネントのリストを取得
        List<UnityEngine.UI.Outline> outLines = gridCell.GetComponents<UnityEngine.UI.Outline>().ToList();

        // Imageコンポーネントのcolorを設定
        Image img = gridCell.GetComponent<Image>();
        if (img != null && img.color != param.AnswerColor)
        {
            img.color = param.AnswerColor;
            modified = true;
        }

        // Outlineが複数ある場合は1つに減らす
        if (outLines.Count > 1)
        {
            for (int i = outLines.Count - 1; i >= 1; i--)
            {
                Object.DestroyImmediate(outLines[i]);
                outLines.RemoveAt(i);
                modified = true;
            }
        }

        // Outlineの設定を更新
        if (outLines.Count > 0)
        {
            var outline = outLines[0];
            Vector2 targetDistance = Vector2.one * 1f;
            
            if (outline.effectDistance != targetDistance)
            {
                outline.effectDistance = targetDistance;
                modified = true;
            }

            if (outline.effectColor != param.OutLineColor)
            {
                outline.effectColor = param.OutLineColor;
                modified = true;
            }
        }

        return modified;
    }

    private string GetFullPath(Transform transform)
    {
        string path = transform.name;
        Transform current = transform.parent;
        
        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }
        
        return path;
    }
}
