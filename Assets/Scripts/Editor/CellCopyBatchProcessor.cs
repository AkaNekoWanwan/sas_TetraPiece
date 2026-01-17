#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// シーン上の全「cell_copy」オブジェクトに対して、親のGridImageSplitterのShapeType別に一括処理を実行するツール
/// </summary>
public class CellCopyBatchProcessor : EditorWindow
{
    private int processedCount = 0;
    private Dictionary<ShapeType, int> processedByType = new Dictionary<ShapeType, int>();

    [MenuItem("Tools/Cell Copy Batch Processor")]
    public static void ShowWindow()
    {
        GetWindow<CellCopyBatchProcessor>("Cell Copy Processor");
    }

    private void OnGUI()
    {
        GUILayout.Label("Cell Copy Batch Processor", EditorStyles.boldLabel);
        GUILayout.Space(10);

        GUILayout.Label("シーン上の全「cell_copy」オブジェクトに対して、", EditorStyles.wordWrappedLabel);
        GUILayout.Label("親のGridImageSplitterのShapeType別に処理を実行します。", EditorStyles.wordWrappedLabel);
        GUILayout.Space(10);

        if (GUILayout.Button("全cell_copyを処理", GUILayout.Height(40)))
        {
            ProcessAllCellCopies();
        }

        GUILayout.Space(10);
        
        if (processedCount > 0)
        {
            GUILayout.Label($"処理完了: {processedCount}個のcell_copyを処理しました", EditorStyles.helpBox);
            GUILayout.Space(5);
            
            foreach (var kvp in processedByType)
            {
                GUILayout.Label($"  - {kvp.Key}: {kvp.Value}個", EditorStyles.miniLabel);
            }
        }
    }

    /// <summary>
    /// シーン上の全cell_copyオブジェクトを検索して処理
    /// </summary>
    private void ProcessAllCellCopies()
    {
        processedCount = 0;
        processedByType.Clear();

        // シーン上の全GameObjectを取得（非アクティブも含む）
        GameObject[] allObjects = GameObject.FindObjectsOfType<GameObject>(true);
        
        // cell_copyという名前のオブジェクトをフィルタ
        var cellCopyObjects = allObjects.Where(obj => obj.name == "cell_copy").ToList();

        Debug.Log($"[CellCopyBatchProcessor] {cellCopyObjects.Count}個のcell_copyを発見しました");

        foreach (var cellCopy in cellCopyObjects)
        {
            // 親階層からAbstractGridImageSplitterを探す
            AbstractGridImageSplitter splitter = cellCopy.transform.parent.parent.parent.parent.GetComponentInChildren<AbstractGridImageSplitter>(true);
            
            if (splitter == null)
            {
                Debug.LogWarning($"[CellCopyBatchProcessor] {cellCopy.name} の親にGridImageSplitterが見つかりませんでした", cellCopy);
                continue;
            }

            ShapeType shapeType = splitter.GetShapeType();
            
            // ShapeType別の処理を実行
            ProcessCellCopyByShapeType(cellCopy, shapeType);

            // 統計情報を更新
            processedCount++;
            if (!processedByType.ContainsKey(shapeType))
            {
                processedByType[shapeType] = 0;
            }
            processedByType[shapeType]++;
        }

        Debug.Log($"[CellCopyBatchProcessor] 処理完了: {processedCount}個のcell_copyを処理しました");
        
        // シーンを変更済みとしてマーク
        EditorUtility.SetDirty(cellCopyObjects[0].scene.GetRootGameObjects()[0]);
    }

    /// <summary>
    /// ShapeType別にcell_copyオブジェクトを処理
    /// ★ここに各ShapeTypeごとの具体的な処理を記述してください
    /// </summary>
    private void ProcessCellCopyByShapeType(GameObject cellCopy, ShapeType shapeType)
    {
        switch (shapeType)
        {
            case ShapeType.Square:
                ProcessSquareCellCopy(cellCopy);
                break;
                
            case ShapeType.Triangle:
                ProcessTriangleCellCopy(cellCopy);
                break;
                
            case ShapeType.Hex:
                ProcessHexCellCopy(cellCopy);
                break;
                
            default:
                Debug.LogWarning($"[CellCopyBatchProcessor] 未対応のShapeType: {shapeType}", cellCopy);
                break;
        }
    }

    #region ShapeType別の処理メソッド（カスタマイズ可能）

    /// <summary>
    /// Square（四角形）のcell_copyに対する処理
    /// </summary>
    private void ProcessSquareCellCopy(GameObject cellCopy)
    {
        // ★ここに四角形セル用の処理を記述
        // 例: localScaleの調整
        // cellCopy.transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);

        // 例: Outlineコンポーネントの取得と処理
        Outline outline = cellCopy.GetComponent<Outline>();
        if (outline != null)
        {
            // outline.effectDistance = new Vector2(2f, -2f);
            // outline.effectColor = Color.black;
        }

        Debug.Log($"[Square] {cellCopy.name} を処理しました", cellCopy);
    }

    /// <summary>
    /// Triangle（三角形）のcell_copyに対する処理
    /// </summary>
    private void ProcessTriangleCellCopy(GameObject cellCopy)
    {
        // ★ここに三角形セル用の処理を記述
        // 例: localScaleの調整
        // cellCopy.transform.localScale = new Vector3(1.1f, 1.1f, 1.0f);

        // 例: Outlineコンポーネントの取得と処理
        Outline outline = cellCopy.GetComponent<Outline>();
        if (outline != null)
        {
            // outline.effectDistance = new Vector2(3f, -3f);
            // outline.effectColor = new Color(0.2f, 0.2f, 0.2f);
        }

        Debug.Log($"[Triangle] {cellCopy.name} を処理しました", cellCopy);
    }

    /// <summary>
    /// Hex（六角形）のcell_copyに対する処理
    /// </summary>
    private void ProcessHexCellCopy(GameObject cellCopy)
    {
        // ★ここに六角形セル用の処理を記述
        // 例: localScaleの調整
        float scaleFactor = 1.01f;
        cellCopy.transform.localScale = new Vector3(scaleFactor, scaleFactor, 1.0f);
        RectTransform rect = cellCopy.GetComponent<RectTransform>();
        RectTransform parentRect = cellCopy.transform.parent.GetComponent<RectTransform>();

        float addSize = 3.5f;
        rect.sizeDelta = new Vector2(parentRect.sizeDelta.x + addSize, parentRect.sizeDelta.y + addSize);

        // 例: Outlineコンポーネントの取得と処理
        List<UnityEngine.UI.Outline> outlines = new List<UnityEngine.UI.Outline>(cellCopy.GetComponents<UnityEngine.UI.Outline>());
        foreach (UnityEngine.UI.Outline outline in outlines)
        {
            outline.enabled = false;
            // outlineを消す
            DestroyImmediate(outline);
            // outline.effectDistance = new Vector2(2.5f, -2.5f);
            // outline.effectColor = new Color(0.1f, 0.1f, 0.1f);
        }

        Debug.Log($"[Hex] {cellCopy.name} を処理しました", cellCopy);
    }

    #endregion
}
#endif