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
    // [Header("Grid Settings")]
    // [Range(1, 20)] public int rows = 2;
    // [Range(1, 20)] public int cols = 2;
    // [Range(2, 30)] public int _pieceNum = 5;

    // [Header("Target Range (Center % of image)")]
    // [Range(10, 200)] public int targetPercent = 100;

    // [Header("Output Settings")]
    // public string outputFolder = "Assets/Textures/Hexa";

    // [Header("Cell Copy Settings")]
    // public Material cellCopyMaterial;
    // public SpritterParam _param;
    // public float _shiftY = 0f;

    // public bool isSkip = true;   // 全更新時にスキップするか
    // public bool isPrefs = false;   // 全更新時にスキップするか
    // public bool isCreative = false;
    // public string PieceCreateSeed = ""; // ピース作成のシード値
    // public string backUpPieceCreateSeed = ""; // ピース作成のシード値のバックアップ
    // public List<string> avoidPatternSeeds = default;

    // // フォルダ名重複回避
    // string GetUniqueFolder(string basePath, string imageName)
    // {
    //     string folderPath = Path.Combine(basePath, imageName);
    //     if (!Directory.Exists(folderPath))
    //         return folderPath;

    //     int counter = 1;
    //     while (Directory.Exists($"{folderPath}_{counter}"))
    //         counter++;

    //     return $"{folderPath}_{counter}";
    // }

#if UNITY_EDITOR
    // public void SplitImageHex()
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

        // === 分割ループ ===
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < cols; x++)
            {
                int px = startX + Mathf.RoundToInt(x * 1.5f * radius);
                int py = startY + Mathf.RoundToInt(y * hexHeight + (x % 2 == 1 ? hexHeight / 2f : 0));
                int w = Mathf.RoundToInt(hexWidth);
                int h = Mathf.RoundToInt(hexHeight);
                if (px + w > srcTex.width || py + h > srcTex.height)
                    continue;

                // === テクスチャ作成 ===
                Texture2D hexTex = CreateHexTexture(srcTex, px, py, w, h);
                string assetPath = $"{saveFolder}/hex_{y}_{x}.png";
                File.WriteAllBytes(assetPath, hexTex.EncodeToPNG());

                // === アセット登録 ===
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
                if (importer != null)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.alphaIsTransparency = true;
                    importer.SaveAndReimport();
                }

                // 確実に反映させる
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                // === Spriteロード（確実に同期） ===
                Sprite sp = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                if (sp == null)
                {
                    Debug.LogWarning($"⚠️ Spriteロード失敗: {assetPath} → 再試行中");
                    AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
                    sp = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                }

                if (sp == null)
                {
                    Debug.LogError($"❌ 最終的にSpriteをロードできませんでした: {assetPath}");
                    continue;
                }

                // === UI生成 ===
                GameObject answerObj = new GameObject($"answer_{y}_{x}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(UnityEngine.UI.Outline));
                GameObject cellObj = new GameObject($"cell_{y}_{x}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                answerObj.transform.SetParent(this.transform, false);
                cellObj.transform.SetParent(this.transform, false);

                SetupRectAndSprite(answerObj, img, sp, radius, hexWidth, hexHeight, x, y);
                SetupRectAndSprite(cellObj, img, sp, radius, hexWidth, hexHeight, x, y);

                // === コンポーネント付与 ===
                answerObj.AddComponent<GridCell>();
                AnswerGridPos ansPos = cellObj.AddComponent<AnswerGridPos>();
                ansPos.answerGrid = answerObj;
                ansPos.x = x;
                ansPos.y = y;

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
                if(outline != null && _param != null)
                {
                    outline.effectColor = _param.OutLineColor;
                    outline.effectDistance = _param.OutLineSize;
                }

                Vector3 setPos = answerObj.transform.position;
                setPos.y += _shiftY;
                answerObj.transform.position = setPos;

                setPos = cellObj.transform.position;
                setPos.y += _shiftY;
                cellObj.transform.position = setPos;
                

                // === cell_copy 生成（アウトライン付き）===
                GameObject copyObj = new GameObject("cell_copy", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                copyObj.transform.SetParent(cellObj.transform, false);

                RectTransform copyRT = copyObj.GetComponent<RectTransform>();
                copyRT.localPosition = Vector3.zero; // Z=0に
                copyRT.localScale = new Vector3(1.05f, 1.05f, 1f); // わずかに拡大
                copyRT.sizeDelta = cellObj.GetComponent<RectTransform>().sizeDelta;

                Image copyImg = copyObj.GetComponent<Image>();
                copyImg.sprite = sp;
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

        Debug.Log($"✅ 六角形分割が完了！保存先: {saveFolder}");
    }

    public override ShapeType GetShapeType()
    {
        return ShapeType.Hex;
    }
#endif

    // public void DeleteChilden()
    // {
    //     for (int j = this.transform.childCount - 1; j >= 0; j--)
    //     {
    //         Transform child = this.transform.GetChild(j);
    //         if (child != null)
    //         {
    //             DestroyImmediate(child.gameObject, true);
    //         }
    //     }
    // }

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
        float uiSquareBase = Mathf.Min(parentSize.x, parentSize.y) * (targetPercent / 100f);
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
        cImg.sprite = sp;
    }
#if UNITY_EDITOR
    // // ステージ作成に必要な一連の流れを実行
    // public void CreatePiece()
    // {
    //     // 設定されているピース数が大き過ぎたら修正
    //     int maxPieceNum = rows * cols;
    //     _pieceNum = Mathf.Min(_pieceNum, maxPieceNum);
    //     // 設定されているピース数がちいさすぎたら修正
    //     _pieceNum = Mathf.Max(_pieceNum, 2);

    //     // 子オブジェクトを全削除
    //     DeleteChilden();
    //     // ピースセル生成
    //     SplitImageHex();

    //     // 同じ階層のGridPieceListControllerを取得
    //     GridPieceListController gridPieceListController = this.transform.parent.gameObject.GetComponentInChildren<GridPieceListController>();
    //     gridPieceListController.isCreative = isCreative;
    //     gridPieceListController.gridParent = this.transform;

    //     // ピースセルをいい感じにピースリストに配置
    //     List<AnswerGridPos> cells = this.gameObject.GetComponentsInChildren<AnswerGridPos>().ToList();
    //     CellSplitter.CellSplit(cols, rows, ref _pieceNum, cells, gridPieceListController, ShapeType.Hex, PieceCreateSeed, avoidPatternSeeds);
    //     PieceCreateSeed = CellSplitter.PatternSeed;
    //     if (string.IsNullOrEmpty(backUpPieceCreateSeed))
    //         backUpPieceCreateSeed = PieceCreateSeed;

    //     // ピースのセットアップ
    //     gridPieceListController.SetUpChildrenPieceDragController();
    // }

    // public void Deletepiece()
    // {
    //     DeleteChilden();
    //     GridPieceListController gridPieceListController = this.transform.parent.gameObject.GetComponentInChildren<GridPieceListController>();
    //     gridPieceListController.PreSetPieceDragControllers();
    // }
#endif
}

#if UNITY_EDITOR
// [CustomEditor(typeof(GridImageSplitterHex))]
// public class HexImageSplitterEditor : Editor
// {
//     public override void OnInspectorGUI()
//     {
//         DrawDefaultInspector();
//         GridImageSplitterHex script = (GridImageSplitterHex)target;
//         GUILayout.Space(10);
//         if (GUILayout.Button("Split Hex Image"))
//         {
//             script.SplitImageHex();
//         }
//         if (GUILayout.Button("Delete all childen"))
//         {
//             script.DeleteChilden();
//         }
//         if (GUILayout.Button("Delete piece"))
//         {
//             script.Deletepiece();
//         }
//         if (GUILayout.Button("Auto Create piece"))
//         {
//             script.CreatePiece();
//         }
//     }
// }
#endif