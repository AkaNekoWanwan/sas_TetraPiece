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
        
        // 既存の子オブジェクトを全て破棄
        while (transform.childCount > 0)
        {
            DestroyImmediate(transform.GetChild(0).gameObject);
        }

        if (_splitImage == null || _splitImage.sprite == null)
        {
            Debug.LogError("Image または Sprite が設定されていません。");
            return;
        }

        if (!Application.isEditor)
        {
            Debug.LogError("この処理はエディターモードでのみ実行可能です。");
            return;
        }

        Sprite sprite = _splitImage.sprite;
        Texture2D srcTex = sprite.texture;
        Rect rect = sprite.rect;
        
        if (!srcTex.isReadable)
        {
            Debug.LogError("元のテクスチャのインポート設定で 'Read/Write Enabled' が有効になっていません。");
            return;
        }

        // === Addressableグループの準備 ===
        GameObject stageRoot = transform.parent.parent.gameObject;
        var prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(stageRoot);
        string groupName = prefabAsset != null ? prefabAsset.name : "Default";
        
        var group = SplitImageHelper.GetOrCreateAddressableGroup(groupName);
        var settings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;

        // 保存先を統一（Addressable用のパスに直接保存）
        string saveDirectory = $"Assets/Prefabs/Addressable/{groupName}";
        if (!AssetDatabase.IsValidFolder(saveDirectory))
        {
            Directory.CreateDirectory(saveDirectory);
            AssetDatabase.Refresh();
        }

        int fullW = (int)rect.width;
        int fullH = (int)rect.height;

        float targetW = fullW * (targetPercent / 100f);
        float targetH = fullH * (targetPercent / 100f);
        int squareSize = Mathf.RoundToInt(Mathf.Min(targetW, targetH));

        // 奇数列がある場合、その上半分も含めた高さを考慮してセルサイズを計算
        bool hasOddColumn = cols > 1;
        float heightRatio = hasOddColumn ? (rows + 0.5f) : rows; // 奇数列の分0.5セル追加
        int cellSize = Mathf.RoundToInt(squareSize / Mathf.Max(heightRatio, cols));
        
        float radius = cellSize / 2f;
        float hexWidth = 2f * radius;
        float hexHeight = Mathf.Sqrt(3f) * radius;

        // 奇数列の上半分を含めた高さを使用
        int usedWidth = Mathf.RoundToInt((cols - 1) * 1.5f * radius + hexWidth);
        int usedHeight = Mathf.RoundToInt(rows * hexHeight + (hasOddColumn ? hexHeight / 2f : 0));
        
        // 画像の中心からのオフセット計算
        int startX = (int)(rect.x + (fullW - usedWidth) / 2f);
        int startY = (int)(rect.y + (fullH - usedHeight) / 2f);
        SetCellScale(hexWidth);

        List<(int x, int y, string assetPath)> importList = new List<(int x, int y, string assetPath)>();
        List<(GameObject obj, string assetPath)> objectsToAddressable = new List<(GameObject, string)>();

        // === テクスチャ生成とファイル書き込み ===
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < cols; x++)
            {
                int px = startX + Mathf.RoundToInt(x * 1.5f * radius);
                int py = startY + Mathf.RoundToInt(y * hexHeight + (x % 2 == 1 ? hexHeight / 2f : 0));
                int w = Mathf.RoundToInt(hexWidth);
                int h = Mathf.RoundToInt(hexHeight);
                
                if (px + w > srcTex.width || py + h > srcTex.height || px < 0 || py < 0)
                    continue;
                
                string fileName = $"hex_{y}_{x}.png";
                string assetPath = $"{saveDirectory}/{fileName}";

                Texture2D hexTex = CreateHexTexture(srcTex, px, py, w, h);
                File.WriteAllBytes(assetPath, hexTex.EncodeToPNG());
                importList.Add((x, y, assetPath));
                Object.DestroyImmediate(hexTex);
            }
        }

        // === インポート設定 ===
        AssetDatabase.Refresh(); 

        foreach (var item in importList)
        {
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(item.assetPath);
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.alphaIsTransparency = true;
                importer.isReadable = false;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.SaveAndReimport();
            }

            // Addressableに登録
            if (group != null && settings != null)
            {
                var guid = AssetDatabase.AssetPathToGUID(item.assetPath);
                settings.CreateOrMoveEntry(guid, group);
            }
        }
        
        AssetDatabase.Refresh();

        // === UI生成 ===
        RectTransform parentRT = _splitImage.GetComponent<RectTransform>();
        Vector2 parentSize = parentRT.rect.size;
        
        float uiSquare = Mathf.Min(parentSize.x, parentSize.y) * (targetPercent * fixTargetPercentCellSize / 100f);
        // キャプチャ時と同じheightRatioを使用
        float uiHeightRatio = hasOddColumn ? (rows + 0.5f) : rows;
        float uiCellSize = uiSquare / Mathf.Max(uiHeightRatio, cols);
        float uiRadius = uiCellSize / 2f;
        float uiHexWidth = 2f * uiRadius;
        float uiHexHeight = Mathf.Sqrt(3f) * uiRadius;

        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < cols; x++)
            {
                string fileName = $"hex_{y}_{x}.png";
                string assetPath = $"{saveDirectory}/{fileName}";
                
                Sprite sp = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                if (sp == null)
                {
                    Debug.LogError($"Spriteロード失敗: {assetPath}");
                    continue;
                }
                
                GameObject answerObj = new GameObject($"answer_{y}_{x}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(UnityEngine.UI.Outline));
                answerObj.transform.SetParent(this.transform, false);
                
                GameObject cellObj = new GameObject($"cell_{y}_{x}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                cellObj.transform.SetParent(this.transform, false);

                // RectTransform設定（uiHexHeight/4f下にシフト）
                float offsetUiX = x * 1.5f * uiRadius - (cols - 1) * 0.75f * uiRadius;
                float offsetUiY = y * uiHexHeight - (rows - 1) * (uiHexHeight / 2f) + (x % 2 == 1 ? uiHexHeight / 2f : 0) - uiHexHeight / 4f;

                RectTransform ansRT = answerObj.GetComponent<RectTransform>();
                ansRT.sizeDelta = new Vector2(uiHexWidth, uiHexHeight);
                ansRT.anchoredPosition = new Vector2(offsetUiX, offsetUiY);

                RectTransform cellRT = cellObj.GetComponent<RectTransform>();
                cellRT.sizeDelta = new Vector2(uiHexWidth, uiHexHeight);
                cellRT.anchoredPosition = new Vector2(offsetUiX, offsetUiY);
                cellRT.SetAsFirstSibling();

                // ★ GridCellBatchUpdaterと同じ設定を適用
                SplitImageHelper.ApplyStandardGridCellSettings(answerObj, _param);
                
                Image cellImg = cellObj.GetComponent<Image>();
                cellImg.sprite = sp;
                Image answerImg = answerObj.GetComponent<Image>();
                answerImg.sprite = sp;
                if(_param != null) cellImg.material = _param.CellsMaterial;
                
                // AddressableImageLoaderを生成時に追加
                AddressableImageLoader loader = cellObj.AddComponent<AddressableImageLoader>();
                loader.addressName = assetPath;

                // コンポーネント追加
                GridCell gridCell = answerObj.AddComponent<GridCell>();
                gridCell.gridX = x;
                gridCell.gridY = y;
                
                AnswerGridPos ansPos = cellObj.AddComponent<AnswerGridPos>();
                ansPos.answerGrid = answerObj;
                ansPos.x = x;
                ansPos.y = y;
                ansPos.InitPos = cellObj.transform.position;

                CreateShadow(ansPos, new Vector2(uiHexWidth, uiHexHeight));
                
                // コピー表示用オブジェクト
                GameObject copyObj = new GameObject("cell_copy", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(UnityEngine.UI.Outline));
                copyObj.transform.SetParent(cellObj.transform, false);
                
                RectTransform copyRT = copyObj.GetComponent<RectTransform>();
                copyRT.localPosition = new Vector3(0, 0, 10);
                copyRT.localScale = Vector3.one * 1.05f; // 六角形は1.05f
                ansPos.outLine = copyRT;
                copyRT.sizeDelta = new Vector2(uiHexWidth, uiHexHeight);

                Image copyImg = copyObj.GetComponent<Image>();
                copyImg.sprite = sp;
                if (cellCopyMaterial != null) copyImg.material = cellCopyMaterial;
                if(_param != null)
                {
                    copyImg.color = _param.OutLineColor;
                    copyImg.material = _param.OutLineMaterial;
                }

                UnityEngine.UI.Outline outline2 = copyObj.GetComponent<UnityEngine.UI.Outline>();
                if(outline2 != null && _param != null)
                {
                    outline2.effectColor = _param.OutLineColor;
                    outline2.effectDistance = _param.OutLineSize;
                }

                // Addressable化対象として登録
                objectsToAddressable.Add((answerObj, assetPath));
                objectsToAddressable.Add((cellObj, assetPath));
            }
        }
        
        // === 最終処理（Addressable化とプレハブ保存） ===
        SplitImageHelper.FinalizeAfterSplitImage(this, objectsToAddressable);
        
        Debug.Log($"✅ Hex画像分割完了（Addressable統合版）: {groupName}");
    }

    /// <summary>
    /// 六角形マスク付きテクスチャを生成
    /// </summary>
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

    public override ShapeType GetShapeType()
    {
        return ShapeType.Hex;
    }
#endif
}