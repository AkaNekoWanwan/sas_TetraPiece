using UnityEngine;
using System.Collections;
using DG.Tweening;
using Cinemachine;

// 各ピースに個別にアタッチする隠れピース表示管理スクリプト
public class IndividualPieceRescue : MonoBehaviour
{
    [Header("Outline Settings")]
    public float outlineCheckInterval;
    public float hiddenThreshold = 1f; // 隠れていると判定する閾値（秒）
    public bool enableOutline = true; // アウトライン機能のON/OFF

    [Header("Outline Visual Settings")]
    public Color hiddenOutlineColor;
        public float hiddenOutlineWidth = 8f; // 隠れている時のアウトライン幅
    
    // プライベート変数
    private float hiddenTime = 0f;
    private bool isCurrentlyHidden = false;
    private Camera mainCamera;
    private Transform myTransform;
    private Renderer myRenderer;
    private PieceTransforms myPieceTransforms;
    private Outline outline;
    
    // アウトラインの元の設定を保存
    private bool originalOutlineEnabled;
    private Color originalOutlineColor;
    private float originalOutlineWidth;
    
    // 外部参照（PuzzleCheckerから設定される）
    private MovePieces movePieces;
    private PuzzleChecker puzzleChecker;
    
    void Awake()
    {
        hiddenOutlineColor = Color.gray;
        myTransform = transform;
        myRenderer = GetComponent<Renderer>();
        myPieceTransforms = GetComponent<PieceTransforms>();
        outline = GetComponent<Outline>();
        outlineCheckInterval = 0.5f;
        // アウトラインの元の設定を保存
        if (outline != null)
        {
            originalOutlineEnabled = outline.enabled;
            originalOutlineColor = outline.OutlineColor;
            originalOutlineWidth = outline.OutlineWidth;
        }
    }
    
    void Start()
    {
        mainCamera = Camera.main;
        
        // 定期チェック開始
        if (enableOutline)
        {
            InvokeRepeating(nameof(CheckIfHidden), outlineCheckInterval, outlineCheckInterval);
        }
    }
    
    // 外部参照を設定するメソッド（PuzzleCheckerから呼ばれる）
    public void SetReferences(MovePieces moveP, PuzzleChecker puzzleC)
    {
        movePieces = moveP;
        puzzleChecker = puzzleC;
    }
    
    // このピースが隠れているかチェック
    void CheckIfHidden()
    {
        if (!enableOutline) return;
        if (puzzleChecker != null && (!puzzleChecker.isStart || puzzleChecker.isClear)) return;
        if (mainCamera == null) return;
        
        // 子オブジェクト化されたピースや選択中のピースはスキップ
        if (IsChildPiece() || IsSelectedPiece()) return;
        
        bool isVisible = IsVisibleFromCamera();
        
        if (!isVisible)
        {
            hiddenTime += outlineCheckInterval;
            
            // 一定時間隠れていたらアウトラインを表示
            if (hiddenTime >= hiddenThreshold && !isCurrentlyHidden)
            {
                ShowHiddenOutline();
                isCurrentlyHidden = true;
            }
        }
        else
        {
            // 見えている場合は時間をリセットし、アウトラインを元に戻す
            hiddenTime = 0f;
            if (isCurrentlyHidden)
            {
                RestoreOriginalOutline();
                isCurrentlyHidden = false;
            }
        }
    }
    
    // カメラからこのピースが見えるかどうかをチェック
    bool IsVisibleFromCamera()
    {
        if (mainCamera == null || myRenderer == null) return true;

        return true;
    }
    
    // 隠れピース用のアウトラインを表示
    void ShowHiddenOutline()
    {
        Debug.Log($"🔍 隠れピース検出: {name} - アウトライン表示");
        
        if (outline != null)
        {
            outline.enabled = true;
            outline.OutlineColor = hiddenOutlineColor;
            outline.OutlineWidth = hiddenOutlineWidth;
        }
    }
    
    // アウトラインを元の設定に戻す
    void RestoreOriginalOutline()
    {
        Debug.Log($"✅ ピース表示復帰: {name} - アウトライン元に戻す");
        
        if (outline != null)
        {
            outline.enabled = false;
            outline.OutlineColor = originalOutlineColor;
            outline.OutlineWidth = originalOutlineWidth;
        }
    }
    
    // 子オブジェクトかどうかをチェック
    bool IsChildPiece()
    {
        // 親にPieceTransformsがあるかチェック
        Transform parent = myTransform.parent;
        while (parent != null)
        {
            PieceTransforms parentPieceTransform = parent.GetComponent<PieceTransforms>();
            if (parentPieceTransform != null)
            {
                return true;
            }
            parent = parent.parent;
        }
        return false;
    }
    
    // 選択中のピースかどうかをチェック
    bool IsSelectedPiece()
    {
        if (movePieces == null) return false;
        return movePieces.selectedPiece == myTransform;
    }
    
    // 手動でアウトラインを表示（デバッグ用）
    [ContextMenu("隠れピースアウトライン表示")]
    public void ManualShowOutline()
    {
        if (enableOutline)
        {
            ShowHiddenOutline();
            isCurrentlyHidden = true;
        }
    }
    
    // 手動でアウトラインを元に戻す（デバッグ用）
    [ContextMenu("アウトライン元に戻す")]
    public void ManualRestoreOutline()
    {
        RestoreOriginalOutline();
        isCurrentlyHidden = false;
        hiddenTime = 0f;
    }
    
    // アウトライン機能の有効/無効を切り替え
    public void SetOutlineEnabled(bool enabled)
    {
        enableOutline = enabled;
        
        if (enabled)
        {
            InvokeRepeating(nameof(CheckIfHidden), outlineCheckInterval, outlineCheckInterval);
        }
        else
        {
            CancelInvoke(nameof(CheckIfHidden));
            // 無効にする際は元のアウトラインに戻す
            if (isCurrentlyHidden)
            {
                RestoreOriginalOutline();
                isCurrentlyHidden = false;
            }
        }
    }
    
    void OnDestroy()
    {
        CancelInvoke();
    }
}