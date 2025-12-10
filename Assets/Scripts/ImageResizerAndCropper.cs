#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

public class ImageResizerAndCropper : EditorWindow
{
    private List<Texture2D> sourceImages = new List<Texture2D>();
    private int targetWidth = 1080;
    private int targetHeight = 1350;
    private string outputFolderPath = "Assets/Textures/Originals/ResizedImages";

    private Vector2 scrollPosition;

    [MenuItem("Tools/Image Resizer & Cropper")]
    public static void ShowWindow()
    {
        GetWindow<ImageResizerAndCropper>("Image Resizer & Cropper");
    }

    void OnGUI()
    {
        // ... (GUI描画部分は変更なし) ...
        
        // スクロールビューの開始
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        GUILayout.Label("Image Resizer & Cropper", EditorStyles.boldLabel);

        // ソース画像リストの表示とD&Dエリア (省略)
        // --- 中略：D&Dエリアとリスト表示 ---
        GUILayout.Space(10);
        EditorGUILayout.LabelField("Source Images (Drag & Drop here):");
        Rect dropArea = GUILayoutUtility.GetRect(0.0f, 100.0f, GUILayout.ExpandWidth(true));
        GUI.Box(dropArea, "Drag Textures Here");

        // ドロップされたオブジェクトの処理 (省略)
        if (dropArea.Contains(Event.current.mousePosition) && Event.current.type == EventType.DragUpdated)
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            Event.current.Use();
        }
        else if (dropArea.Contains(Event.current.mousePosition) && Event.current.type == EventType.DragPerform)
        {
            DragAndDrop.AcceptDrag();
            foreach (Object draggedObject in DragAndDrop.objectReferences)
            {
                if (draggedObject is Texture2D texture)
                {
                    SetTextureReadable(texture, true);
                    sourceImages.Add(texture);
                }
            }
            Event.current.Use();
        }

        // リスト内の画像を表示 (省略)
        if (sourceImages.Count > 0)
        {
            EditorGUILayout.LabelField("Images to process:");
            for (int i = sourceImages.Count - 1; i >= 0; i--)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.ObjectField(sourceImages[i], typeof(Texture2D), false);
                if (GUILayout.Button("Remove", GUILayout.Width(60)))
                {
                    SetTextureReadable(sourceImages[i], false);
                    sourceImages.RemoveAt(i);
                }
                EditorGUILayout.EndHorizontal();
            }
            if (GUILayout.Button("Clear All Images"))
            {
                foreach (var img in sourceImages) SetTextureReadable(img, false);
                sourceImages.Clear();
            }
        }
        else
        {
            EditorGUILayout.HelpBox("No images added. Drag textures onto the box above.", MessageType.Info);
        }

        GUILayout.Space(20);

        // 設定項目
        targetWidth = EditorGUILayout.IntField("Target Width", targetWidth);
        targetHeight = EditorGUILayout.IntField("Target Height", targetHeight);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Output Folder Path:");
        EditorGUILayout.BeginHorizontal();
        outputFolderPath = EditorGUILayout.TextField(outputFolderPath);
        if (GUILayout.Button("Browse", GUILayout.Width(60)))
        {
            string path = EditorUtility.OpenFolderPanel("Select Output Folder", outputFolderPath, "");
            if (!string.IsNullOrEmpty(path))
            {
                outputFolderPath = "Assets" + path.Replace(Application.dataPath, "");
            }
        }
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(20);

        // スクロールビューの終了
        EditorGUILayout.EndScrollView();
        
        // 処理実行ボタン (省略)
        EditorGUI.BeginDisabledGroup(sourceImages.Count == 0 || string.IsNullOrEmpty(outputFolderPath));
        if (GUILayout.Button("Resize and Crop Images", GUILayout.Height(40)))
        {
            ProcessImages();
        }
        EditorGUI.EndDisabledGroup();
    }

    // ProcessImages 関数は変更なし
    void ProcessImages()
    {
        if (!Directory.Exists(outputFolderPath))
        {
            Directory.CreateDirectory(outputFolderPath);
            AssetDatabase.Refresh();
        }

        foreach (Texture2D originalTexture in sourceImages)
        {
            if (originalTexture == null) continue;

            // 新しいロジックのResizeAndCropを呼び出す
            Texture2D finalTexture = ResizeAndCrop(originalTexture, targetWidth, targetHeight);

            if (finalTexture != null)
            {
                // PNGとして保存
                byte[] bytes = finalTexture.EncodeToPNG();
                string filePath = Path.Combine(outputFolderPath, originalTexture.name + "_" + targetWidth + "x" + targetHeight + ".png");
                File.WriteAllBytes(filePath, bytes);

                // ログとメモリ解放
                Debug.Log($"Processed: {originalTexture.name} -> {filePath}");
                DestroyImmediate(finalTexture); // 最終生成したTexture2Dを解放
            }
            // 中間テクスチャはResizeAndCrop内でDestroyされる
        }

        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Image Processing Complete", $"{sourceImages.Count} images processed successfully!", "OK");

        // 処理後、元のテクスチャの読み書き設定を元に戻す
        foreach (var img in sourceImages) SetTextureReadable(img, false);
        sourceImages.Clear();
    }
    
    // SetTextureReadable 関数は変更なし
    private void SetTextureReadable(Texture2D texture, bool readable)
    {
        string path = AssetDatabase.GetAssetPath(texture);
        if (string.IsNullOrEmpty(path)) return;

        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;

        if (importer.isReadable != readable)
        {
            importer.isReadable = readable;
            importer.SaveAndReimport();
        }
    }


    /// <summary>
    /// 画像を目標アスペクト比を維持しつつ目標サイズをカバーするように拡大縮小し、中央をトリミングします。
    /// </summary>
    private Texture2D ResizeAndCrop(Texture2D source, int targetW, int targetH)
    {
        if (!source.isReadable)
        {
            Debug.LogError($"Texture '{source.name}' is not readable.");
            return null;
        }

        // 1. トリミング先のサイズBと比率Bを取得
        float targetAspect = (float)targetW / targetH;

        // 3. 受け取った画像のサイズAと比率Aを取得
        int sourceW = source.width;
        int sourceH = source.height;
        float sourceAspect = (float)sourceW / sourceH;

        // 3-A1. 比率を保ったまま目標サイズをカバーするように拡大縮小する倍率を計算
        // 目標サイズを覆う（カバー）ためには、目標比率と元画像比率の差分が大きい方の倍率を採用する
        float widthRatio = (float)targetW / sourceW;
        float heightRatio = (float)targetH / sourceH;

        // 目標サイズを「満たす(Fill/Cover)」ために、大きい方のスケール倍率を採用
        float resizeScale = Mathf.Max(widthRatio, heightRatio);

        // 新しい（拡大/縮小後の）サイズを計算
        int resizedW = Mathf.RoundToInt(sourceW * resizeScale);
        int resizedH = Mathf.RoundToInt(sourceH * resizeScale);

        // =======================================================
        // ステップ 4: 拡大縮小（リサイズ）の実行 (Graphics.Blitを使用し高品質化)
        // =======================================================
        
        // Render Textureに画像をリサイズして描画
        RenderTexture rt = RenderTexture.GetTemporary(resizedW, resizedH, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(source, rt);
        
        // リサイズされたテクスチャを中間生成
        Texture2D resizedTexture = new Texture2D(resizedW, resizedH, TextureFormat.RGBA32, false);
        RenderTexture.active = rt;
        resizedTexture.ReadPixels(new Rect(0, 0, resizedW, resizedH), 0, 0);
        resizedTexture.Apply();
        
        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt); // Render Textureを解放

        // =======================================================
        // ステップ 5: トリミングの実行 (目標サイズW x Hに中央をクロップ)
        // =======================================================

        int cropX = Mathf.FloorToInt((resizedW - targetW) / 2.0f);
        int cropY = Mathf.FloorToInt((resizedH - targetH) / 2.0f);

        // 3-A2. トリミングして出力
        // GetPixels はテクスチャのピクセルを取得する
        Color[] finalPixels = resizedTexture.GetPixels(cropX, cropY, targetW, targetH);
        
        // 最終的なテクスチャの生成
        Texture2D finalTexture = new Texture2D(targetW, targetH, TextureFormat.RGBA32, false);
        finalTexture.SetPixels(finalPixels);
        finalTexture.Apply();

        // 中間生成したリサイズ済みテクスチャを解放
        DestroyImmediate(resizedTexture); 

        return finalTexture;
    }
}
#endif