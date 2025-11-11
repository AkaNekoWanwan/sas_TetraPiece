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
    // [Header("Grid Settings")]
    // [Range(1, 20)] public int rows = 2;
    // [Range(1, 20)] public int cols = 2;
    // [Range(2, 30)] public int _pieceNum = 5;

    // [Header("Target Range (Center % of image)")]
    // [Range(10, 1000)] public int targetPercent = 80;

    // [Header("Output Settings")]
    // public string outputFolder = "Assets/Textures/Square";

    // [Header("Cell Copy Settings")]
    // public Material cellCopyMaterial;
    // public SpritterParam _param;

    // public bool isSkip = true;   // 全更新時にスキップするか
    // public bool isPrefs = false;   // 全更新時にスキップするか
    // public bool isCreative = false;
    // public string PieceCreateSeed = ""; // ピース作成のシード値
    // public string backUpPieceCreateSeed = ""; // ピース作成のシード値のバックアップ
    // public List<string> _seeds; // 作成を避けるシード値
    // public List<string> avoidPatternSeeds = default;

    // private Coroutine _createPieceCoriutine = null;

#if UNITY_EDITOR
    // string GetUniqueFolder(string basePath, string imageName)
    // {
    //     string folderPath = Path.Combine(basePath, imageName);

    //     if (!Directory.Exists(folderPath))
    //         return folderPath;

    //     int counter = 1;
    //     while (Directory.Exists($"{folderPath}_{counter}")) counter++;
    //     return $"{folderPath}_{counter}";
    // }

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
    // public int text = 0;
    // private bool IsDummyAnswerOnly(int x, int y)
    // {
    //     Debug.Log($"info : {text}");
    //     text++;
    //     if(!isCreative)
    //         return false;
    //     if( x < 0 || y < 0 || cols <= x || rows <= y)
    //         return true;
    //     return false;
    // }

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
    //     SplitImage();

    //     // 同じ階層のGridPieceListControllerを取得
    //     GridPieceListController gridPieceListController = this.transform.parent.gameObject.GetComponentInChildren<GridPieceListController>();
    //     gridPieceListController.isCreative = isCreative;
    //     gridPieceListController.gridParent = this.transform;

    //     // ピースセルをいい感じにピースリストに配置
    //     List<AnswerGridPos> cells = this.gameObject.GetComponentsInChildren<AnswerGridPos>().ToList();
    //     CellSplitter.CellSplit( cols, rows, ref _pieceNum, cells, gridPieceListController, ShapeType.Square, PieceCreateSeed, avoidPatternSeeds );
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
// [CustomEditor(typeof(GridImageSplitter))]
// public class GridImageSplitterEditor : Editor
// {
//     public override void OnInspectorGUI()
//     {
//         DrawDefaultInspector();

//         GridImageSplitter script = (GridImageSplitter)target;

//         GUILayout.Space(10);

//         if (GUILayout.Button("Split Image"))
//         {
//             script.SplitImage();
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