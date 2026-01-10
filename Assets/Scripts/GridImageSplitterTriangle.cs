using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

[ExecuteInEditMode]
[RequireComponent(typeof(Image))]
public class GridImageSplitterTriangle : AbstractGridImageSplitter
{
#if UNITY_EDITOR
    public override ShapeType GetShapeType()
    {
        return ShapeType.Triangle;
    }

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

        int startX = (int)(rect.x + (fullW - squareSize) / 2f + _trimShift.x);
        int startY = (int)(rect.y + (fullH - squareSize) / 2f + _trimShift.y);

        float triSize = squareSize / Mathf.Max(rows, cols);
        float triHeight = Mathf.Sqrt(3f) / 2f * triSize;
        SetCellScale(triSize);
        
        // 実際に使用される領域のサイズを計算
        float usedWidth = (cols - 1) * (triSize / 2f) + triSize;  // 三角形は半分ずつずれる
        float usedHeight = rows * triHeight;
        
        // 中央揃えのためのオフセット
        int offsetX = startX + Mathf.RoundToInt((squareSize - usedWidth) / 2f);
        int offsetY = startY + Mathf.RoundToInt((squareSize - usedHeight) / 2f);
        
        List<(int x, int y, string assetPath)> importList = new List<(int x, int y, string assetPath)>();
        List<(GameObject obj, string assetPath)> objectsToAddressable = new List<(GameObject, string)>();

        // === テクスチャの切り出しと保存 ===
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < cols; x++)
            {
                bool pointingUp = ((x + y) % 2 == 0);

                int px = offsetX + Mathf.RoundToInt(x * (triSize / 2f));
                int py = offsetY + Mathf.RoundToInt(y * triHeight);

                int w = Mathf.RoundToInt(triSize);
                int h = Mathf.RoundToInt(triHeight);

                if (px + w > srcTex.width || py + h > srcTex.height || px < 0 || py < 0) continue;

                Texture2D triTex = CreateTriangleTexture(srcTex, px, py, w, h, pointingUp);

                string fileName = $"tri_{y}_{x}.png";
                string assetPath = $"{saveDirectory}/{fileName}";
                
                File.WriteAllBytes(assetPath, triTex.EncodeToPNG());
                importList.Add((x, y, assetPath));
                Object.DestroyImmediate(triTex);
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

        // === UI配置 ===
        RectTransform parentRT = _splitImage.GetComponent<RectTransform>();
        Vector2 parentSize = parentRT.rect.size;
        
        float uiSquare = Mathf.Min(parentSize.x, parentSize.y) * (targetPercent * fixTargetPercentCellSize / 100f);
        float uiTriSize = uiSquare / Mathf.Max(rows, cols);
        float uiTriHeight = Mathf.Sqrt(3f) / 2f * uiTriSize;
        
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < cols; x++)
            {
                bool pointingUp = ((x + y) % 2 == 0);
                
                string fileName = $"tri_{y}_{x}.png";
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

                // RectTransform設定
                float offsetUiX = x * (uiTriSize / 2f) - (cols - 1) * (uiTriSize / 4f);
                float offsetUiY = y * uiTriHeight - (rows - 1) * (uiTriHeight / 2f);

                RectTransform ansRT = answerObj.GetComponent<RectTransform>();
                ansRT.sizeDelta = new Vector2(uiTriSize, uiTriHeight);
                ansRT.anchoredPosition = new Vector2(offsetUiX, offsetUiY);

                RectTransform cellRT = cellObj.GetComponent<RectTransform>();
                cellRT.sizeDelta = new Vector2(uiTriSize, uiTriHeight);
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
                gridCell.isUpSide = pointingUp; // 三角形の上下向きを設定
                
                AnswerGridPos ansPos = cellObj.AddComponent<AnswerGridPos>();
                ansPos.answerGrid = answerObj;
                ansPos.x = x;
                ansPos.y = y;
                ansPos.InitPos = cellObj.transform.position;
                ansPos.isUpSide = pointingUp; // 三角形の上下向きを設定
                
                // TriangleCellCopyHandlerを追加（cell_copy調整用）
                TriangleCellCopyHandler triHandler = cellObj.AddComponent<TriangleCellCopyHandler>();
                triHandler.CellPos = new Vector2Int(x, y);
                triHandler.IsUpSide = ansPos.isUpSide;
                triHandler.Scale = 1.1f;

                CreateShadow(ansPos, new Vector2(uiTriSize, uiTriHeight));
                
                // コピー表示用オブジェクト（三角形はアウトラインなし）
                GameObject copyObj = new GameObject("cell_copy", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                copyObj.transform.SetParent(cellObj.transform, false);
                
                RectTransform copyRT = copyObj.GetComponent<RectTransform>();
                copyRT.localScale = new Vector3(1.1f, 1.1f, 1.1f);
                copyRT.localPosition = new Vector3(0, 0, 10);
                ansPos.outLine = copyRT;
                copyRT.sizeDelta = new Vector2(uiTriSize, uiTriHeight);
                
                // TriangleCellCopyHandlerにCellCopyを設定
                triHandler.CellCopy = copyRT;

                Image copyImg = copyObj.GetComponent<Image>();
                copyImg.sprite = sp;
                if (cellCopyMaterial != null) copyImg.material = cellCopyMaterial;
                if(_param != null)
                {
                    copyImg.color = _param.OutLineColor;
                    copyImg.material = _param.OutLineMaterial;
                }

                // Addressable化対象として登録
                objectsToAddressable.Add((answerObj, assetPath));
                objectsToAddressable.Add((cellObj, assetPath));
            }
        }
        
        // === 最終処理（Addressable化とプレハブ保存） ===
        SplitImageHelper.FinalizeAfterSplitImage(this, objectsToAddressable);
        
        Debug.Log($"✅ Triangle画像分割完了（Addressable統合版）: {groupName}");
    }

    /// <summary> 
    /// 三角形マスク付きテクスチャを生成
    /// </summary>
    Texture2D CreateTriangleTexture(Texture2D srcTex, int px, int py, int w, int h, bool pointingUp)
    {
        Color[] pixels = srcTex.GetPixels(px, py, w, h);
        Texture2D triTex = new Texture2D(w, h, TextureFormat.RGBA32, false);

        for (int yy = 0; yy < h; yy++)
        {
            for (int xx = 0; xx < w; xx++)
            {
                int idx = yy * w + xx;
                if (IsInsideTriangle(new Vector2(xx, yy), w, h, pointingUp))
                    triTex.SetPixel(xx, yy, pixels[idx]);
                else
                    triTex.SetPixel(xx, yy, Color.clear);
            }
        }

        triTex.Apply();
        return triTex;
    }

    bool IsInsideTriangle(Vector2 p, int w, int h, bool pointingUp)
    {
        float slope = (float)h / (w / 2f);
        if (pointingUp)
        {
            return (p.y >= 0) && (p.y <= h) &&
                   (p.y >= -slope * (p.x - w / 2f)) &&
                   (p.y >= slope * (p.x - w / 2f));
        }
        else
        {
            return (p.y >= 0) && (p.y <= h) &&
                   (p.y <= -slope * (p.x - w / 2f) + h) &&
                   (p.y <= slope * (p.x - w / 2f) + h);
        }
    }
    
    void SetupRectAndSprite(GameObject obj, Image parentImg, Sprite sp,
                            float triSize, float triHeight, int gridX, int gridY)
    {
        RectTransform parentRT = parentImg.GetComponent<RectTransform>();
        RectTransform rtChild = obj.GetComponent<RectTransform>();

        Vector2 parentSize = parentRT.rect.size;
        float uiSquare = Mathf.Min(parentSize.x, parentSize.y) * (targetPercent / 100f) * fixTargetPercentCellSize;
        float uiSize = uiSquare / Mathf.Max(rows, cols);
        float uiHeight = Mathf.Sqrt(3f) / 2f * uiSize;

        rtChild.sizeDelta = new Vector2(uiSize, uiHeight);

        float offsetX = gridX * (uiSize / 2f);
        float offsetY = gridY * uiHeight;

        offsetX -= (cols - 1) * (uiSize / 2f) / 2f;
        offsetY -= (rows - 1) * uiHeight / 2f;

        rtChild.anchoredPosition = new Vector2(offsetX, offsetY);

        Image cImg = obj.GetComponent<Image>();
        
        // 💡 ロード処理を削除し、受け取ったSpriteをそのまま使用
        if (sp != null)
            cImg.sprite = sp;
        else
            Debug.LogError($"Spriteが設定されていません。");
    }
#endif
}
