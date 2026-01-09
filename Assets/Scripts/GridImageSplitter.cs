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
        SplitImageSquareOptimized();
    }

    /// <summary>
    /// Square用の最適化されたSplitImage処理（Addressable化統合版）
    /// </summary>
    private void SplitImageSquareOptimized()
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

        // Addressableグループの準備
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
        int squareSize = Mathf.RoundToInt(Mathf.Min(fullW, fullH) * (targetPercent / 100f));
        int startX = (int)(rect.x + (fullW - squareSize) / 2f + _trimShift.x);
        int startY = (int)(rect.y + (fullH - squareSize) / 2f + _trimShift.y);
        int cellSize = Mathf.RoundToInt(squareSize / Mathf.Max(rows, cols));
        int usedWidth = cellSize * cols;
        int usedHeight = cellSize * rows;
        int offsetX = startX + (squareSize - usedWidth) / 2;
        int offsetY = startY + (squareSize - usedHeight) / 2;

        List<(int x, int y, string assetPath)> importList = new List<(int x, int y, string assetPath)>();
        List<(GameObject obj, string assetPath)> objectsToAddressable = new List<(GameObject, string)>();

        int initY = 0, initX = 0, targetRows = rows, targetCols = cols;
        if(isCreative)
        {
            initY = -2; initX = -2;
            targetRows += 2; targetCols += 2;
        }

        // === テクスチャ保存 ===
        for (int y = initY; y < targetRows; y++)
        {
            for (int x = initX; x < targetCols; x++)
            {
                if(IsDummyAnswerOnly(x, y)) continue;

                int px = offsetX + x * cellSize;
                int py = offsetY + y * cellSize;
                
                if (px + cellSize > srcTex.width || py + cellSize > srcTex.height || px < 0 || py < 0)
                    continue;

                string fileName = $"grid_{y}_{x}.png";
                string assetPath = $"{saveDirectory}/{fileName}";

                Color[] pixels = srcTex.GetPixels(px, py, cellSize, cellSize);
                Texture2D newTex = new Texture2D(cellSize, cellSize, TextureFormat.RGBA32, false);
                newTex.SetPixels(pixels);
                newTex.Apply();

                File.WriteAllBytes(assetPath, newTex.EncodeToPNG());
                importList.Add((x, y, assetPath));
                Object.DestroyImmediate(newTex);
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
        float uiCellSize = uiSquare / Mathf.Max(rows, cols);
        Vector2 uiCellSizeVec = new Vector2(uiCellSize, uiCellSize);
        SetCellScale(uiCellSize);

        int rowCount = isCreative ? rows + 2 : rows;
        int colCount = isCreative ? cols + 2 : cols;
        
        for (int y = initY; y < rowCount; y++)
        {
            for (int x = initX; x < colCount; x++)
            {
                string fileName = $"grid_{y}_{x}.png";
                string assetPath = $"{saveDirectory}/{fileName}";
                
                GameObject answerObj = new GameObject($"answer_{y}_{x}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(UnityEngine.UI.Outline));
                answerObj.transform.SetParent(this.transform, false);
                
                GameObject cellObj = null;
                Sprite sp = null;
                
                if(!IsDummyAnswerOnly(x, y))
                {
                    sp = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                    if (sp == null)
                    {
                        Debug.LogError($"Spriteロード失敗: {assetPath}");
                        continue;
                    }
                    
                    cellObj = new GameObject($"cell_{y}_{x}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    cellObj.transform.SetParent(this.transform, false);
                }

                // RectTransform設定
                float offsetUiX = (x - (cols - 1) * 0.5f) * uiCellSize;
                float offsetUiY = (y - (rows - 1) * 0.5f) * uiCellSize;

                RectTransform ansRT = answerObj.GetComponent<RectTransform>();
                ansRT.sizeDelta = uiCellSizeVec;
                ansRT.anchoredPosition = new Vector2(offsetUiX, offsetUiY);

                if(!IsDummyAnswerOnly(x, y))
                {
                    RectTransform cellRT = cellObj.GetComponent<RectTransform>();
                    cellRT.sizeDelta = uiCellSizeVec;
                    cellRT.anchoredPosition = new Vector2(offsetUiX, offsetUiY);
                    cellRT.SetAsFirstSibling();
                }

                // ★ GridCellBatchUpdaterと同じ設定を適用
                SplitImageHelper.ApplyStandardGridCellSettings(answerObj, _param);
                
                if(!IsDummyAnswerOnly(x, y))
                {
                    Image cellImg = cellObj.GetComponent<Image>();
                    cellImg.sprite = sp;
                    Image answerImg = answerObj.GetComponent<Image>();
                    answerImg.sprite = sp;
                    if(_param != null) cellImg.material = _param.CellsMaterial;
                    
                    // AddressableImageLoaderを生成時に追加
                    AddressableImageLoader loader = cellObj.AddComponent<AddressableImageLoader>();
                    loader.addressName = assetPath;
                }
                else
                {
                    continue;
                }

                // コンポーネント追加
                GridCell gridCell = answerObj.AddComponent<GridCell>();
                gridCell.gridX = x;
                gridCell.gridY = y;
                
                AnswerGridPos ansPos = cellObj.AddComponent<AnswerGridPos>();
                ansPos.answerGrid = answerObj;
                ansPos.x = x;
                ansPos.y = y;
                ansPos.InitPos = cellObj.transform.position;

                CreateShadow(ansPos, uiCellSizeVec);
                
                // コピー表示用オブジェクト
                GameObject copyObj = new GameObject("cell_copy", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(UnityEngine.UI.Outline));
                copyObj.transform.SetParent(cellObj.transform, false);
                
                RectTransform copyRT = copyObj.GetComponent<RectTransform>();
                copyRT.localPosition = new Vector3(0, 0, 10);
                copyRT.localScale = isCreative ? Vector3.zero : Vector3.one * 1.1f;
                ansPos.outLine = copyRT;
                copyRT.sizeDelta = uiCellSizeVec;

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
        
        Debug.Log($"✅ Square画像分割完了（Addressable統合版）: {groupName}");
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
