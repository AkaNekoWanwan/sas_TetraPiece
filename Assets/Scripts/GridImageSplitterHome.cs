using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

[ExecuteInEditMode]
[RequireComponent(typeof(Image))]
public class GridImageSplitterHome : AbstractGridImageSplitter
{
    public Sprite maskSp = default;
    public Font _font;
    public List<HomePanel> HomePanels = default;
#if UNITY_EDITOR

    public override ShapeType GetShapeType()
    {
        return ShapeType.Square;
    }

    public override void SplitImage()
    {
        base.SplitImage();
        HomePanels = new List<HomePanel>();
        
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

        // 💡 実行モードチェック: Editor上でのみ実行されるように念のためチェック
        if (!Application.isEditor)
        {
            Debug.LogError("この処理はエディターモードでのみ実行可能です。");
            return;
        }

        Sprite sprite = img.sprite;
        Texture2D srcTex = sprite.texture;
        Rect rect = sprite.rect;

        // 読み書き可能なテクスチャか確認（非ReadOnlyに設定されているか）
        if (!srcTex.isReadable)
        {
            Debug.LogError("元のテクスチャのインポート設定で 'Read/Write Enabled' が有効になっていません。有効にして再試行してください。");
            return;
        }

        string imageName = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(srcTex));
        if (string.IsNullOrEmpty(imageName))
        {
             imageName = Path.GetFileNameWithoutExtension(srcTex.name);
        }
        string saveFolder = GetUniqueFolder(outputFolder, imageName);

        if (!Directory.Exists(saveFolder))
            Directory.CreateDirectory(saveFolder);
        
        // パスをAssets/から始まる形に調整
        string relativeSaveFolder = saveFolder.Replace(Application.dataPath, "Assets");
        if (!relativeSaveFolder.StartsWith("Assets"))
        {
             relativeSaveFolder = "Assets/" + relativeSaveFolder;
        }

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

        // すべての画像を一度ディスクに保存し、インポートを完了させるためのリスト
        List<(int x, int y, string assetPath)> importList = new List<(int x, int y, string assetPath)>();


        // === 3️⃣ 各セルを生成 (テクスチャ保存まで) ===
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
                int px = offsetX + x * cellSize;
                int py = offsetY + y * cellSize;
                int w = cellSize;
                int h = cellSize;

                // 範囲外チェック
                if (px + w > srcTex.width || py + h > srcTex.height || px < 0 || py < 0)
                    continue;

                string fileName = $"grid_{y}_{x}.png";
                string fullPath = Path.Combine(saveFolder, fileName);
                string assetPath = Path.Combine(relativeSaveFolder, fileName).Replace('\\', '/');

                if(!IsDummyAnswerOnly(x, y))
                {
                    Color[] pixels = srcTex.GetPixels(px, py, w, h);
                    Texture2D newTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                    newTex.SetPixels(pixels);
                    newTex.Apply();

                    // ディスクに保存
                    File.WriteAllBytes(fullPath, newTex.EncodeToPNG());
                    
                    importList.Add((x, y, assetPath));
                    
                    // 💡 注意: ここで破棄しないとメモリ上にテクスチャが残り続ける
                    Object.DestroyImmediate(newTex);
                }
            }
        }
        
        // === 3-1️⃣ アセットデータベースの更新と再インポート ===
        
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

                importer.spriteImportMode = SpriteImportMode.Single;
                
                importer.SaveAndReimport();
            }
        }
        
        // 再度更新を強制し、Spriteアセットが確実に生成されるのを待つ (これが「1フレーム待機」の代わり)
        AssetDatabase.Refresh();
        
        // ここでようやく、ディスク上の永続的なSpriteアセットをロード可能になる
        
        // === 4️⃣ UI配置とSprite紐付け ===
        
        int rowCount = rows;
        int colCount = cols;
        if(isCreative)
        {
            rowCount += 2;
            colCount += 2;
        }
        
        for (int y = initY; y < rowCount; y++)
        {
            for (int x = initX; x < colCount; x++)
            {
                string fileName = $"grid_{y}_{x}.png";
                string assetPath = Path.Combine(relativeSaveFolder, fileName).Replace('\\', '/');
                
                // === UI配置のための共通処理 ===
                
                GameObject cellObj = null;
                GameObject cellMaskObj = null;
                GameObject cellBack = null;
                GameObject cellTextNum = null;
                Sprite sp = null;
                
                // 💡 ディスクから永続アセットをロード
                sp = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                
                if (sp == null)
                {
                    // 念のためTexture2Dとしてロードし、エラーを出す
                    Texture2D texCheck = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                    if (texCheck != null)
                    {
                        // Sprite.Createは一時的なインスタンスを作るため避けるべきだが、ロード失敗時の代替としてログ
                        Debug.LogError($"Spriteアセットのロード失敗。Texture2Dはロードできたが、インポート設定を確認してください: {assetPath}");
                    }
                    else
                    {
                        Debug.LogError($"アセットファイル自体がロードできませんでした: {assetPath}");
                    }
                    // ロードに失敗したらこのセルの処理をスキップ
                    continue;
                }
                
                // Cellオブジェクトを生成 (Spriteがロードできた場合のみ)
                cellMaskObj = new GameObject($"cellMask_{y}_{x}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Mask), typeof(HomePanel));
                cellMaskObj.transform.SetParent(this.transform, false);
                cellObj = new GameObject($"cell_{y}_{x}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                cellObj.transform.SetParent(cellMaskObj.transform, false);
                cellBack = new GameObject($"PieceBack", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                cellBack.transform.SetParent(cellMaskObj.transform, false);
                cellTextNum = new GameObject($"TextNum", typeof(RectTransform), typeof(Text));
                cellTextNum.transform.SetParent(cellMaskObj.transform, false);

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

                RectTransform cellRT;
                float maskDownScale = 4f;
                cellRT = cellMaskObj.GetComponent<RectTransform>();
                cellRT.sizeDelta = new Vector2(uiCellSize - maskDownScale, uiCellSize - maskDownScale);
                cellRT.anchoredPosition = new Vector2(offsetUiX, offsetUiY);
                cellRT.SetAsFirstSibling();

                cellRT = cellObj.GetComponent<RectTransform>();
                cellRT.sizeDelta = uiCellSizeVec;
                cellRT.anchoredPosition = Vector2.zero;

                cellRT = cellBack.GetComponent<RectTransform>();
                cellRT.sizeDelta = uiCellSizeVec;
                cellRT.anchoredPosition = Vector2.zero;

                cellRT = cellTextNum.GetComponent<RectTransform>();
                cellRT.sizeDelta = uiCellSizeVec;
                cellRT.anchoredPosition = Vector2.zero;

                // 色設定とSprite紐付け
                cellMaskObj.GetComponent<Image>().sprite = maskSp;
                // 💡 ロードした永続的なSpriteアセットを紐付け
                cellObj.GetComponent<Image>().sprite = sp;

                cellBack.GetComponent<Image>().color = new Color32(35, 65, 77, 255);

                int number = (5 - y) * 5 + x + 1;
                // int number = 0;
                Text text = cellTextNum.GetComponent<Text>();
                text.text = "" + number;
                text.alignment = TextAnchor.MiddleCenter;
                text.font = _font;
                text.fontSize = 80;
                // text.color = new Color32(35, 65, 77, 255);

                HomePanel homePanel = cellMaskObj.GetComponent<HomePanel>();
                // number = (5 - y) * 5 + x + 1;
                homePanel.cellNumber = number;
                homePanel.hideableObjs = new List<GameObject>();
                homePanel.hideableObjs.Add(cellBack);
                homePanel.hideableObjs.Add(cellTextNum);
                HomePanels.Add(homePanel);
                homePanel.NumText = text;
                
                if(_param != null)
                {
                    Image cellImg = cellObj.GetComponent<Image>();
                }

                // === 5️⃣ 補助コンポーネント ===
                AnswerGridPos ansPos;
                ansPos = cellObj.AddComponent<AnswerGridPos>();
                ansPos.x = x;
                ansPos.y = y;
            }
        }
        
        // 💡 最終的に変更をUnityに保存させる
        EditorUtility.SetDirty(this.gameObject);
        AssetDatabase.SaveAssets();

        Debug.Log($"正方形分割が完了！保存先: {saveFolder}");
    }
#endif
}