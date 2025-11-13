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
        Image img = GetComponent<Image>();
        if (img == null || img.sprite == null)
        {
            Debug.LogError("Image または Sprite が設定されていません。");
            return;
        }

        Sprite sprite = img.sprite;
        Texture2D srcTex = sprite.texture;
        Rect rect = sprite.rect;

        // 画像名を取得（拡張子なし）
        string imageName = Path.GetFileNameWithoutExtension(srcTex.name);
        string saveFolder = GetUniqueFolder(outputFolder, imageName);

        int fullW = (int)rect.width;
        int fullH = (int)rect.height;

        float targetW = fullW * (targetPercent / 100f);
        float targetH = fullH * (targetPercent / 100f);
        int squareSize = Mathf.RoundToInt(Mathf.Min(targetW, targetH));

        int startX = (int)(rect.x + (fullW - squareSize) / 2f) + (int)_trimShift.x;
        int startY = (int)(rect.y + (fullH - squareSize) / 2f) + (int)_trimShift.y;

        float triSize = squareSize / Mathf.Max(rows, cols);
        float triHeight = Mathf.Sqrt(3f) / 2f * triSize;
        SetCellScale(triSize);

        if (!Directory.Exists(saveFolder))
            Directory.CreateDirectory(saveFolder);

        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < cols; x++)
            {
                bool pointingUp = ((x + y) % 2 == 0);

                int px = startX + Mathf.RoundToInt(x * (triSize / 2f));
                int py = startY + Mathf.RoundToInt(y * triHeight);

                int w = Mathf.RoundToInt(triSize);
                int h = Mathf.RoundToInt(triHeight);

                if (px + w > srcTex.width || py + h > srcTex.height) continue;

                Texture2D triTex = CreateTriangleTexture(srcTex, px, py, w, h, pointingUp);

                string assetPath = $"{saveFolder}/tri_{y}_{x}.png";
                File.WriteAllBytes(assetPath, triTex.EncodeToPNG());

                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
                if (importer != null)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.alphaIsTransparency = true;
                    importer.SaveAndReimport();
                }
                
                AssetDatabase.Refresh();
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);

                GameObject answerObj = new GameObject($"answer_{y}_{x}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(UnityEngine.UI.Outline));
                GameObject cellObj   = new GameObject($"cell_{y}_{x}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(TriangleCellCopyHandler));

                answerObj.transform.SetParent(this.transform, false);
                cellObj.transform.SetParent(this.transform, false);

                SetupRectAndSprite(answerObj, img, assetPath, triSize, triHeight, x, y);
                SetupRectAndSprite(cellObj,   img, assetPath, triSize, triHeight, x, y);

                GridCell answerCell = answerObj.AddComponent<GridCell>();
                answerCell.isUpSide = pointingUp;
                
                AnswerGridPos ansPos = cellObj.AddComponent<AnswerGridPos>();
                ansPos.answerGrid = answerObj;
                ansPos.x = x;
                ansPos.y = y;
                ansPos.isUpSide = pointingUp;

                // === パラメーター付与 ===
                if(_param != null)
                {
                    Image answerImg = answerObj.GetComponent<Image>();
                    answerImg.material = _param.AnswerMaterial;
                    answerImg.color = _param.AnswerColor;
                    Image cellImg = cellObj.GetComponent<Image>();
                    cellImg.material = _param.CellsMaterial;
                }

                TriangleCellCopyHandler copyHnandler = cellObj.gameObject.GetComponent<TriangleCellCopyHandler>();
                copyHnandler.IsUpSide = pointingUp;
                copyHnandler.CellPos = new Vector2Int(x, y);

                UnityEngine.UI.Outline outline = answerObj.GetComponent<UnityEngine.UI.Outline>();
                if(outline != null && _param != null)
                {
                    outline.effectColor = _param.OutLineColor;
                    outline.effectDistance = _param.OutLineSize;
                }

                Sprite sp = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                if (sp != null)
                {
                    GameObject copyObj = new GameObject("cell_copy", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    copyObj.transform.SetParent(cellObj.transform, false);

                    RectTransform copyRT = copyObj.GetComponent<RectTransform>();

                    float posY = 22f;
                    if(!pointingUp)
                        posY *= -1f;
                    copyRT.localPosition = new Vector3(0, posY, 10);
                    copyRT.localScale = new Vector3(1.1f, 1.1f, 1.1f);
                    copyRT.sizeDelta = cellObj.GetComponent<RectTransform>().sizeDelta;

                    Image copyImg = copyObj.GetComponent<Image>();
                    copyImg.sprite = sp;
                    if (cellCopyMaterial != null)
                    {
                        copyImg.material = cellCopyMaterial;
                    }
                    if(_param != null)
                    {
                        copyImg.material = _param.OutLineMaterial;
                        copyImg.color = _param.OutLineColor;
                    }
                }
            }
        }

        Debug.Log($"三角形分割が完了！保存先: {saveFolder}");
        
        // UnityEditor.EditorApplication.delayCall += () => CreateCellCopies();
        CreateCellCopies();
    }
#endif

    void CreateCellCopies()
    {
        List<TriangleCellCopyHandler> copyHnandlers = new List<TriangleCellCopyHandler>();
        foreach (Transform child in transform)
        {
            if (child.name.StartsWith("cell_"))
            {
                Image cellImg = child.GetComponent<Image>();
                if (cellImg == null || cellImg.sprite == null) continue;

                GameObject copyObj = new GameObject("cell_copy", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                copyObj.transform.SetParent(child, false);

                AnswerGridPos ansPos = child.GetComponent<AnswerGridPos>();
                float posY = -22f;
                if (!ansPos.isUpSide)
                    posY *= -1f;

                TriangleCellCopyHandler copyHnandler = child.GetComponent<TriangleCellCopyHandler>();
                copyHnandlers.Add(copyHnandler);
                copyHnandler.CellCopy = copyObj.transform;

                RectTransform copyRT = copyObj.GetComponent<RectTransform>();
                copyRT.localPosition = new Vector3(0, posY, 10);
                copyRT.localScale = new Vector3(1.1f, 1.1f, 1.1f);
                copyRT.sizeDelta = child.GetComponent<RectTransform>().sizeDelta;

                Image copyImg = copyObj.GetComponent<Image>();
                copyImg.sprite = cellImg.sprite;

                if (cellCopyMaterial != null)
                {
                    copyImg.material = cellCopyMaterial;
                }
                if (_param != null)
                {
                    copyImg.material = _param.OutLineMaterial;
                    copyImg.color = _param.OutLineColor;
                }
            }
        }
        copyHnandlers[0].UpdateAllCellCopyTransform(copyHnandlers);
        Debug.Log("CellCopy生成完了！");
    }


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

#if UNITY_EDITOR
    void SetupRectAndSprite(GameObject obj, Image parentImg, string assetPath,
                            float triSize, float triHeight, int gridX, int gridY)
    {
        RectTransform parentRT = parentImg.GetComponent<RectTransform>();
        RectTransform rtChild = obj.GetComponent<RectTransform>();

        Vector2 parentSize = parentRT.rect.size;
        float uiSquare = Mathf.Min(parentSize.x, parentSize.y) * (targetPercent / 100f);
        float uiSize = uiSquare / Mathf.Max(rows, cols);
        float uiHeight = Mathf.Sqrt(3f) / 2f * uiSize;

        rtChild.sizeDelta = new Vector2(uiSize, uiHeight);

        float offsetX = gridX * (uiSize / 2f);
        float offsetY = gridY * uiHeight;

        offsetX -= (cols - 1) * (uiSize / 2f) / 2f;
        offsetY -= (rows - 1) * uiHeight / 2f;

        rtChild.anchoredPosition = new Vector2(offsetX, offsetY);

        Image cImg = obj.GetComponent<Image>();
        Sprite sp = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (sp == null)
        {
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (tex != null)
            {
                sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }
        }

        if (sp != null)
            cImg.sprite = sp;
        else
            Debug.LogError($"Spriteロード失敗: {assetPath}");
    }
#endif
}
