using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

[ExecuteInEditMode]
[RequireComponent(typeof(Image))]
public class GridImageSplitterHex : AbstractGridImageSplitter
{
#if UNITY_EDITOR
    public override void SplitImage()
    {
        base.SplitImage();
        
        // 既存の子オブジェクトを全て破棄 (EditorUtilityを使う)
        while (transform.childCount > 0)
        {
            DestroyImmediate(transform.GetChild(0).gameObject);
        }

        Image img = GetComponent<Image>();
        if (img == null || img.sprite == null)
        {
            Debug.LogError("Image または Sprite が設定されていません。");
            return;
        }

        // 💡 実行モードチェック
        if (!Application.isEditor)
        {
            Debug.LogError("この処理はエディターモードでのみ実行可能です。");
            return;
        }

        Sprite sprite = img.sprite;
        Texture2D srcTex = sprite.texture;
        Rect rect = sprite.rect;
        
        // 読み書き可能なテクスチャか確認
        if (!srcTex.isReadable)
        {
            Debug.LogError("元のテクスチャのインポート設定で 'Read/Write Enabled' が有効になっていません。有効にして再試行してください。");
            return;
        }

        // パスの整備
        string imageName = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(srcTex));
        if (string.IsNullOrEmpty(imageName))
        {
             imageName = Path.GetFileNameWithoutExtension(srcTex.name);
        }
        string saveFolder = GetUniqueFolder(outputFolder, imageName);
        if (!Directory.Exists(saveFolder))
            Directory.CreateDirectory(saveFolder);

        string relativeSaveFolder = saveFolder.Replace(Application.dataPath, "Assets");
        if (!relativeSaveFolder.StartsWith("Assets"))
        {
             relativeSaveFolder = "Assets/" + relativeSaveFolder;
        }

        int fullW = (int)rect.width;
        int fullH = (int)rect.height;

        // 対象領域サイズ
        float targetW = fullW * (targetPercent / 100f);
        float targetH = fullH * (targetPercent / 100f);
        int squareSize = Mathf.RoundToInt(Mathf.Min(targetW, targetH));

        // セル半径と高さ
        int cellSize = squareSize / Mathf.Max(rows, cols);
        float radius = cellSize / 2f;
        float hexWidth = 2f * radius;
        float hexHeight = Mathf.Sqrt(3f) * radius;

        // 使用領域を中央に配置
        int usedWidth = Mathf.RoundToInt((cols - 1) * 1.5f * radius + hexWidth);
        int usedHeight = Mathf.RoundToInt((rows - 1) * hexHeight + hexHeight + (cols > 1 ? hexHeight / 2f : 0));
        int startX = (int)(rect.x + (fullW - usedWidth) / 2f);
        int startY = (int)(rect.y + (fullH - usedHeight) / 2f);
        SetCellScale(hexWidth);

        // すべての画像を一度ディスクに保存し、インポートを完了させるためのリスト
        List<(int x, int y, string assetPath)> importList = new List<(int x, int y, string assetPath)>();

        // === 1. テクスチャ生成とファイル書き込み ===
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < cols; x++)
            {
                int px = startX + Mathf.RoundToInt(x * 1.5f * radius);
                int py = startY + Mathf.RoundToInt(y * hexHeight + (x % 2 == 1 ? hexHeight / 2f : 0));
                int w = Mathf.RoundToInt(hexWidth);
                int h = Mathf.RoundToInt(hexHeight);
                
                // 範囲外チェック
                if (px + w > srcTex.width || py + h > srcTex.height || px < 0 || py < 0)
                    continue;
                
                string fileName = $"hex_{y}_{x}.png";
                string fullPath = Path.Combine(saveFolder, fileName);
                string assetPath = Path.Combine(relativeSaveFolder, fileName).Replace('\\', '/');

                // === 六角形マスク付きテクスチャ生成 ===
                Texture2D hexTex = CreateHexTexture(srcTex, px, py, w, h);
                
                // ディスクに保存
                File.WriteAllBytes(fullPath, hexTex.EncodeToPNG());
                
                importList.Add((x, y, assetPath));
                
                // 💡 メモリ上のテクスチャを即座に破棄
                Object.DestroyImmediate(hexTex);
            }
        }

        // === 2. アセットデータベースの更新とインポート設定（永続化処理） ===
        
        // 全ファイルの書き込み後、一度アセットデータベースを更新してファイル群を認識させる
        AssetDatabase.Refresh(); 

        foreach (var item in importList)
        {
            // 個別ファイルのインポート設定
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(item.assetPath);
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.alphaIsTransparency = true;
                importer.isReadable = false; // 読み込み不要なのでOFFに戻す
                
                // 💡 Sprite ModeをSingleに明示的に設定
                importer.spriteImportMode = SpriteImportMode.Single;
                
                importer.SaveAndReimport();
            }
        }
        
        // 再度更新を強制し、Spriteアセットが確実に生成されるのを待つ
        AssetDatabase.Refresh();

        // === 3. UI生成と永続Spriteの紐付け ===
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < cols; x++)
            {
                string fileName = $"hex_{y}_{x}.png";
                string assetPath = Path.Combine(relativeSaveFolder, fileName).Replace('\\', '/');
                
                // 💡 ディスクから永続アセットをロード
                Sprite sp = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);

                if (sp == null)
                {
                    Debug.LogError($"❌ Spriteアセットのロード失敗: {assetPath}");
                    continue;
                }

                // === UI生成 ===
                GameObject answerObj = new GameObject($"answer_{y}_{x}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(UnityEngine.UI.Outline));
                GameObject cellObj = new GameObject($"cell_{y}_{x}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                answerObj.transform.SetParent(this.transform, false);
                cellObj.transform.SetParent(this.transform, false);

                // Spriteの紐付けはSetupRectAndSprite内で実施
                SetupRectAndSprite(answerObj, img, sp, radius, hexWidth, hexHeight, x, y);
                SetupRectAndSprite(cellObj, img, sp, radius, hexWidth, hexHeight, x, y);

                // === コンポーネント付与 ===
                GridCell gridCell = answerObj.AddComponent<GridCell>();
                AnswerGridPos ansPos = cellObj.AddComponent<AnswerGridPos>();
                ansPos.answerGrid = answerObj;
                ansPos.x = x;
                ansPos.y = y;
                gridCell.gridX = x;
                gridCell.gridY = y;

                // === パラメーター付与 ===
                if(_param != null)
                {
                    Image answerImg = answerObj.GetComponent<Image>();
                    answerImg.material = _param.AnswerMaterial;
                    answerImg.color = _param.AnswerColor;
                    Image cellImg = cellObj.GetComponent<Image>();
                    cellImg.material = _param.CellsMaterial;
                }
                UnityEngine.UI.Outline outline = answerObj.GetComponent<UnityEngine.UI.Outline>();
                UnityEngine.UI.Outline outline2 = answerObj.AddComponent<UnityEngine.UI.Outline>();
                outline.effectDistance = Vector2.one * 2f;
                outline2.effectDistance = Vector2.one * 3f;
                if((outline != null || outline2 != null ) && _param != null)
                {
                    outline.effectColor = _param.OutLineColor;
                    outline2.effectColor = _param.OutLineColor;
                }

                Vector3 setPos = answerObj.transform.position;
                setPos.x += _trimShift.x;
                setPos.y += _trimShift.y;
                answerObj.transform.position = setPos;

                setPos = cellObj.transform.position;
                setPos.x += _trimShift.x;
                setPos.y += _trimShift.y;
                cellObj.transform.position = setPos;
                

                // === cell_copy 生成（アウトライン付き）===
                CreateShadow(ansPos, cellObj.GetComponent<RectTransform>().sizeDelta);
                GameObject copyObj = new GameObject("cell_copy", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                copyObj.transform.SetParent(cellObj.transform, false);

                RectTransform copyRT = copyObj.GetComponent<RectTransform>();
                copyRT.localPosition = Vector3.zero; // Z=0に
                copyRT.localScale = new Vector3(1.05f, 1.05f, 1f); // わずかに拡大
                copyRT.sizeDelta = cellObj.GetComponent<RectTransform>().sizeDelta;

                Image copyImg = copyObj.GetComponent<Image>();
                copyImg.sprite = sp; // 💡 永続Spriteを紐付け
                copyImg.color = Color.white;
                if (cellCopyMaterial != null)
                {
                    copyImg.material = cellCopyMaterial;
                }
                if(_param != null)
                {
                    copyImg.color = _param.OutLineColor;
                    copyImg.material = _param.OutLineMaterial;
                }
            }
        }
        
        // 💡 最終的に変更をUnityに保存させる
        EditorUtility.SetDirty(this.gameObject);
        AssetDatabase.SaveAssets();


        Debug.Log($"✅ 六角形分割が完了！保存先: {saveFolder}");
    }

    public override ShapeType GetShapeType()
    {
        return ShapeType.Hex;
    }
#endif

    // === 六角形マスク付きテクスチャ生成 ===
    Texture2D CreateHexTexture(Texture2D srcTex, int px, int py, int w, int h)
    {
        Color[] pixels = srcTex.GetPixels(px, py, w, h);
        Texture2D hexTex = new Texture2D(w, h, TextureFormat.RGBA32, false);

        Vector2 center = new Vector2(w / 2f, h / 2f);
        float radius = w / 2f;

        for (int yy = 0; yy < h; yy++)
        {
            for (int xx = 0; xx < w; xx++)
            {
                int idx = yy * w + xx;
                Vector2 p = new Vector2(xx, yy);
                if (IsInsideHexagon(p, center, radius))
                    hexTex.SetPixel(xx, yy, pixels[idx]);
                else
                    hexTex.SetPixel(xx, yy, Color.clear);
            }
        }

        hexTex.Apply();
        return hexTex;
    }

    bool IsInsideHexagon(Vector2 p, Vector2 center, float radius)
    {
        Vector2 d = new Vector2(Mathf.Abs(p.x - center.x), Mathf.Abs(p.y - center.y));
        if (d.x > radius) return false;
        if (d.y > Mathf.Sqrt(3f) * radius / 2f) return false;
        if (d.x / (radius * 0.5f) + d.y / (Mathf.Sqrt(3f) * radius / 2f) > 2f) return false;
        return true;
    }

    // === UI配置処理 ===
    void SetupRectAndSprite(GameObject obj, Image parentImg, Sprite sp,
                            float radius, float hexWidth, float hexHeight, int gridX, int gridY)
    {
        RectTransform parentRT = parentImg.GetComponent<RectTransform>();
        RectTransform rtChild = obj.GetComponent<RectTransform>();

        Vector2 parentSize = parentRT.rect.size;
        float uiSquareBase = Mathf.Min(parentSize.x, parentSize.y) * (targetPercent * fixTargetPercentCellSize / 100f);
        int uiSquareInt = Mathf.RoundToInt(uiSquareBase);
        int uiCellSizeInt = uiSquareInt / Mathf.Max(rows, cols);
        float uiRadius = uiCellSizeInt / 2f;

        float uiWidth = 2f * uiRadius;
        float uiHeight = Mathf.Sqrt(3f) * uiRadius;
        rtChild.sizeDelta = new Vector2(uiWidth, uiHeight);

        float offsetX = gridX * (1.5f * uiRadius);
        float offsetY = gridY * uiHeight + (gridX % 2 == 1 ? uiHeight / 2f : 0);
        offsetX -= (cols - 1) * (1.5f * uiRadius) * 0.5f;
        offsetY -= (rows - 1) * uiHeight * 0.5f;
        rtChild.anchoredPosition = new Vector2(offsetX, offsetY);

        Image cImg = obj.GetComponent<Image>();
        cImg.sprite = sp; // 💡 永続Spriteを紐付け
    }
}