using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

[ExecuteInEditMode]
[RequireComponent(typeof(Image))]
public class GridImageSplitter : AbstractGridImageSplitter
{
#if UNITY_EDITOR

    public override ShapeType GetShapeType()
    {
        return ShapeType.Square;
    }

    public override void SplitImage()
    {
        Image img = GetComponent<Image>();
        if (img == null || img.sprite == null)
        {
            Debug.LogError("Image または Sprite が設定されていません。");
            return;
        }

        Sprite sprite = img.sprite;
        Texture2D srcTex = sprite.texture;
        Rect rect = sprite.rect;

        string imageName = Path.GetFileNameWithoutExtension(srcTex.name);
        string saveFolder = GetUniqueFolder(outputFolder, imageName);

        if (!Directory.Exists(saveFolder))
            Directory.CreateDirectory(saveFolder);

        int fullW = (int)rect.width;
        int fullH = (int)rect.height;

        // === 1️⃣ 正方形の切り出し範囲を計算 ===
        int squareSize = Mathf.RoundToInt(Mathf.Min(fullW, fullH) * (targetPercent / 100f));

        // 画像中心を基準に正方形範囲を決定
        int startX = (int)(rect.x + (fullW - squareSize) / 2f);
        int startY = (int)(rect.y + (fullH - squareSize) / 2f);

        // === 2️⃣ 分割単位（正方形セルサイズ） ===
        int cellSize = Mathf.RoundToInt(squareSize / Mathf.Max(rows, cols));

        int usedWidth = cellSize * cols;
        int usedHeight = cellSize * rows;

        // 正方形領域の中央にグリッドを配置する
        int offsetX = startX + (squareSize - usedWidth) / 2;
        int offsetY = startY + (squareSize - usedHeight) / 2;

        // === 3️⃣ 各セルを生成 ===
        int initY = 0;
        int initX = 0;
        int targetRows = rows;
        int targetCols = cols;
        if(isCreative)
        {
            initY = -2;
            initX = -2;
            targetRows += 2;
            targetCols += 2;
        }
        bool isSetCellScale = false;

        for (int y = initY; y < targetRows; y++)
        {
            for (int x = initX; x < targetCols; x++)
            {
                Debug.Log($"info {x}, {y}, {targetRows}, {targetCols}, {IsDummyAnswerOnly(x, y)}");
                int px = offsetX + x * cellSize;
                int py = offsetY + y * cellSize;
                int w = cellSize;
                int h = cellSize;

                if (px + w > srcTex.width || py + h > srcTex.height)
                    continue;

                string assetPath = $"{saveFolder}/grid_{y}_{x}.png";
                if(!IsDummyAnswerOnly(x, y))
                {
                    Color[] pixels = srcTex.GetPixels(px, py, w, h);
                    Texture2D newTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                    newTex.SetPixels(pixels);
                    newTex.Apply();

                    assetPath = $"{saveFolder}/grid_{y}_{x}.png";
                    File.WriteAllBytes(assetPath, newTex.EncodeToPNG());

                    AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                    TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
                    if (importer != null)
                    {
                        importer.textureType = TextureImporterType.Sprite;
                        importer.alphaIsTransparency = true;
                        importer.SaveAndReimport();
                    }
                }

                // === 4️⃣ UI配置 ===
                GameObject answerObj = new GameObject($"answer_{y}_{x}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(UnityEngine.UI.Outline));
                answerObj.transform.SetParent(this.transform, false);

                GameObject cellObj = null;
                if(!IsDummyAnswerOnly(x, y))
                {
                    cellObj = new GameObject($"cell_{y}_{x}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    cellObj.transform.SetParent(this.transform, false);
                }

                RectTransform parentRT = img.GetComponent<RectTransform>();
                Vector2 parentSize = parentRT.rect.size;

                // 正方形領域（UI上）の辺長
                float uiSquare = Mathf.Min(parentSize.x, parentSize.y) * (targetPercent / 100f);

                // 分割セルのサイズ（常に正方形）
                float uiCellSize = uiSquare / Mathf.Max(rows, cols);
                Vector2 uiCellSizeVec = new Vector2(uiCellSize, uiCellSize);
                SetCellScale(uiCellSize);

                // 配置
                float offsetUiX = (x - (cols - 1) * 0.5f) * uiCellSize;
                float offsetUiY = (y - (rows - 1) * 0.5f) * uiCellSize;

                RectTransform ansRT = answerObj.GetComponent<RectTransform>();
                ansRT.sizeDelta = uiCellSizeVec;
                ansRT.anchoredPosition = new Vector2(offsetUiX, offsetUiY);

                RectTransform cellRT;
                if(!IsDummyAnswerOnly(x, y))
                {
                    cellRT = cellObj.GetComponent<RectTransform>();
                    cellRT.sizeDelta = uiCellSizeVec;
                    cellRT.anchoredPosition = new Vector2(offsetUiX, offsetUiY);
                    cellRT.SetAsFirstSibling();
                }

                UnityEngine.UI.Outline outline = answerObj.GetComponent<UnityEngine.UI.Outline>();
                if(outline != null && _param != null)
                {
                    outline.effectColor = _param.OutLineColor;
                    outline.effectDistance = _param.OutLineSize;
                }

                // 色設定、
                Sprite sp = null;
                if(!IsDummyAnswerOnly(x, y))
                {
                    sp = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                    if (sp == null)
                    {
                        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                        if (tex != null)
                            sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                    }

                    if (sp != null)
                    {
                        answerObj.GetComponent<Image>().sprite = sp;
                        cellObj.GetComponent<Image>().sprite = sp;
                    }
                    else
                    {
                        Debug.LogError($"Spriteロード失敗: {assetPath}");
                    }
                }
                if(_param != null)
                {
                    Image answerImg = answerObj.GetComponent<Image>();
                    answerImg.material = _param.AnswerMaterial;
                    answerImg.color = _param.AnswerColor;
                    if(!IsDummyAnswerOnly(x, y))
                    {
                        Image cellImg = cellObj.GetComponent<Image>();
                        cellImg.material = _param.CellsMaterial;
                    }
                }

                // === 5️⃣ 補助コンポーネント ===
                GridCell gridCell = answerObj.AddComponent<GridCell>();
                AnswerGridPos ansPos;
                if(!IsDummyAnswerOnly(x, y))
                {
                    ansPos = cellObj.AddComponent<AnswerGridPos>();
                    ansPos.answerGrid = answerObj;
                    ansPos.x = x;
                    ansPos.y = y;
                }
                else
                    continue;

                // === 6️⃣ コピー表示用オブジェクト ===
                GameObject copyObj = new GameObject("cell_copy", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(UnityEngine.UI.Outline));
                copyObj.transform.SetParent(cellObj.transform, false);

                RectTransform copyRT = copyObj.GetComponent<RectTransform>();
                copyRT.localPosition = new Vector3(0, 0, 10);
                copyRT.localScale = Vector3.one;
                if(!isCreative)
                    copyRT.localScale *= 1.1f;
                else
                    copyRT.localScale *= 1.03f;
                copyRT.sizeDelta = uiCellSizeVec;

                Image copyImg = copyObj.GetComponent<Image>();
                copyImg.sprite = sp;
                if (cellCopyMaterial != null)
                    copyImg.material = cellCopyMaterial;

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
            }
        }

        Debug.Log($"正方形分割が完了！保存先: {saveFolder}");
    }
#endif
}
