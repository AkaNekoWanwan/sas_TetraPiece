using UnityEngine;

/// <summary>
/// 現在ドラッグ中のPieceDragControllerを管理する静的クラス
/// HandCursorControllerなど他のシステムから参照可能
/// </summary>
public static class DragStateManager
{
    /// <summary>
    /// 現在ドラッグ中のPieceDragController
    /// </summary>
    public static PieceDragController CurrentDraggingPiece { get; private set; }

    /// <summary>
    /// 現在ドラッグ中のピースのRectTransform
    /// </summary>
    public static RectTransform CurrentDraggingTransform
    {
        get => CurrentDraggingPiece != null ? CurrentDraggingPiece.GetComponent<RectTransform>() : null;
    }

    /// <summary>
    /// 現在ドラッグ中のピースのCanvas
    /// </summary>
    public static Canvas CurrentDraggingCanvas
    {
        get => CurrentDraggingPiece != null ? CurrentDraggingPiece.GetComponentInParent<Canvas>() : null;
    }

    /// <summary>
    /// 現在ドラッグ中のピースのキャンバスのカメラ
    /// </summary>
    public static Camera CurrentDraggingCamera
    {
        get
        {
            var canvas = CurrentDraggingCanvas;
            if (canvas == null) return null;
            return canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        }
    }

    /// <summary>
    /// 何かがドラッグ中かどうか
    /// </summary>
    public static bool IsDragging => CurrentDraggingPiece != null;

    /// <summary>
    /// 現在ドラッグ中のマウス位置（スクリーン座標）
    /// </summary>
    public static Vector2 CurrentDragMousePosition
    {
        get => CurrentDraggingPiece != null ? CurrentDraggingPiece.CurrentDragScreenPosition : Vector2.zero;
    }

    /// <summary>
    /// ドラッグ開始を登録
    /// </summary>
    public static void RegisterDrag(PieceDragController piece)
    {
        CurrentDraggingPiece = piece;
    }

    /// <summary>
    /// ドラッグ終了を登録
    /// </summary>
    public static void UnregisterDrag(PieceDragController piece)
    {
        if (CurrentDraggingPiece == piece)
        {
            CurrentDraggingPiece = null;
        }
    }

    /// <summary>
    /// 強制的に全てクリア（シーン遷移時など）
    /// </summary>
    public static void Clear()
    {
        CurrentDraggingPiece = null;
    }

    /// <summary>
    /// ドラッグ中のピースの位置を異なるキャンバスのローカル座標に変換
    /// </summary>
    /// <param name="targetRect">変換先のRectTransform（通常はHandCursorの親）</param>
    /// <param name="targetCamera">変換先のキャンバスのカメラ</param>
    /// <param name="localPos">変換後のローカル座標</param>
    /// <returns>変換に成功したかどうか</returns>
    public static bool GetDraggedPieceLocalPosition(RectTransform targetRect, Camera targetCamera, out Vector2 localPos)
    {
        localPos = Vector2.zero;
        
        if (!IsDragging || CurrentDraggingTransform == null || targetRect == null)
            return false;

        // Step 1: ドラッグ中のピースのワールド座標を取得
        Vector3 pieceWorldPos = CurrentDraggingTransform.position;

        // Step 2: ピース側のキャンバス設定でワールド座標→スクリーン座標に変換
        Vector3 screenPos;
        var dragCamera = CurrentDraggingCamera;
        
        if (dragCamera != null)
        {
            // Camera空間の場合
            screenPos = dragCamera.WorldToScreenPoint(pieceWorldPos);
        }
        else
        {
            // Overlay空間の場合（既にスクリーン座標相当）
            screenPos = pieceWorldPos;
        }

        // Step 3: スクリーン座標→ターゲット側のローカル座標に変換
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            targetRect, 
            screenPos, 
            targetCamera, 
            out localPos);
    }

    /// <summary>
    /// ドラッグ中のマウス位置（スクリーン座標）を異なるキャンバスのローカル座標に変換
    /// こちらはピースの実際の位置ではなく、ユーザーがドラッグしているマウス位置を使用
    /// </summary>
    /// <param name="targetRect">変換先のRectTransform（通常はHandCursorの親）</param>
    /// <param name="targetCamera">変換先のキャンバスのカメラ</param>
    /// <param name="localPos">変換後のローカル座標</param>
    /// <returns>変換に成功したかどうか</returns>
    public static bool GetDragMouseLocalPosition(RectTransform targetRect, Camera targetCamera, out Vector2 localPos)
    {
        localPos = Vector2.zero;
        
        if (!IsDragging || targetRect == null)
            return false;

        // ドラッグ中のマウス位置（スクリーン座標）を直接使用
        Vector2 mouseScreenPos = CurrentDragMousePosition;

        // スクリーン座標→ターゲット側のローカル座標に変換
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            targetRect, 
            mouseScreenPos, 
            targetCamera, 
            out localPos);
    }
}
