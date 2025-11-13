using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public class MeshSaveEditor : EditorWindow
{
    private string meshSavePath = "";
    private string nameOfCreateMesh = "DefaultMesh";


    [MenuItem("Tools/MeshSave")]
    public static void ShowWindow()
    {
        GetWindow<MeshSaveEditor>("Mesh Saver");
    }

    private void OnSelectionChange()
    {
        // オブジェクト選択が変更されたときに更新
        nameOfCreateMesh = Selection.activeGameObject != null ? "Cut__" + Selection.activeGameObject.name : "DefaultMesh";
        Repaint(); // 再描画をリクエスト
    }

    private void OnGUI()
    {
        GUILayout.Label("Mesh Saver Settings", EditorStyles.boldLabel);

        EditorGUILayout.Toggle("ダミーにゃんにゃん", true);
        meshSavePath = EditorGUILayout.TextField("保存パス", "Assets/Models");
        nameOfCreateMesh = EditorGUILayout.TextField("メッシュ名", "Cut__" + nameOfCreateMesh);

        if (GUILayout.Button("Combine Meshes"))
        {
            SaveMeshes();
        }
    }

    private void SaveMeshes()
    {
        // 1. 選択されているオブジェクトを取得
        GameObject targetObject = Selection.activeGameObject;
        if (targetObject == null)
        {
            Debug.LogError("No target object selected.");
            return;
        }
        Transform targetTransform = targetObject.transform;
        List<MeshFilter> meshFilterList = new List<MeshFilter>();
        
        meshFilterList.AddRange(targetObject.GetComponentsInChildren<MeshFilter>());

        MeshFilter[] meshFilters = meshFilterList.ToArray();

        // 2. メッシュを収集し、保存
        for (int i = 0; i < meshFilters.Length; i++)
        {
            Mesh savedMesh = meshFilters[i].mesh;
            AssetSaver.SaveUniqueAsset(savedMesh, meshSavePath, nameOfCreateMesh);
        }
    }
}
