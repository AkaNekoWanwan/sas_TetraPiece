using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// Hand cursor controller for UI (RectTransform).
/// - handCursor の親 RectTransform 基準で座標変換
/// - タップ中: Exact / Smoothed / TweenThenExact（既定は Exact）
/// - 近距離ロックで縮小＋grab画像に切替、離すと復元＋normal画像
/// - タップしていない間: 指数スムーズ追従（遅め）＋小揺れ無視
/// - DOTween 必須
/// </summary>
[DisallowMultipleComponent]
public class HandCursorController : MonoBehaviour
{
    // ===== References =====
    [Header("References")]
    public RectTransform handCursor;     // カーソル画像（RectTransform）
    public Canvas canvas;                // Overlay / Camera / World

    // ===== Touch Follow Modes =====
    public enum FollowMode { Exact, Smoothed, TweenThenExact }

    [Header("Touch Follow (While Pressing)")]
    public FollowMode followMode = FollowMode.Exact; // 既定: 完全一致

    [Tooltip("タップ中に指からのオフセット（真下なら zero）")]
    public Vector2 dragOffset = Vector2.zero;

    [Header("First Approach (TweenThenExact)")]
    public bool useFirstApproach = true;
    public float firstApproachTime = 0.2f;
    public Ease firstApproachEase = Ease.OutQuad;

    [Header("Near-Lock (Scale & Visual)")]
    public bool useNearLock = true;
    [Tooltip("ロック距離(px)")]
    public float nearDistance = 20f;
    [Tooltip("ロック時の縮小率")]
    public float nearScale = 0.85f;
    public float scaleTweenTime = 0.1f;
    public Ease scaleEase = Ease.OutSine;
    [Tooltip("通常手画像 (ON: normal)")]
    public Image normalHand;
    [Tooltip("掴み手画像 (ON: grabbing)")]
    public Image grabHand;
    [Tooltip("ロック解除距離倍率（>1で解除を少し遅らせる）")]
    public float nearUnlockFactor = 1.2f;

    [Header("Smoothed Settings (While Pressing)")]
    [Range(0.05f, 0.5f)] public float smoothingFactor = 0.2f; // 小さいほどヌルっと
    public bool snapWhenClose = true;
    public float snapThreshold = 0.5f; // px

    [Header("Release Slide (When Releasing)")]
    public bool useReleaseSlide = true;               // Idle追従ONのときは自動無効
    public Vector2 releaseOffset = new Vector2(50f, -50f);
    public float releaseSlideTime = 0.25f;
    public Ease releaseEase = Ease.OutSine;

    // ===== Idle (Not Pressing) =====
    [Header("Idle Follow (When Not Touching)")]
    public bool useIdleFollow = true;
    [Tooltip("遅れ具合（大きいほどゆっくり）。0.18〜0.35あたりが目安")]
    public float idleTimeConstant = 0.25f;
    [Tooltip("停止許容量：ターゲットとの距離がこの値以下なら動かない")]
    public float idleDeadzone = 3f;            // px
    [Tooltip("マウスの小揺れ無視：この距離未満の移動ではターゲット更新しない")]
    public float idleMinPointerDelta = 1.5f;   // px

    // ===== Internals =====
    private bool isTouching;
    private bool lockedNear;
    private Vector3 iniScale;

    private Tweener moveTween;     // first approach
    private Tweener scaleTween;    // near lock scale
    private Tweener releaseTween;  // release slide

    private Vector2 smoothedPosition; // for Smoothed mode (while touching)

    private Vector2 lastIdleTarget; // for Idle follow
    private bool hasIdleTarget;

    private Camera cam;
    private RectTransform targetRect;

    void Awake()
    {
        if (!handCursor) handCursor = GetComponent<RectTransform>();
        if (!canvas) canvas = GetComponentInParent<Canvas>();

        targetRect = handCursor.transform.parent as RectTransform ?? canvas.GetComponent<RectTransform>();
        cam = canvas && canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas ? canvas.worldCamera : null;

        iniScale = handCursor.localScale;
    }

    void OnEnable()
    {
        isTouching = false;
        lockedNear = false;
        hasIdleTarget = false;
        KillAllTweens();
        handCursor.localScale = iniScale;
        SetHandVisual(false); // normal 表示で開始
    }

    void OnDisable() => KillAllTweens();

    void Update()
    {
        // ===== Press start =====
        if (Input.GetMouseButtonDown(0))
        {
            isTouching = true;
            lockedNear = false;

            KillAllTweens(); // 途中のTweenは破棄

            if (!ScreenToParentLocal(Input.mousePosition, out var localPos))
                return;

            var targetPos = localPos + dragOffset;

            // 近距離ロック開始判定（開始時）
            if (useNearLock &&
                Vector2.Distance(handCursor.anchoredPosition, targetPos) <= nearDistance)
            {
                lockedNear = true;
                scaleTween = handCursor.DOScale(iniScale * nearScale, scaleTweenTime).SetEase(scaleEase);
                SetHandVisual(true); // grabに切替
            }
            else
            {
                SetHandVisual(false); // 念のため normal
            }

            switch (followMode)
            {
                case FollowMode.Exact:
                    handCursor.anchoredPosition = targetPos; // ピタ
                    smoothedPosition = targetPos;
                    break;

                case FollowMode.Smoothed:
                    handCursor.anchoredPosition = targetPos; // 吸着してからSmoothed
                    smoothedPosition = targetPos;
                    break;

                case FollowMode.TweenThenExact:
                    if (useFirstApproach && firstApproachTime > 0f)
                    {
                        moveTween = handCursor.DOAnchorPos(targetPos, firstApproachTime)
                            .SetEase(firstApproachEase)
                            .OnComplete(() =>
                            {
                                handCursor.anchoredPosition = targetPos;
                                smoothedPosition = targetPos;
                            });
                    }
                    else
                    {
                        handCursor.anchoredPosition = targetPos;
                        smoothedPosition = targetPos;
                    }
                    break;
            }
        }

        // ===== While pressing =====
        if (isTouching && Input.GetMouseButton(0))
        {
            // TweenThenExact の初回寄せ中は待つ
            if (followMode == FollowMode.TweenThenExact && moveTween != null && moveTween.IsActive())
                return;

            if (!ScreenToParentLocal(Input.mousePosition, out var localPos))
                return;

            var targetPos = localPos + dragOffset;
            float dist = Vector2.Distance(handCursor.anchoredPosition, targetPos);

            // ロック移行（未ロック→ロック）
            if (useNearLock && !lockedNear && dist <= nearDistance)
            {
                lockedNear = true;
                scaleTween?.Kill();
                scaleTween = handCursor.DOScale(iniScale * nearScale, scaleTweenTime).SetEase(scaleEase);
                SetHandVisual(true); // grab
            }
            // ロック解除（ヒステリシス付き）
            else if (useNearLock && lockedNear && dist > nearDistance * Mathf.Max(1f, nearUnlockFactor))
            {
                lockedNear = false;
                scaleTween?.Kill();
                scaleTween = handCursor.DOScale(iniScale, scaleTweenTime).SetEase(scaleEase);
                SetHandVisual(false); // normal
            }

            if (followMode == FollowMode.Exact || followMode == FollowMode.TweenThenExact)
            {
                handCursor.anchoredPosition = targetPos; // 完全一致
            }
            else // Smoothed
            {
                smoothedPosition = Vector2.Lerp(smoothedPosition, targetPos, smoothingFactor);
                if (snapWhenClose && Vector2.Distance(smoothedPosition, targetPos) <= snapThreshold)
                    smoothedPosition = targetPos;

                handCursor.anchoredPosition = smoothedPosition;
            }
        }

        // ===== Idle (not pressing): slow follow + jitter ignore =====
        if (!isTouching && useIdleFollow)
        {
            if (ScreenToParentLocal(Input.mousePosition, out var localPos))
            {
                // 小揺れはターゲット更新しない
                if (!hasIdleTarget || Vector2.Distance(lastIdleTarget, localPos) >= idleMinPointerDelta)
                {
                    lastIdleTarget = localPos;
                    hasIdleTarget = true;
                }

                if (hasIdleTarget)
                {
                    var current = handCursor.anchoredPosition;
                    var dist = Vector2.Distance(current, lastIdleTarget);

                    if (dist > idleDeadzone)
                    {
                        // 時間定数ベース指数スムージング（ゆっくり追従）
                        float alpha = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.0001f, idleTimeConstant));
                        handCursor.anchoredPosition = Vector2.Lerp(current, lastIdleTarget, alpha);
                    }
                    // deadzone 以内は停止＝微小動作は無視
                }
            }
        }

        // ===== Release =====
        if (Input.GetMouseButtonUp(0))
        {
            if (!isTouching) return;
            isTouching = false;

            // スケール復元
            scaleTween?.Kill();
            handCursor.DOScale(iniScale, scaleTweenTime).SetEase(scaleEase);

            // 要求通り：離したら normal に戻す
            SetHandVisual(false);

            // Idle追従が有効なら、すぐマウスへ寄るのでスライドは飛ばす
            if (!useIdleFollow && useReleaseSlide)
            {
                releaseTween?.Kill();
                releaseTween = handCursor
                    .DOAnchorPos(handCursor.anchoredPosition + releaseOffset, releaseSlideTime)
                    .SetEase(releaseEase);
            }

            // Idle開始時のターゲット初期化（次フレームで更新）
            hasIdleTarget = false;
        }
    }

    // ========= Helpers =========
    private bool ScreenToParentLocal(Vector3 screenPos, out Vector2 localPos)
    {
        if (!targetRect)
        {
            localPos = default;
            return false;
        }
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(targetRect, screenPos, cam, out localPos);
    }

    private void SetHandVisual(bool grabbing)
    {
        if (normalHand) normalHand.gameObject.SetActive(!grabbing);
        if (grabHand)   grabHand.gameObject.SetActive(grabbing);
    }

    private void KillAllTweens()
    {
        moveTween?.Kill();    moveTween = null;
        scaleTween?.Kill();   scaleTween = null;
        releaseTween?.Kill(); releaseTween = null;
    }
}
