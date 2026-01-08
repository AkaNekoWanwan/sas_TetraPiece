using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// 既存のGridImageSplitter継承クラスを新しいGridImageSplitter(ShapeTypeパラメータ版)に移行するツール
/// </summary>
public class SplitterMigrationTool : EditorWindow
{
    private Vector2 scrollPosition;
    private int convertedCount = 0;
    private int skippedCount = 0;
    private int errorCount = 0;

    [MenuItem("Tools/Splitter Migration Tool")]
    public static void ShowWindow()
    {
        GetWindow<SplitterMigrationTool>("Splitter Migration");
    }

    private void OnGUI()
    {
        GUILayout.Label("Grid Image Splitter 移行ツール", EditorStyles.boldLabel);
        GUILayout.Space(10);

        EditorGUILayout.HelpBox(
            "このツールは、プロジェクト内の全プレハブを検索し、\n" +
            "GridImageSplitterTriangle/HexコンポーネントをGridImageSplitterに変換します。\n" +
            "変換後も従来の機能は完全に保持されます。",
            MessageType.Info
        );

        GUILayout.Space(10);

        if (GUILayout.Button("全プレハブを検索して移行", GUILayout.Height(40)))
        {
            MigrateAllPrefabs();
        }

        GUILayout.Space(10);

        if (GUILayout.Button("シーン内のオブジェクトを移行", GUILayout.Height(40)))
        {
            MigrateSceneObjects();
        }

        GUILayout.Space(10);

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        EditorGUILayout.LabelField($"変換済み: {convertedCount}");
        EditorGUILayout.LabelField($"スキップ: {skippedCount}");
        EditorGUILayout.LabelField($"エラー: {errorCount}");
        EditorGUILayout.EndScrollView();
    }

    private void MigrateAllPrefabs()
    {
        convertedCount = 0;
        skippedCount = 0;
        errorCount = 0;

        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        int total = prefabGuids.Length;

        try
        {
            for (int i = 0; i < total; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);

                if (EditorUtility.DisplayCancelableProgressBar(
                    "Splitter移行中",
                    $"処理中: {path} ({i + 1}/{total})",
                    (float)i / total))
                {
                    Debug.Log("処理がキャンセルされました");
                    break;
                }

                MigratePrefab(path);
            }

            EditorUtility.ClearProgressBar();
            
            Debug.Log($"<color=green>移行完了: {convertedCount}個変換、{skippedCount}個スキップ、{errorCount}個エラー</color>");
            EditorUtility.DisplayDialog(
                "完了",
                $"変換: {convertedCount}\nスキップ: {skippedCount}\nエラー: {errorCount}",
                "OK"
            );
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        Repaint();
    }

    private void MigrateSceneObjects()
    {
        convertedCount = 0;
        skippedCount = 0;
        errorCount = 0;

        // シーン内の全AbstractGridImageSplitterを取得
        var splitters = FindObjectsOfType<AbstractGridImageSplitter>(true);

        foreach (var splitter in splitters)
        {
            MigrateComponent(splitter.gameObject);
        }

        Debug.Log($"<color=green>シーン内移行完了: {convertedCount}個変換、{skippedCount}個スキップ、{errorCount}個エラー</color>");
        EditorUtility.DisplayDialog("完了", $"変換: {convertedCount}\nスキップ: {skippedCount}\nエラー: {errorCount}", "OK");
        
        Repaint();
    }

    private void MigratePrefab(string prefabPath)
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
        if (prefabRoot == null) return;

        try
        {
            var splitters = prefabRoot.GetComponentsInChildren<AbstractGridImageSplitter>(true);
            bool modified = false;

            foreach (var splitter in splitters)
            {
                if (MigrateComponent(splitter.gameObject))
                {
                    modified = true;
                }
            }

            if (modified)
            {
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                Debug.Log($"<color=cyan>移行完了: {prefabPath}</color>");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private bool MigrateComponent(GameObject targetObject)
    {
        var component = targetObject.GetComponent<AbstractGridImageSplitter>();
        if (component == null)
        {
            return false;
        }

        // 既にGridImageSplitter(基底クラス)の場合はスキップ
        System.Type componentType = component.GetType();
        if (componentType == typeof(GridImageSplitter))
        {
            skippedCount++;
            return false;
        }

        try
        {
            // ShapeTypeを判定
            ShapeType shapeType = ShapeType.Square;
            if (componentType.Name == "GridImageSplitterTriangle")
            {
                shapeType = ShapeType.Triangle;
            }
            else if (componentType.Name == "GridImageSplitterHex")
            {
                shapeType = ShapeType.Hex;
            }
            else
            {
                // 不明な型の場合もスキップ
                skippedCount++;
                return false;
            }

            // 既存のコンポーネントのデータをコピー
            var serializedObject = new SerializedObject(component);
            
            // 新しいGridImageSplitterコンポーネントを追加
            var newComponent = targetObject.AddComponent<GridImageSplitter>();
            
            // SerializedPropertyを使用して全フィールドをコピー
            CopySerializedFields(serializedObject, new SerializedObject(newComponent));
            
            // ShapeTypeを設定
            SerializedObject newSerialized = new SerializedObject(newComponent);
            SerializedProperty shapeTypeProp = newSerialized.FindProperty("_shapeType");
            if (shapeTypeProp != null)
            {
                shapeTypeProp.enumValueIndex = (int)shapeType;
                newSerialized.ApplyModifiedProperties();
            }
            
            // 古いコンポーネントを削除
            Object.DestroyImmediate(component);

            convertedCount++;
            Debug.Log($"<color=green>変換成功: {targetObject.name} ({componentType.Name} → GridImageSplitter[{shapeType}])</color>");
            return true;
        }
        catch (System.Exception e)
        {
            errorCount++;
            Debug.LogError($"<color=red>変換エラー: {targetObject.name} - {e.Message}</color>");
            return false;
        }
    }

    private void CopySerializedFields(SerializedObject source, SerializedObject destination)
    {
        SerializedProperty iterator = source.GetIterator();
        bool enterChildren = true;
        
        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;
            
            // m_Scriptプロパティはスキップ（コンポーネントの型情報）
            if (iterator.propertyPath == "m_Script") continue;
            
            SerializedProperty destProp = destination.FindProperty(iterator.propertyPath);
            if (destProp != null && destProp.propertyType == iterator.propertyType)
            {
                destination.CopyFromSerializedProperty(iterator);
            }
        }
        
        destination.ApplyModifiedProperties();
    }
}
