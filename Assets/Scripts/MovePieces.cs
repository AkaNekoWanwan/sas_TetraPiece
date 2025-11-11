using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;
using Cinemachine;
using System.Linq;

public class MovePieces : MonoBehaviour
{
    public Transform selectedPiece;
    private bool isDragging = false;
    private Vector3 grabOffset;

    [Header("Boundary Settings")]
    public bool enableRealTimeBoundaryCheck = true;
    public bool enableSoftBoundary = true;
    public float softBoundaryZone = 2f;
    public float boundaryForceMultiplier = 5f;

    [Header("Movement Settings")]
    public float moveSpeed = 200f;
    public float overshootAmount = 0.3f;
    public float dampingFactor = 0.7f;
    public float liftHeight = 4f;

    [Header("Snap Settings")]
    public float snapDistance = 3.0f; // 一時的に大きくしてテスト
    public float snapAngleThreshold = 30f;
    public float snapDuration = 0.5f;
    public AnimationCurve snapCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    public PuzzleChecker puzzleChecker;
    public float maxX;
    public float minX;
    public float maxZ;
    public float minZ;
    public bool isStart = false;
    public bool isClear = false;
    public int checkBuffer;
    public FirebaseManager firebaseManager;

    private Dictionary<Transform, bool> pieceIsSnapped = new Dictionary<Transform, bool>();
    private HashSet<Transform> snappingPieces = new HashSet<Transform>();

    // 物理的な動きをシミュレート
    private Vector3 pieceVelocity = Vector3.zero;
    private Vector3 lastMousePosition = Vector3.zero;
    private Vector3 mouseVelocity = Vector3.zero;

    private int frameCounter = 0;
    public Vector3 offset;
    public CinemachineImpulseSource impulseSource;

    void Start()
    {
        snapDistance = 1.5f;
        moveSpeed = 200f;
        offset = new Vector3(0f, 0, 0f);
        snapDuration = 0.2f;
        firebaseManager = GameObject.Find("FirebaseManager").GetComponent<FirebaseManager>();
        puzzleChecker = GetComponent<PuzzleChecker>();
        impulseSource = GameObject.Find("SmallImpulseSource").GetComponent<CinemachineImpulseSource>();

        Debug.Log("[初期化] MovePieces初期化開始");

        // 各ピースの初期化
        PieceTransforms[] pieces = FindObjectsOfType<PieceTransforms>();
        Debug.Log($"[初期化] 発見されたピース数: {pieces.Length}");
        
        foreach (var pieceTransform in pieces)
        {
            Transform piece = pieceTransform.transform;
            pieceIsSnapped[piece] = false;

            Debug.Log($"[初期化] ピース {piece.name} を初期化");
                    if (pieceTransform.isDummy)
        {
            Debug.Log($"[初期化] {piece.name} はダミーピースです");
            continue;
        }



            // AnswerPieceInfoの確認
            AnswerPieceInfo answerInfo = piece.GetComponent<AnswerPieceInfo>();
            if (answerInfo == null)
            {
                Debug.LogError($"[初期化] {piece.name} にAnswerPieceInfoコンポーネントがありません！");
            }
            else if (answerInfo.answerPiece == null)
            {
                Debug.LogError($"[初期化] {piece.name} のAnswerPieceInfoにanswerPieceが設定されていません！");
            }
            else
            {
                Debug.Log($"[初期化] {piece.name} のanswerPiece: {answerInfo.answerPiece.name}");
            }

            // 全てのピースのOutlineを最初は無効に
            Outline outline = piece.GetComponent<Outline>();
            if (outline != null)
            {
                outline.enabled = false;
            }
        }

        Debug.Log("[初期化] MovePieces初期化完了");
    }

    void Update()
    {
        isStart = puzzleChecker.isStart;
        isClear = puzzleChecker.isClear;
        if (!isStart || isClear) return;
        checkBuffer++;

        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            float rayDistance = Camera.main.orthographic ? 100f : 1000f;

            if (Physics.Raycast(ray, out RaycastHit hit, rayDistance))
            {
                Debug.Log($"Raycast hit: {hit.collider.name} at {hit.point}");

                Transform targetPiece = FindSelectablePiece(hit.collider.transform);
                
                // スナップ中や既にスナップ済みのピースは選択不可
                if (targetPiece != null && !snappingPieces.Contains(targetPiece) && !pieceIsSnapped[targetPiece])
                {
                    selectedPiece = targetPiece;
                    firebaseManager.TapCount(puzzleChecker.stageName);

                    selectedPiece.DOKill();

                    Vector3 mouseWorldPos = GetMouseWorldPosition();
                    grabOffset = selectedPiece.position - mouseWorldPos;
                    grabOffset.y = 0f;
                    grabOffset+= offset; // オフセットを適用

                    lastMousePosition = mouseWorldPos;
                    mouseVelocity = Vector3.zero;
                    pieceVelocity = Vector3.zero;

                    // ピースを持ち上げる（正解角度に修正しながら）
                    LiftPiece(selectedPiece);

                    // アウトラインを有効にする
                    EnableOutlineWithAnimation(selectedPiece);

                    isDragging = true;

                    Debug.Log($"選択したピース: {selectedPiece.name}");
                }
                else if (targetPiece != null)
                {
                    Debug.Log($"{targetPiece.name} は選択できません（スナップ中またはスナップ済み）");
                }
            }
        }

        if (Input.GetMouseButtonUp(0))
        {
            if (selectedPiece != null)
            {
                // アウトラインを無効にする
                DisableOutlineWithAnimation(selectedPiece);

                // ピースを落下させる
                DropPieceWithPhysics(selectedPiece);
            }

            isDragging = false;
            selectedPiece = null;
            pieceVelocity = Vector3.zero;
        }

        // ドラッグ中の移動処理
        if (isDragging && selectedPiece != null)
        {
            Vector3 currentMousePos = GetMouseWorldPosition();
            Vector3 targetPos = currentMousePos + grabOffset;

            mouseVelocity = (currentMousePos - lastMousePosition) / Time.deltaTime;
            lastMousePosition = currentMousePos;

            Vector3 currentPiecePos = selectedPiece.position;
            currentPiecePos.y = liftHeight; // 選択中は常にliftHeight

            Vector3 toTarget = targetPos - currentPiecePos;
            toTarget.y = 0;

            Vector3 overshootForce = mouseVelocity * overshootAmount;
            Vector3 springForce = toTarget * moveSpeed;
            Vector3 totalForce = springForce + overshootForce;

            pieceVelocity += totalForce * Time.deltaTime;
            pieceVelocity *= dampingFactor;

            Vector3 newPos = currentPiecePos + pieceVelocity * Time.deltaTime;
            newPos.y = liftHeight; // 選択中は常にliftHeight

            // 境界チェックを適用
            if (enableRealTimeBoundaryCheck)
            {
                newPos = ApplyBoundaryConstraints(newPos);
            }

            selectedPiece.position = newPos;
        }

        // 境界チェック
        if (frameCounter % 10 == 0)
        {
            CheckBounds();
        }
        frameCounter++;
    }
    // answerPieceのスケールを取得するヘルパーメソッド
Vector3 GetAnswerScale(Transform piece)
{
    AnswerPieceInfo answerInfo = piece.GetComponent<AnswerPieceInfo>();
    if (answerInfo == null)
    {
        Debug.LogWarning($"[スケール取得] {piece.name} にAnswerPieceInfoコンポーネントがありません");
        return Vector3.zero;
    }

    if (answerInfo.answerPiece == null)
    {
        Debug.LogWarning($"[スケール取得] {piece.name} のAnswerPieceInfoにanswerPieceが設定されていません");
        return Vector3.zero;
    }

    Vector3 answerScale = answerInfo.answerPiece.transform.localScale;
    Debug.Log($"[スケール取得] {piece.name} の正解スケール: {answerScale}");
    return answerScale;
}

    // ピースを持ち上げて正解角度に修正
    // void LiftPiece(Transform piece)
    // {
    //     // 正解角度を取得
    //     Quaternion answerRotation = puzzleChecker.GetAnswerRotation(piece);

    //     // 高さを上げて角度を修正
    //     Vector3 currentPos = piece.position;
    //     currentPos.y = liftHeight;
    //     piece.position = currentPos;

    //     // 正解角度に滑らかに回転
    //     piece.DORotateQuaternion(answerRotation, 0.3f).SetEase(Ease.OutQuad);

    //     // Rigidbodyをkinematicにして物理演算を無効化
    //     Rigidbody rb = piece.GetComponent<Rigidbody>();
    //     if (rb != null)
    //     {
    //         rb.isKinematic = true;
    //         rb.velocity = Vector3.zero;
    //         rb.angularVelocity = Vector3.zero;
    //     }
    // }
void LiftPiece(Transform piece)
{
    // 正解角度を取得
    Quaternion answerRotation = puzzleChecker.GetAnswerRotation(piece);

    // AnswerPieceInfoから正解スケールを取得
    Vector3 answerScale = GetAnswerScale(piece);

    // 高さを上げて角度を修正
    Vector3 currentPos = piece.position;
    currentPos.y = liftHeight;
    piece.position = currentPos;

    // 正解角度に滑らかに回転
    piece.DORotateQuaternion(answerRotation, 0.3f).SetEase(Ease.OutQuad);

    // 正解スケールに滑らかに変更
    if (answerScale != Vector3.zero) // answerPieceが設定されている場合のみ
    {
        piece.DOScale(answerScale, 0.3f).SetEase(Ease.OutQuad);
        Debug.Log($"[スケール調整] {piece.name} のスケールを {piece.localScale} から {answerScale} に変更");
    }

    // Rigidbodyをkinematicにして物理演算を無効化
    Rigidbody rb = piece.GetComponent<Rigidbody>();
    if (rb != null)
    {
        rb.isKinematic = true;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }
}
    // 境界制限を適用する
    Vector3 ApplyBoundaryConstraints(Vector3 targetPosition)
    {
        Vector3 constrainedPos = targetPosition;

        if (enableSoftBoundary)
        {
            // ソフト境界：境界に近づくと徐々に抵抗が増す
            float softMinX = minX + softBoundaryZone;
            float softMaxX = maxX - softBoundaryZone;
            float softMinZ = minZ + softBoundaryZone;
            float softMaxZ = maxZ - softBoundaryZone;

            // X軸の境界処理
            if (constrainedPos.x < softMinX)
            {
                float penetration = softMinX - constrainedPos.x;
                float resistance = Mathf.Clamp01(penetration / softBoundaryZone);
                float pushBack = resistance * resistance * boundaryForceMultiplier;
                constrainedPos.x += pushBack * Time.deltaTime;
                pieceVelocity.x *= (1f - resistance * 0.9f);
            }
            else if (constrainedPos.x > softMaxX)
            {
                float penetration = constrainedPos.x - softMaxX;
                float resistance = Mathf.Clamp01(penetration / softBoundaryZone);
                float pushBack = resistance * resistance * boundaryForceMultiplier;
                constrainedPos.x -= pushBack * Time.deltaTime;
                pieceVelocity.x *= (1f - resistance * 0.9f);
            }

            // Z軸の境界処理
            if (constrainedPos.z < softMinZ)
            {
                float penetration = softMinZ - constrainedPos.z;
                float resistance = Mathf.Clamp01(penetration / softBoundaryZone);
                float pushBack = resistance * resistance * boundaryForceMultiplier;
                constrainedPos.z += pushBack * Time.deltaTime;
                pieceVelocity.z *= (1f - resistance * 0.9f);
            }
            else if (constrainedPos.z > softMaxZ)
            {
                float penetration = constrainedPos.z - softMaxZ;
                float resistance = Mathf.Clamp01(penetration / softBoundaryZone);
                float pushBack = resistance * resistance * boundaryForceMultiplier;
                constrainedPos.z -= pushBack * Time.deltaTime;
                pieceVelocity.z *= (1f - resistance * 0.9f);
            }
        }

        // ハード境界：絶対に超えてはいけない境界
        constrainedPos.x = Mathf.Clamp(constrainedPos.x, minX, maxX);
        constrainedPos.z = Mathf.Clamp(constrainedPos.z, minZ, maxZ);

        // 境界に到達した場合は速度をリセット
        if (constrainedPos.x <= minX || constrainedPos.x >= maxX)
        {
            pieceVelocity.x = 0f;
        }
        if (constrainedPos.z <= minZ || constrainedPos.z >= maxZ)
        {
            pieceVelocity.z = 0f;
        }

        return constrainedPos;
    }

    // ヒットしたコライダーから選択可能なピースを探す
    Transform FindSelectablePiece(Transform hitTransform)
    {
        // 1. PieceTransformsコンポーネントを持つオブジェクトかチェック
        var pieceTransform = hitTransform.GetComponent<PieceTransforms>();
        if (pieceTransform != null)
        {
            return pieceTransform.transform;
        }

        // 2. PieceTransformsコンポーネントがない場合、親階層を辿って探す
        Transform current = hitTransform.parent;
        while (current != null)
        {
            var parentPieceTransform = current.GetComponent<PieceTransforms>();
            if (parentPieceTransform != null)
            {
                return parentPieceTransform.transform;
            }
            current = current.parent;
        }

        return null;
    }

    // 通常の物理落下処理
    void DropPieceWithPhysics(Transform piece)
    {
        Rigidbody rb = piece.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;

            // Y軸の制約を解除して落下できるようにする
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

            // 少し下向きの初速を与えて自然な落下を演出
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, -30f, rb.linearVelocity.z);
        }

        // 地面に着地したら適切な制約に戻すためのコルーチンを開始
        StartCoroutine(WaitForLanding(piece));
    }

    // 着地を待つコルーチン
    System.Collections.IEnumerator WaitForLanding(Transform piece)
    {
        Debug.Log($"[着地待機] {piece.name} の着地を待機開始");
        
        Rigidbody rb = piece.GetComponent<Rigidbody>();
        if (rb == null) 
        {
            Debug.LogError($"[着地待機] {piece.name} にRigidbodyがありません");
            yield break;
        }

        float timeoutCounter = 0f;
        float maxWaitTime = 5f; // 最大5秒まで待機

        // 着地するまで待機（速度が十分小さくなるまで）
        while (rb != null && !rb.isKinematic && Mathf.Abs(rb.linearVelocity.y) > 0.4f && timeoutCounter < maxWaitTime)
        {
            timeoutCounter += Time.fixedDeltaTime;
            Debug.Log($"[着地待機] {piece.name}: Y速度={rb.linearVelocity.y:F2}, 待機時間={timeoutCounter:F1}秒");
            yield return new WaitForFixedUpdate();
        }

        if (timeoutCounter >= maxWaitTime)
        {
            Debug.LogWarning($"[着地待機] {piece.name} の着地待機がタイムアウトしました");
        }

        // 着地後の処理
        if (rb != null && !rb.isKinematic)
        {
            rb.constraints = RigidbodyConstraints.None;
            Debug.Log($"[着地完了] {piece.name} が着地しました。位置: {piece.position}");

            // 着地後にスナップ判定を実行
            yield return new WaitForSeconds(0.1f); // 少し待ってから判定
            CheckSnapAfterLanding(piece);
        }
        else
        {
            Debug.LogWarning($"[着地完了] {piece.name} のRigidbodyが無効またはkinematicです");
        }
    }

    // 着地後のスナップ判定
    void CheckSnapAfterLanding(Transform piece)
    {
           PieceTransforms pieceComponent = piece.GetComponent<PieceTransforms>();
    if (pieceComponent != null && pieceComponent.isDummy)
    {
        Debug.Log($"[スナップ判定] {piece.name}: ダミーピースのためスナップ判定をスキップ");
        return;
    }
        if (snappingPieces.Contains(piece) || pieceIsSnapped[piece])
        {
            Debug.Log($"[スナップ判定] {piece.name}: スナップ中またはスナップ済みのためスキップ");
            return;
        }

        Debug.Log($"[スナップ判定] {piece.name} の着地後スナップ判定を開始");

        Vector3 answerPosition;
        Quaternion answerRotation;
        
        if (puzzleChecker.IsNearAnswerPosition(piece, out answerPosition, out answerRotation))
        {
            Debug.Log($"[スナップ実行] {piece.name} を正解位置に吸い寄せます");
            SnapToAnswerPosition(piece, answerPosition, answerRotation);
        }
        else
        {
            Debug.Log($"[スナップ判定] {piece.name} は正解位置から遠いためスナップしません");
        }
    }

    // 正解位置にスナップする
    void SnapToAnswerPosition(Transform piece, Vector3 answerPosition, Quaternion answerRotation)
    {
        if (snappingPieces.Contains(piece)) return;

        snappingPieces.Add(piece);

        // Rigidbodyを無効化
        Rigidbody rb = piece.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // 吸い寄せアニメーション
        piece.DOMove(answerPosition, snapDuration).SetEase(Ease.OutQuart);
        piece.DORotateQuaternion(answerRotation, snapDuration).SetEase(Ease.OutQuart)
            .OnComplete(() =>
            {
                // スナップ完了
                pieceIsSnapped[piece] = true;
                snappingPieces.Remove(piece);

                // エフェクト
                impulseSource.GenerateImpulse();
                
                // スケールアニメーション
                var originalScale = piece.localScale;
                piece.DOScale(originalScale * 1.1f, 0.1f).SetEase(Ease.OutQuad)
                    .OnComplete(() =>
                    {
                        GameObject.Find("SoundManager").GetComponent<SoundManager>().PlaySound();
                        piece.DOScale(originalScale, 0.1f).SetEase(Ease.InQuad);
                    });

                Debug.Log($"ピース {piece.name} が正解位置にスナップしました");
            });
    }

    void EnableOutlineWithAnimation(Transform piece)
    {
        Outline outline = piece.GetComponent<Outline>();
        if (outline != null)
        {
            outline.enabled = true;
            outline.OutlineWidth = 0f;
            outline.OutlineColor = Color.white;

            // widthを0から10fに滑らかに変化
            DOTween.To(() => outline.OutlineWidth, x => outline.OutlineWidth = x, 10f, 0.3f)
                .SetEase(Ease.OutQuad);
        }
    }

    void DisableOutlineWithAnimation(Transform piece)
    {
        Outline outline = piece.GetComponent<Outline>();
        if (outline != null)
        {
            // widthを10fから0に滑らかに変化してから無効化
            DOTween.To(() => outline.OutlineWidth, x => outline.OutlineWidth = x, 0f, 0.3f)
                .SetEase(Ease.InQuad)
                .OnComplete(() =>
                {
                    outline.enabled = false;
                });
        }
    }

    private Vector3 GetMouseWorldPosition()
    {
        Vector3 mousePos = Input.mousePosition;

        if (Camera.main.orthographic)
        {
            mousePos.z = Camera.main.nearClipPlane + 1f;
        }
        else
        {
            mousePos.z = Camera.main.transform.position.y;
        }

        Vector3 worldPos = Camera.main.ScreenToWorldPoint(mousePos);
        worldPos.y = 0f;
        return worldPos;
    }

    void CheckBounds()
    {
        PieceTransforms[] pieces = FindObjectsOfType<PieceTransforms>();
        foreach (var pieceComponent in pieces)
        {
            Transform piece = pieceComponent.transform;
            if (piece == null || pieceIsSnapped[piece]) continue;

            Vector3 pos = piece.position;
            if (pos.x < minX || pos.x > maxX || pos.z > maxZ || pos.z < minZ)
            {
                Debug.LogWarning($"ピース {piece.name} が境界外にあります: {pos}");
                MovePieceBackToSafePosition(piece);
            }
        }
    }

    void MovePieceBackToSafePosition(Transform piece)
    {
        // 境界内の安全な位置に移動
        Vector3 safePos = piece.position;
        safePos.x = Mathf.Clamp(safePos.x, minX + 1f, maxX - 1f);
        safePos.z = Mathf.Clamp(safePos.z, minZ + 1f, maxZ - 1f);
        safePos.y = 0f;

        // Rigidbodyがある場合は物理演算で戻す
        Rigidbody rb = piece.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            piece.position = safePos;
            rb.isKinematic = false;
        }
        else
        {
            // フォールバック：DOTweenで移動
            piece.DOMove(safePos, 0.2f).SetEase(Ease.OutQuad);
        }
    }
}