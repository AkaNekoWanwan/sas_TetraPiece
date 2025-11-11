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

    // スクロール位置を保持するための変数
    private Vector2 scrollPosition;

    [MenuItem("Tools/Image Resizer & Cropper")]
    public static void ShowWindow()
    {
        GetWindow<ImageResizerAndCropper>("Image Resizer & Cropper");
    }

    void OnGUI()
    {
        // スクロールビューの開始
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        GUILayout.Label("Image Resizer & Cropper", EditorStyles.boldLabel);

        // ソース画像リストの表示とD&Dエリア
        GUILayout.Space(10);
        EditorGUILayout.LabelField("Source Images (Drag & Drop here):");
        Rect dropArea = GUILayoutUtility.GetRect(0.0f, 100.0f, GUILayout.ExpandWidth(true));
        GUI.Box(dropArea, "Drag Textures Here");

        // ドロップされたオブジェクトの処理
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

        // リスト内の画像を表示
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

        // 処理実行ボタンはスクロールビューの外側に配置することで、
        // ウィンドウの一番下に固定されます。（ここは好みで調整可能です）
        EditorGUI.BeginDisabledGroup(sourceImages.Count == 0 || string.IsNullOrEmpty(outputFolderPath));
        if (GUILayout.Button("Resize and Crop Images", GUILayout.Height(40)))
        {
            ProcessImages();
        }
        EditorGUI.EndDisabledGroup();
    }

    void ProcessImages()
    {
        if (!Directory.Exists(outputFolderPath))
        {
            Directory.CreateDirectory(outputFolderPath);
            AssetDatabase.Refresh(); // Unityエディタにフォルダ作成を通知
        }

        foreach (Texture2D originalTexture in sourceImages)
        {
            if (originalTexture == null) continue;

            Texture2D resizedTexture = ResizeAndCrop(originalTexture, targetWidth, targetHeight);

            // PNGとして保存
            byte[] bytes = resizedTexture.EncodeToPNG();
            string filePath = Path.Combine(outputFolderPath, originalTexture.name + "_" + targetWidth + "x" + targetHeight + ".png");
            File.WriteAllBytes(filePath, bytes);

            // ログとメモリ解放
            Debug.Log($"Processed: {originalTexture.name} -> {filePath}");
            DestroyImmediate(resizedTexture); // 生成したTexture2Dを解放
        }

        AssetDatabase.Refresh(); // Unityエディタに新しいアセットの生成を通知
        EditorUtility.DisplayDialog("Image Processing Complete", $"{sourceImages.Count} images processed successfully!", "OK");

        // 処理後、元のテクスチャの読み書き設定を元に戻す
        foreach (var img in sourceImages) SetTextureReadable(img, false);
        sourceImages.Clear();
    }

    /// <summary>
    /// 画像を指定されたターゲット解像度にリサイズし、アスペクト比を維持しつつ中央をトリミングします。
    /// </summary>
    /// <param name="source">元のTexture2D</param>
    /// <param name="targetW">目標幅</param>
    /// <param name="targetH">目標高さ</param>
    /// <returns>リサイズ＆トリミングされた新しいTexture2D</returns>
    private Texture2D ResizeAndCrop(Texture2D source, int targetW, int targetH)
    {
        // sourceTextureが読み込み可能であることを確認
        if (!source.isReadable)
        {
            Debug.LogError($"Texture '{source.name}' is not readable. Please set 'Read/Write Enabled' in its Import Settings.");
            return null;
        }

        // 目標のアスペクト比
        float targetAspect = (float)targetW / targetH;
        // 元画像のアスペクト比
        float sourceAspect = (float)source.width / source.height;

        int cropX = 0;
        int cropY = 0;
        int cropWidth = source.width;
        int cropHeight = source.height;

        if (sourceAspect > targetAspect) // 元画像が目標より横長
        {
            // 目標アスペクト比に合わせて高さを基準に幅をトリミング
            cropWidth = Mathf.RoundToInt(source.height * targetAspect);
            cropX = (source.width - cropWidth) / 2; // 中央からトリミング
        }
        else if (sourceAspect < targetAspect) // 元画像が目標より縦長
        {
            // 目標アスペクト比に合わせて幅を基準に高さをトリミング
            cropHeight = Mathf.RoundToInt(source.width / targetAspect);
            cropY = (source.height - cropHeight) / 2; // 中央からトリミング
        }
        // else の場合、アスペクト比が同じなのでトリミングは不要

        // トリミングされた領域のピクセルを取得
        Color[] croppedPixels = source.GetPixels(cropX, cropY, cropWidth, cropHeight);
        Texture2D croppedTexture = new Texture2D(cropWidth, cropHeight);
        croppedTexture.SetPixels(croppedPixels);
        croppedTexture.Apply();

        // ここでリサイズを行う（UnityのTexture2Dは直接リサイズするメソッドがないため、手動またはGraphics.Blitを使用）
        // 簡素化のため、今回はGetPixels/SetPixelsでリサイズを行う関数を作成します。
        // より高品質なリサイズが必要な場合は、Graphics.Blitを使ったレンダーテクスチャ経由のリサイズが推奨されます。

        Texture2D finalTexture = new Texture2D(targetW, targetH, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[targetW * targetH];
        float dx = (float)croppedTexture.width / targetW;
        float dy = (float)croppedTexture.height / targetH;

        for (int y = 0; y < targetH; y++)
        {
            for (int x = 0; x < targetW; x++)
            {
                // バイリニア補間などの高品質なリサイズではないが、機能デモ用
                pixels[y * targetW + x] = croppedTexture.GetPixelBilinear(x * dx / croppedTexture.width, y * dy / croppedTexture.height);
            }
        }
        finalTexture.SetPixels(pixels);
        finalTexture.Apply();

        DestroyImmediate(croppedTexture); // 中間生成したテクスチャを解放
        return finalTexture;
    }

    /// <summary>
    /// テクスチャのImport SettingsでRead/Write Enabledを切り替えます。
    /// </summary>
    private void SetTextureReadable(Texture2D texture, bool readable)
    {
        string path = AssetDatabase.GetAssetPath(texture);
        if (string.IsNullOrEmpty(path)) return; // Runtimeで生成されたテクスチャなどの場合はパスがない

        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;

        if (importer.isReadable != readable)
        {
            importer.isReadable = readable;
            importer.SaveAndReimport();
        }
    }
}
#endif