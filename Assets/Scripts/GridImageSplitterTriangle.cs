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
        
        // 💡 読み書き可能チェック
        if (!srcTex.isReadable)
        {
            Debug.LogError("元のテクスチャのインポート設定で 'Read/Write Enabled' が有効になっていません。有効にして再試行してください。");
            return;
        }

        // 画像名を取得（アセットパスから取得を試みる）
        string imageName = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(srcTex));
        if (string.IsNullOrEmpty(imageName))
        {
             imageName = Path.GetFileNameWithoutExtension(srcTex.name);
        }
        string saveFolder = GetUniqueFolder(outputFolder, imageName);

        if (!Directory.Exists(saveFolder))
            Directory.CreateDirectory(saveFolder);

        // 💡 パスをAssets/から始まる形に調整
        string relativeSaveFolder = saveFolder.Replace(Application.dataPath, "Assets");
        if (!relativeSaveFolder.StartsWith("Assets"))
        {
             relativeSaveFolder = "Assets/" + relativeSaveFolder;
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
        
        // すべての画像を一度ディスクに保存し、インポートを完了させるためのリスト
        List<(int x, int y, string assetPath)> importList = new List<(int x, int y, string assetPath)>();

        // === 1️⃣ テクスチャの切り出しと保存 ===
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < cols; x++)
            {
                bool pointingUp = ((x + y) % 2 == 0);

                int px = startX + Mathf.RoundToInt(x * (triSize / 2f));
                int py = startY + Mathf.RoundToInt(y * triHeight);

                int w = Mathf.RoundToInt(triSize);
                int h = Mathf.RoundToInt(triHeight);

                // 範囲外チェック
                if (px + w > srcTex.width || py + h > srcTex.height || px < 0 || py < 0) continue;

                // 💡 CreateTriangleTextureで生成されたTexture2Dをファイルに保存
                Texture2D triTex = CreateTriangleTexture(srcTex, px, py, w, h, pointingUp);

                string fileName = $"tri_{y}_{x}.png";
                string fullPath = Path.Combine(saveFolder, fileName);
                string assetPath = Path.Combine(relativeSaveFolder, fileName).Replace('\\', '/');
                
                File.WriteAllBytes(fullPath, triTex.EncodeToPNG());
                
                importList.Add((x, y, assetPath));
                
                // 💡 メモリ上のTexture2Dを破棄
                Object.DestroyImmediate(triTex);
            }
        }
        
        // === 2️⃣ アセットデータベースの更新と再インポート ===
        
        // 全ファイルの書き込み後、一度アセットデータベースを更新してファイル群を認識させる
        AssetDatabase.Refresh(); 

        foreach (var item in importList)
        {
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(item.assetPath);
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.alphaIsTransparency = true;
                importer.isReadable = false; // 読み込み不要なのでOFFに戻す
                importer.spriteImportMode = SpriteImportMode.Single;
                
                importer.SaveAndReimport();
            }
        }
        
        AssetDatabase.Refresh(); // Spriteアセットが確実に生成されるのを待つ

        // === 3️⃣ UI配置とSprite紐付け ===
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < cols; x++)
            {
                bool pointingUp = ((x + y) % 2 == 0);

                string fileName = $"tri_{y}_{x}.png";
                string assetPath = Path.Combine(relativeSaveFolder, fileName).Replace('\\', '/');

                // 💡 永続アセットのロード
                Sprite sp = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                
                if (sp == null)
                {
                    // ファイルがロードできない場合はスキップ
                    Debug.LogError($"Spriteアセットのロード失敗。ファイルを確認してください: {assetPath}");
                    continue;
                }

                GameObject answerObj = new GameObject($"answer_{y}_{x}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(UnityEngine.UI.Outline));
                GameObject cellObj   = new GameObject($"cell_{y}_{x}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(TriangleCellCopyHandler));

                answerObj.transform.SetParent(this.transform, false);
                cellObj.transform.SetParent(this.transform, false);
                cellObj.transform.SetAsFirstSibling(); // CellをAnswerより手前に配置

                // 💡 Sprite引数を追加し、SetupRectAndSpriteを呼び出す
                SetupRectAndSprite(answerObj, img, sp, triSize, triHeight, x, y);
                SetupRectAndSprite(cellObj,   img, sp, triSize, triHeight, x, y);

                GridCell answerCell = answerObj.AddComponent<GridCell>();
                answerCell.isUpSide = pointingUp;
                answerCell.gridX = x;
                answerCell.gridY = y;
                
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
                UnityEngine.UI.Outline outline2 = answerObj.AddComponent<UnityEngine.UI.Outline>();
                if((outline != null || outline2 != null ) && _param != null)
                {
                    outline.effectColor = _param.OutLineColor;
                    outline2.effectColor = _param.OutLineColor;
                }
                outline.effectDistance = Vector2.one * 2f;
                outline2.effectDistance = Vector2.one * 3f;
            }
        }
        
        // === 4️⃣ コピー表示用オブジェクト生成 ===
        // UI生成ループからコピー生成処理を分離し、後でまとめて実行
        CreateCellCopies();

        // 💡 最終的に変更をUnityに保存させる
        EditorUtility.SetDirty(this.gameObject);
        AssetDatabase.SaveAssets();

        Debug.Log($"三角形分割が完了！保存先: {saveFolder}");
    }

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
                AnswerGridPos ansPos = child.GetComponent<AnswerGridPos>();
                CreateShadow(ansPos, child.GetComponent<RectTransform>().sizeDelta);
                copyObj.transform.SetParent(child, false);
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
