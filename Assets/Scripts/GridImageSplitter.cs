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
    // ★ ShapeTypeをインスペクターで選択可能に
    [Header("Shape Configuration")]
    [SerializeField, Tooltip("形状タイプを選択")]
    private ShapeType _shapeType = ShapeType.Square;

    // ★ Strategyパターンでの処理委譲用
    private IShapeStrategy _currentStrategy;

#if UNITY_EDITOR

    private void OnValidate()
    {
        // Strategy初期化
        _currentStrategy = ShapeStrategyFactory.GetStrategy(_shapeType);
        
        // targetPercentを自動調整
        targetPercent = _currentStrategy.GetTargetPercent();
    }

    public override ShapeType GetShapeType()
    {
        return _shapeType;
    }

    /// <summary>
    /// ShapeTypeを設定（移行ツール用）
    /// </summary>
    public void SetShapeType(ShapeType shapeType)
    {
        _shapeType = shapeType;
        _currentStrategy = ShapeStrategyFactory.GetStrategy(_shapeType);
        targetPercent = _currentStrategy.GetTargetPercent();
    }

    public override void SplitImage()
    {
        // ★ ShapeTypeに応じて適切なSplitImage処理を実行
        // Triangle/Hexの場合は、一時的に対応するコンポーネントを追加して処理を委譲
        if (_shapeType == ShapeType.Triangle)
        {
            ExecuteTriangleSplitImage();
            return;
        }
        else if (_shapeType == ShapeType.Hex)
        {
            ExecuteHexSplitImage();
            return;
        }

        // Square用の処理（デフォルト）
        base.SplitImage();
        
        // 既存の子オブジェクトを全て破棄 (EditorUtilityを使う)
        while (transform.childCount > 0)
        {
            DestroyImmediate(transform.GetChild(0).gameObject);
        }
        
        if (_splitImage == null || _splitImage.sprite == null)
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

        Sprite sprite = _splitImage.sprite;
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
        int startX = (int)(rect.x + (fullW - squareSize) / 2f + _trimShift.x);
        int startY = (int)(rect.y + (fullH - squareSize) / 2f + _trimShift.y);

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
                GameObject answerObj = new GameObject($"answer_{y}_{x}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(UnityEngine.UI.Outline));
                answerObj.transform.SetParent(this.transform, false);
                
                GameObject cellObj = null;
                Sprite sp = null;
                
                if(!IsDummyAnswerOnly(x, y))
                {
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
                    cellObj = new GameObject($"cell_{y}_{x}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    cellObj.transform.SetParent(this.transform, false);
                }

                RectTransform parentRT = _splitImage.GetComponent<RectTransform>();
                Vector2 parentSize = parentRT.rect.size;

                // 正方形領域（UI上）の辺長
                float uiSquare = Mathf.Min(parentSize.x, parentSize.y) * (targetPercent * fixTargetPercentCellSize / 100f);

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

                // 色設定とSprite紐付け
                if(!IsDummyAnswerOnly(x, y))
                {
                    // 💡 ロードした永続的なSpriteアセットを紐付け
                    answerObj.GetComponent<Image>().sprite = sp;
                    cellObj.GetComponent<Image>().sprite = sp;
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
                gridCell.gridX = x;
                gridCell.gridY = y;
                AnswerGridPos ansPos;
                if(!IsDummyAnswerOnly(x, y))
                {
                    ansPos = cellObj.AddComponent<AnswerGridPos>();
                    ansPos.answerGrid = answerObj;
                    ansPos.x = x;
                    ansPos.y = y;
                    ansPos.InitPos = cellObj.transform.position;
                }
                else
                    continue;

                // === 6️⃣ コピー表示用オブジェクト ===
                CreateShadow(ansPos, uiCellSizeVec);
                GameObject copyObj = new GameObject("cell_copy", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(UnityEngine.UI.Outline));
                copyObj.transform.SetParent(cellObj.transform, false);
                
                RectTransform copyRT = copyObj.GetComponent<RectTransform>();
                copyRT.localPosition = new Vector3(0, 0, 10);
                copyRT.localScale = Vector3.one;
                ansPos.outLine = copyRT;
                if(!isCreative)
                    copyRT.localScale *= 1.1f;
                else
                    copyRT.localScale *= 0f;
                copyRT.sizeDelta = uiCellSizeVec;

                Image copyImg = copyObj.GetComponent<Image>();
                copyImg.sprite = sp; // 💡 ロードした永続的なSpriteアセットを紐付け
                
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
        
        // 💡 最終的に変更をUnityに保存させる
        EditorUtility.SetDirty(this.gameObject);
        AssetDatabase.SaveAssets();

        Debug.Log($"正方形分割が完了！保存先: {saveFolder}");
    }

    /// <summary>
    /// Triangle用の画像分割処理を実行
    /// 既存のGridImageSplitterTriangleクラスの処理を利用
    /// </summary>
    private void ExecuteTriangleSplitImage()
    {
        // 一時的にGridImageSplitterTriangleコンポーネントを追加
        var tempTriangle = gameObject.AddComponent<GridImageSplitterTriangle>();
        
        // 現在の設定をコピー
        CopySettingsTo(tempTriangle);
        
        // Triangle用のSplitImageを実行
        tempTriangle.SplitImage();
        
        // 一時コンポーネントを削除
        DestroyImmediate(tempTriangle);
        
        Debug.Log("Triangle用の画像分割が完了しました");
    }

    /// <summary>
    /// Hex用の画像分割処理を実行
    /// 既存のGridImageSplitterHexクラスの処理を利用
    /// </summary>
    private void ExecuteHexSplitImage()
    {
        // 一時的にGridImageSplitterHexコンポーネントを追加
        var tempHex = gameObject.AddComponent<GridImageSplitterHex>();
        
        // 現在の設定をコピー
        CopySettingsTo(tempHex);
        
        // Hex用のSplitImageを実行
        tempHex.SplitImage();
        
        // 一時コンポーネントを削除
        DestroyImmediate(tempHex);
        
        Debug.Log("Hex用の画像分割が完了しました");
    }

    /// <summary>
    /// 現在の設定を他のSplitterにコピー
    /// </summary>
    private void CopySettingsTo(AbstractGridImageSplitter target)
    {
        target.cols = this.cols;
        target.rows = this.rows;
        target._pieceNum = this._pieceNum;
        target.targetPercent = this.targetPercent;
        target.fixTargetPercentCellSize = this.fixTargetPercentCellSize;
        target.outputFolder = this.outputFolder;
        target.cellCopyMaterial = this.cellCopyMaterial;
        target._param = this._param;
        target.isSkip = this.isSkip;
        target.isPrefs = this.isPrefs;
        target.isCreative = this.isCreative;
        target.PieceCreateSeed = this.PieceCreateSeed;
        target.backUpPieceCreateSeed = this.backUpPieceCreateSeed;
        target.avoidPatternSeeds = this.avoidPatternSeeds;
        target._shadowSprite = this._shadowSprite;
        target._trimShift = this._trimShift;
        target.uniqueId = this.uniqueId;
        target.index = this.index;
        target.PrefabSavePath = this.PrefabSavePath;
    }
#endif
}
