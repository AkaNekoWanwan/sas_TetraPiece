using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;
using System.Collections;
using Cinemachine;
using MoreMountains.Feedbacks;
using Unity.VisualScripting;
using UnityEngine.UI;

public class PuzzleChecker : MonoBehaviour
{
    public float positionThreshold = 1.0f;
    public float rotationThreshold = 15f; // 角度の許容範囲（度）
    public GameObject zoomCam;
    public string stageName;
    public bool isStart = false;
    public Sprite stagePic;

    public bool isClear = false;
    public StageManager stageManager;
    public MovePieces movePieces;
    public List<PieceTransforms> piecesx;
    public ParticleSystem ps;
    public CinemachineImpulseSource impulseSource;
    public MMF_Player feedbackPlayer;
    public FillGaugeController fg;

    private Dictionary<Transform, bool> pieceCompletionStatus = new Dictionary<Transform, bool>();
    
    public int buffer;
    public float targetValue;
    public float posValue;
    public CelebrationAnimationController celebrationAnimationController;
    void Start()
    {
        positionThreshold = 2f;
        movePieces = GetComponent<MovePieces>();
        feedbackPlayer = GetComponent<MMF_Player>();
        impulseSource = GameObject.Find("ImpulseSource").GetComponent<CinemachineImpulseSource>();
        stageManager = GameObject.Find("StageManager").GetComponent<StageManager>();
        piecesx = new List<PieceTransforms>(GetComponentsInChildren<PieceTransforms>());
        ps = GameObject.Find("ClearEffect").GetComponent<ParticleSystem>();

        // 各ピースの初期化
        foreach (var piece in piecesx)
        {
            Transform pieceTransform = piece.transform;

            // Rigidbodyの設定
            Rigidbody rb = pieceTransform.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = pieceTransform.gameObject.AddComponent<Rigidbody>();
            }
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.mass = 1f;
            rb.linearDamping = 1f;
            rb.angularDamping = 0.8f;
              if (piece.isDummy)
        {
            Debug.Log($"[初期化] {piece.name} はダミーピースです");
            continue;
        }

            // AnswerPieceInfoの確認
            AnswerPieceInfo answerInfo = piece.GetComponent<AnswerPieceInfo>();
            if (answerInfo == null)
            {
                Debug.LogError($"ピース {piece.name} にAnswerPieceInfoコンポーネントがありません！");
            }
            else if (answerInfo.answerPiece == null)
            {
                Debug.LogError($"ピース {piece.name} のAnswerPieceInfoにanswerPieceが設定されていません！");
            }

            // 完了状態を初期化
            pieceCompletionStatus[pieceTransform] = false;

            IndividualPieceRescue rescueComponent = piece.gameObject.AddComponent<IndividualPieceRescue>();
        }

        // stageManager.FadeInTransparentObjects();
        isStart = true;
    }

    void Update()
    {
        if (!isStart || isClear) return;

        // 各ピースの正解位置との距離をチェック
        CheckPieceCompletion();

        // クリア判定
        CheckGameCompletion();
    }

    void CheckPieceCompletion()
    {
        posValue = 0f;
        targetValue = piecesx.Count;

        foreach (var piece in piecesx)
        {
                    if (piece.isDummy) continue;

            AnswerPieceInfo answerInfo = piece.GetComponent<AnswerPieceInfo>();
            if (answerInfo == null || answerInfo.answerPiece == null) continue;

            Transform pieceTransform = piece.transform;
            Transform answerTransform = answerInfo.answerPiece.transform;

            // 位置と角度の差を計算
            float positionDistance = Vector3.Distance(pieceTransform.position, answerTransform.position);
            float rotationDistance = Quaternion.Angle(pieceTransform.rotation, answerTransform.rotation);

            // 完了判定
            bool isComplete = positionDistance <= positionThreshold && rotationDistance <= rotationThreshold;
            
            if (isComplete)
            {
                posValue += 1f;
                pieceCompletionStatus[pieceTransform] = true;
            }
            else
            {
                pieceCompletionStatus[pieceTransform] = false;
            }

            // デバッグ情報
            if (piece.name.Contains("Debug")) // デバッグ用のピースの場合
            {
                Debug.Log($"{piece.name}: Pos距離={positionDistance:F2}, Rot距離={rotationDistance:F2}, 完了={isComplete}");
            }
        }
    }

    void CheckGameCompletion()
    {
        // 全てのピースが完了しているかチェック
        bool allComplete = true;
        foreach (var piece in piecesx)
        {
                    if (piece.isDummy) continue;

            if (!pieceCompletionStatus.ContainsKey(piece.transform) || !pieceCompletionStatus[piece.transform])
            {
                allComplete = false;
                break;
            }
        }

        if (allComplete)
        {
            ClearAnimation();
            Debug.Log("🎉 ゲームクリア！全てのピースが正解位置に配置されました");
        }
    }

    public void ClearAnimation()
    {
        if (isClear) return;
        
        if (fg)
        {
            fg.fillGauge.gameObject.SetActive(false);
        }
    if(celebrationAnimationController != null)
        {
            celebrationAnimationController.StartAnimation();
        }
        // 全てのピースのアニメーションを停止
        foreach (var piece in piecesx)
        {
            piece.transform.DOKill();
            
            if (piece.gameObject.GetComponent<Rigidbody>() != null)
            {
                Destroy(piece.gameObject.GetComponent<Rigidbody>());
            }

            // 正解位置に最終調整
            AnswerPieceInfo answerInfo = piece.GetComponent<AnswerPieceInfo>();
            if (answerInfo != null && answerInfo.answerPiece != null)
            {
                piece.transform.DOMove(answerInfo.answerPiece.transform.position, 0.5f).SetEase(Ease.InOutQuad).SetLink(piece.gameObject);
                piece.transform.DORotateQuaternion(answerInfo.answerPiece.transform.rotation, 0.5f).SetEase(Ease.InOutQuad).SetLink(piece.gameObject);
            }
        }

        impulseSource.GenerateImpulse();
        if (buffer == 0)
        {
            buffer = 1;
            stageManager.ClearTrigger();
        }
        isClear = true;
        
        // パズル全体のスケールアニメーション
        var iniSca = this.transform.localScale;
        this.transform.DOScaleY(iniSca.y * 0.85f, 0.15f).SetEase(Ease.OutQuad).SetLink(this.gameObject);
        this.transform.DOScaleX(iniSca.x * 1.2f, 0.15f)
            .SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                this.transform.DOScaleY(iniSca.y * 1.15f, 0.065f).SetEase(Ease.OutQuad).SetLink(this.gameObject);
                this.transform.DOScaleX(iniSca.x * 0.85f, 0.065f).SetEase(Ease.OutQuad).OnComplete(() =>
                {
                    this.transform.DOScale(iniSca, 0.05f).SetEase(Ease.OutQuad).SetLink(this.gameObject);
                });
            }).SetLink(this.gameObject);
        ps.Play();
        StartCoroutine(ClearEffect());
    }

    public IEnumerator ClearEffect()
    {
        yield return new WaitForSeconds(1f);
        zoomCam.SetActive(true);
    }

    // ピースが正解位置に近いかどうかをチェック（MovePiecesから呼ばれる）
    public bool IsNearAnswerPosition(Transform piece, out Vector3 answerPosition, out Quaternion answerRotation)
    {
        answerPosition = Vector3.zero;
        answerRotation = Quaternion.identity;

        PieceTransforms pieceComponent = piece.GetComponent<PieceTransforms>();
        if (pieceComponent == null) return false;
        if (pieceComponent.isDummy) return false; // ダミーピースは無視

        AnswerPieceInfo answerInfo = pieceComponent.GetComponent<AnswerPieceInfo>();
        if (answerInfo == null || answerInfo.answerPiece == null) return false;

        answerPosition = answerInfo.answerPiece.transform.position;
        answerRotation = answerInfo.answerPiece.transform.rotation;

        float positionDistance = Vector3.Distance(piece.position, answerPosition);
        float rotationDistance = Quaternion.Angle(piece.rotation, answerRotation);

        return positionDistance <= positionThreshold && rotationDistance <= rotationThreshold;
    }

    // 正解角度を取得（MovePiecesから呼ばれる）
    public Quaternion GetAnswerRotation(Transform piece)
    {
        PieceTransforms pieceComponent = piece.GetComponent<PieceTransforms>();
        if (pieceComponent == null) return piece.rotation;

        AnswerPieceInfo answerInfo = pieceComponent.GetComponent<AnswerPieceInfo>();
        if (answerInfo == null || answerInfo.answerPiece == null) return piece.rotation;

        return answerInfo.answerPiece.transform.rotation;
    }
}