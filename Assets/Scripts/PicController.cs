using System.Linq; // ★ LINQ
using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;

#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(SpriteRenderer))]
public class PicController : MonoBehaviour
{
    [Header("Assign")]
    public Transform targetPic;               // このピースの正解ターゲット
    public string targetTag = "targetPic";    // ターゲット候補タグ

    // 親リスト連携
    [HideInInspector] public PicListController listManager;
    [HideInInspector] public bool isSnapped = false;
    bool canSelect = true;

    [Header("Motion")]
    public float followLerp = 18f;            // ドラッグ追従の滑らかさ
    public float snapDistance;                // スナップ許容距離(ワールド)
    public float releaseReturnTime = 0.35f;   // 戻り時間
    public float snapTime = 0.25f;            // スナップ時間
    public float shakeStrength;               // ブブッ揺れ幅
    public int   shakeVibrato;                // ブブッの細かさ

    [Header("Scale/Rot 同期")]
    public bool matchRotation = true;
    public bool matchScale = true;

    [Header("Runtime (read only)")]
    public Vector3 iniPos;
    public Vector3 iniSca;
    public bool isSelected;
    public Vector3 offset;

    [Header("Outline (child SR)")]
    public SpriteRenderer childOutline;       // 子アウトライン。未設定なら自動検出
    public float outlineOnAlpha = 0.8f;       // 選択時のα（固定で 0.8）
    public float outlineFadeIn   = 0.15f;     // 点灯時間
    public float outlineFadeOut  = 0.12f;     // 消灯時間

    public StageManager stageManager;
    public HandCursorController handCursorController;

    Camera cam;
    SpriteRenderer sr;

    // どちらか存在すれば使う
    Collider   col3d;
    Collider2D col2d;

    Vector3 grabOffsetLocal;
    int? draggingFingerId;
    float fixedZ;

    Tween currentTween;
    Tween outlineTween;

    //（未使用でも保持）
    public int indexInQueue;                 // 左からの番号(0-based)
    public int selectableCount = 3;          // 左からいくつまで選択可
    public int handBuffer;
    // ★ 追加：子に付いている SpriteOutlineMPB を握る（Createは変更しない）
    private SpriteOutlineMPB childOutlineMPB;

    void Awake()
    {
        cam = Camera.main;
        sr  = GetComponent<SpriteRenderer>();
        if(!handCursorController)
            handCursorController = FindObjectOfType<HandCursorController>();

            
        // --- コライダー確保 ---
        col3d = GetComponent<Collider>();
        col2d = GetComponent<Collider2D>();
        if (col3d == null && col2d == null)
        {
            var box = gameObject.AddComponent<BoxCollider>();
            FitBoxColliderToSprite(box);
            col3d = box;
        }
        else if (col3d is BoxCollider box3d)
        {
            FitBoxColliderToSprite(box3d);
        }

        // --- アウトライン SR を LINQ で自動検出（未指定時）---
        if (childOutline == null)
        {
            childOutline = GetComponentsInChildren<SpriteRenderer>(includeInactive: true)
                           .FirstOrDefault(s => s.gameObject != this.gameObject
                                             && s.transform.parent == this.transform);
        }
    }

    void FitBoxColliderToSprite(BoxCollider box)
    {
        var b = sr.bounds;
        var localCenter  = transform.InverseTransformPoint(b.center);
        var localExtents = transform.InverseTransformVector(b.extents);

        box.center = localCenter;
        box.size   = new Vector3(Mathf.Abs(localExtents.x) * 2f,
                                 Mathf.Abs(localExtents.y) * 2f,
                                 0.02f);
    }

    void Start()
    {
        // 好みでインスペクタ調整するならここを外してOK
        snapDistance = 4.5f;
        shakeStrength = 0.5f;
        shakeVibrato = 25;
        offset = new Vector3(0.5f, -4.5f, 0.0f);
        if (handCursorController)
        {
            offset=Vector2.zero;
        }
outlineOnAlpha = 0.8f;  
        iniPos = transform.position;
        iniSca = transform.localScale;
        fixedZ = transform.position.z;

        var sm = GameObject.Find("StageManager");
        if (sm) stageManager = sm.GetComponent<StageManager>();

        // ★ 既存の childOutline がある場合、起動時は透明に初期化（SpriteOutlineMPB優先）
        if (childOutline != null)
        {
            childOutlineMPB = childOutline.GetComponent<SpriteOutlineMPB>();
            if (childOutlineMPB != null)
            {
                var c = childOutlineMPB.outlineColor;
                c.a = 0f;
                childOutlineMPB.outlineColor = c;
                childOutlineMPB.Apply(); // ← 1回でOK
            }
            else
            {
                // フォールバック（MPB直書き）
                SetRendererAlpha(childOutline, 0f);
            }
        }
            // ★ 最初は必ず消灯（フェードアウト時間で0へ）
    SetOutlineVisible(false, instant: true);
    }

    void OnGrabStart()
    {
// handCursorController.NotifyFocus(this.transform);
    }

    void OnGrabEnd()
    {
// handCursorController.NotifyBlur(this.transform);
    }
    void OnDisable()
    {
        currentTween?.Kill();
        outlineTween?.Kill();
        isSelected = false;
        draggingFingerId = null;

        // 念のためアウトライン消灯
        SetOutlineVisible(false);
    }

    // 先頭n可否を親から制御
    public void SetSelectable(bool value)
    {
        canSelect = value;
    }

    public void TweenToQueueX(float x, float time, float delay = 0f)
    {
        currentTween?.Kill();
        currentTween = transform.DOMoveX(x, time)
                                 .SetEase(Ease.OutBack, 1.1f)
                                 .SetDelay(delay);
        iniPos = new Vector3(x, iniPos.y, fixedZ);
    }

    void Update()
    {
        if (!isSelected)
        {
            if (!canSelect) return; // 先頭3以外は開始不可

            if (Input.GetMouseButtonDown(0) && HitSelf(Input.mousePosition))
            {
                BeginDrag(Input.mousePosition);
                draggingFingerId = null;
            }
        }

        if (isSelected)
        {
            Vector3 targetWorld = transform.position;

            if (Input.GetMouseButton(0))
            {
                targetWorld = ScreenToWorldOnPlane(Input.mousePosition, fixedZ) - grabOffsetLocal;
                OnGrabStart();
            }
            else
            {
                EndDrag();
                OnGrabEnd();
            }

            var cur  = transform.position;
            var next = Vector3.Lerp(cur, new Vector3(targetWorld.x, targetWorld.y, fixedZ),
                                    Time.deltaTime * followLerp);
            transform.position = next;
        }
    }

    bool HitSelf(Vector2 screenPos)
    {
        if (col3d != null)
        {
            var ray = cam.ScreenPointToRay(screenPos);
            if (Physics.Raycast(ray, out var hit, 1000f, ~0, QueryTriggerInteraction.Collide))
                return hit.collider == col3d;
            return false;
        }
        if (col2d != null)
        {
            Vector3 world = ScreenToWorldOnPlane(screenPos, fixedZ);
            return col2d.OverlapPoint(world);
        }
        return false;
    }

    void BeginDrag(Vector2 screenPos)
    {
      
        currentTween?.Kill();

        Vector3 world = ScreenToWorldOnPlane(screenPos, fixedZ);
        grabOffsetLocal = world - transform.position + offset;
        if (handCursorController == false)
        {
                    isSelected = true;

            // ちょい持ち上げ
            transform.DOScale(matchScale ? (targetPic ? targetPic.localScale * 1.02f : iniSca * 1.05f) : iniSca * 1.05f, 0.15f)
                     .SetEase(Ease.OutQuad);

            // 前面へ（アウトラインの order も追随）
            sr.sortingOrder += 1000;

            // アウトライン点灯
            SetOutlineVisible(true);
        }
        else
        {
            OnGrabStart();
            // ちょい持ち上げ
            if (isSelected == false)
            {
                                                 SetOutlineVisible(true);

                transform.DOScale(matchScale ? (targetPic ? targetPic.localScale * 1.02f : iniSca * 1.05f) : iniSca * 1.05f, 0.15f)
                         .SetEase(Ease.OutQuad).SetDelay(0.15f).OnComplete(() =>
                         {
                            
                                 // 前面へ（アウトラインの order も追随）
                                 sr.sortingOrder += 1000;
                                 sr.GetComponentInChildren<Transform>().gameObject.GetComponent<SpriteRenderer>().sortingOrder = sr.sortingOrder - 1;

                                 // アウトライン点灯
                             isSelected = true;

                             
                         });

                // 前面へ（アウトラインの order も追随）
            }
        }
    }

    void EndDrag()
    {
        isSelected = false;
        draggingFingerId = null;

        var (closest, dist) = FindClosestTarget();

        if (targetPic != null && dist < snapDistance && closest == targetPic.gameObject)
        {
            SnapToTarget();
        }
        else if (closest != null && dist < snapDistance)
        {
            ShakeAndReturn();
        }
        else
        {
            BackToInitial();
        }

        // 消灯要求（スナップ演出中でもOK）
        SetOutlineVisible(false);
    }

    (GameObject, float) FindClosestTarget()
    {
        var candidates = GameObject.FindGameObjectsWithTag(targetTag);
        GameObject closest = null;
        float best = float.MaxValue;
        Vector3 p = transform.position;
        foreach (var c in candidates)
        {
            float d = Vector2.Distance((Vector2)p, (Vector2)c.transform.position);
            if (d < best) { best = d; closest = c; }
        }
        return (closest, best);
    }

    void SnapToTarget()
    {
        currentTween?.Kill();

        var seq = DOTween.Sequence();

        Vector3 pos = new Vector3(targetPic.position.x, targetPic.position.y, targetPic.position.z);
        Quaternion rot = matchRotation ? targetPic.rotation : transform.rotation;
        Vector3 sca = targetPic.localScale;

        // --- 進行方向（XY）にだけオーバーシュート ---
        Vector2 deltaXY = new Vector2(pos.x - transform.position.x, pos.y - transform.position.y);
        float dist = deltaXY.magnitude;
        float overshootRatio = 1.7f;
        Vector3 posOvershoot = pos;
        if (dist > 0.0001f)
        {
            Vector2 dir = deltaXY / dist;
            float extra = Mathf.Min(dist * (overshootRatio - 1f), 0.5f);
            posOvershoot = new Vector3(
                pos.x + dir.x * extra,
                pos.y + dir.y * extra,
                fixedZ
            );
        }

        float t1 = snapTime * 0.3f;
        float t2 = snapTime * 0.2f;
        seq.Append(transform.DOMove(posOvershoot, t1).SetEase(Ease.OutQuad));
        seq.Join(transform.DORotateQuaternion(rot, t1).SetDelay(0.05f).SetEase(Ease.OutQuad));
        seq.Append(transform.DOMove(pos, t2).SetEase(Ease.InQuad));

        seq.AppendCallback(() => { transform.localScale = sca; });

        float tUp   = snapTime * 0.60f;
        seq.Append(transform.DOScale(sca, tUp).SetEase(Ease.OutQuad));

        seq.OnComplete(() =>
        {
            sr.sortingOrder = 2;

            isSnapped = true;

            var c3d = GetComponent<Collider>();   if (c3d) c3d.enabled = false;
            var c2d = GetComponent<Collider2D>(); if (c2d) c2d.enabled = false;

            listManager?.NotifySnapped(this);
        });

        currentTween = seq;
        // if (stageManager) stageManager.CountUpPic();
    }

    void ShakeAndReturn()
    {
        currentTween?.Kill();
        var seq = DOTween.Sequence();
        seq.Append(transform.DOShakePosition(0.22f, new Vector3(shakeStrength, 0f, 0f), shakeVibrato, 0, false, true));
        seq.Append(transform.DOMove(new Vector3(iniPos.x, iniPos.y, fixedZ), releaseReturnTime).SetEase(Ease.InOutQuad));
        seq.Join(transform.DOScale(iniSca, 0.18f));
        seq.OnComplete(() =>
        {
            sr.sortingOrder -= 1000;
        });
        currentTween = seq;
    }

    void BackToInitial()
    {
        currentTween?.Kill();
        var seq = DOTween.Sequence();
        seq.Append(transform.DOMove(new Vector3(iniPos.x, iniPos.y, fixedZ), releaseReturnTime).SetEase(Ease.OutQuad));
        seq.Join(transform.DOScale(iniSca, 0.18f));
        seq.OnComplete(() =>
        {
            sr.sortingOrder -= 1000;
        });
        currentTween = seq;
    }

    // 画面の指位置 → 固定Z平面へ
    Vector3 ScreenToWorldOnPlane(Vector2 screenPos, float z)
    {
        Plane plane = new Plane(Vector3.forward, new Vector3(0, 0, z));
        Ray ray = cam.ScreenPointToRay(screenPos);
        if (plane.Raycast(ray, out float enter))
            return ray.GetPoint(enter);
        return new Vector3(0, 0, z);
    }

    // ==== アウトライン可視制御（フェード） ====
    // 選択中 α=outlineOnAlpha(既定0.8f)、非選択 α=0f
    void SetOutlineVisible(bool on, bool instant = false)
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (childOutline == null) return;

        outlineTween?.Kill();

        // 並び順
        childOutline.sortingLayerID = sr.sortingLayerID;
        childOutline.sortingOrder = on ? (sr.sortingOrder - 1) : 5;

        float dur = instant ? 0f : (on ? outlineFadeIn : outlineFadeOut);
        float target = on ? outlineOnAlpha : 0f;   // 0.8f ↔ 0f

        // 子の SpriteOutlineMPB を握る
        if (childOutlineMPB == null)
            childOutlineMPB = childOutline.GetComponent<SpriteOutlineMPB>();
    Debug.Log("SetOutlineResult: " + target + " (from " + childOutline.color.a + ")");

        // ===== SpriteOutlineMPB 駆動（推奨） =====
        if (childOutlineMPB != null)
        {
            float fromOutline = childOutlineMPB.outlineColor.a;
            float fromTint = childOutline.color.a;
            float from = Mathf.Max(fromOutline, fromTint);
            Color baseTint = childOutline.color;

            if (dur <= 0f)
            {
                var c = childOutlineMPB.outlineColor; c.a = target;
                childOutlineMPB.outlineColor = c;
                childOutlineMPB.Apply();

                baseTint.a = target;
                childOutline.color = baseTint;
                return;
            }
            if (handCursorController == false)
            {
                outlineTween = DG.Tweening.DOTween.To(() => from, (float a) =>   // ★ float 明示
                {
                    var c = childOutlineMPB.outlineColor; c.a = a;
                    childOutlineMPB.outlineColor = c;
                    childOutlineMPB.Apply();

                    baseTint.a = a;
                    childOutline.color = baseTint;
                }, target, dur).SetEase(Ease.OutQuad);
            }
            else
            { outlineTween = DG.Tweening.DOTween.To(() => from, (float a) =>   // ★ float 明示
                {
                    var c = childOutlineMPB.outlineColor; c.a = a;
                    childOutlineMPB.outlineColor = c;
                    childOutlineMPB.Apply();

                    baseTint.a = a;
                    childOutline.color = baseTint;
                }, target, dur).SetEase(Ease.OutQuad).SetDelay(0.15f);
                
            }

            return;
        }

        // ===== フォールバック（MPB直書き + Tint） =====
        var mpb = new MaterialPropertyBlock();
        childOutline.GetPropertyBlock(mpb);
        Color oc = mpb.GetColor("_OutlineColor");

        if (!childOutline.sharedMaterial || !childOutline.sharedMaterial.HasProperty("_OutlineColor"))
        {
            // _OutlineColor が無いシェーダの場合は Tint の α を基準に
            oc = new Color(1f, 1f, 1f, childOutline.color.a);
        }

        float fromO = oc.a;
        float fromT = childOutline.color.a;
        float from2 = Mathf.Max(fromO, fromT);
        Color baseTint2 = childOutline.color;

        if (dur <= 0f)
        {
            oc.a = target;
            mpb.SetColor("_OutlineColor", oc);
            childOutline.SetPropertyBlock(mpb);

            baseTint2.a = target;
            childOutline.color = baseTint2;
            return;
        }

        if (handCursorController == false)
        {
            outlineTween = DG.Tweening.DOTween.To(() => from2, (float a) =>      // ★ float 明示
            {
                oc.a = a;
                mpb.SetColor("_OutlineColor", oc);
                childOutline.SetPropertyBlock(mpb);

                baseTint2.a = a;
                childOutline.color = baseTint2;
            }, target, dur).SetEase(Ease.OutQuad);
        }
        else
        {
            outlineTween = DG.Tweening.DOTween.To(() => from2, (float a) =>      // ★ float 明示
            {
                oc.a = a;
                mpb.SetColor("_OutlineColor", oc);
                childOutline.SetPropertyBlock(mpb);

                baseTint2.a = a;
                childOutline.color = baseTint2;
            }, target, dur).SetEase(Ease.OutQuad).SetDelay(0.15f);
        }
}



    // =========================
    //  Editor 右クリック専用: 子アウトラインを生成/更新
    // =========================
    [ContextMenu("Create/Refresh Outline Child (_jr)")]
    void CreateOrRefreshOutlineChild()
    {
        // 直下の子をすべて削除
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);
#if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(child.gameObject);
            else                        Destroy(child.gameObject);
#else
            Destroy(child.gameObject);
#endif
        }

        // 新しい子を作成
        string childName = gameObject.name + "_jr";
        var go = new GameObject(childName);
        var t  = go.transform;
        t.SetParent(this.transform, false);
        t.localPosition = Vector3.zero;
        t.localRotation = Quaternion.identity;
        t.localScale    = Vector3.one;

        // 親のSpriteRendererをコピー
        var parentSR = GetComponent<SpriteRenderer>();
        var childSR  = go.AddComponent<SpriteRenderer>();

#if UNITY_EDITOR
        UnityEditor.EditorUtility.CopySerialized(parentSR, childSR);
#else
        childSR.sprite = parentSR.sprite;
        childSR.color  = parentSR.color;
        childSR.flipX  = parentSR.flipX;
        childSR.flipY  = parentSR.flipY;
        childSR.drawMode        = parentSR.drawMode;
        childSR.maskInteraction = parentSR.maskInteraction;
#endif

        // 子専用の設定
        childSR.sortingLayerID = parentSR.sortingLayerID;
        childSR.sortingOrder   = 5;

        // PiecesOutlineByAlpha マテリアルをセット
        Material outlineMat = FindMaterialByName("PiecesOutlineByAlpha");
        if (outlineMat != null) childSR.sharedMaterial = outlineMat;
        else Debug.LogWarning("[PicController] Material 'PiecesOutlineByAlpha' が見つかりませんでした。");

        // SpriteOutlineMPB（あれば便利）を付与しておく（なくても動く設計）
        if (go.GetComponent<SpriteOutlineMPB>() == null)
            go.AddComponent<SpriteOutlineMPB>();

        // 参照更新
        childOutline = childSR;

        // 生成時はα=0（MPBで）に初期化（★ Createは変更しないのでこのまま）
        SetRendererAlpha(childOutline, 0f);

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(go);
            EditorUtility.SetDirty(this.gameObject);
            EditorSceneManagerMarkDirty();
        }
#endif
    }

    // ======= ユーティリティ群（共有汚染なしの安全なα制御） =======

    // 子の現在αを読む（MPB優先、_OutlineColor/_Color を自動判定）
    float ReadRendererAlpha(SpriteRenderer r)
    {
        if (!r) return 0f;
        var m = r.sharedMaterial;
        string prop = GetAlphaPropName(m);

        var mpb = new MaterialPropertyBlock();
        r.GetPropertyBlock(mpb);

        if (prop == "_OutlineColor")
        {
            Color c = mpb.GetColor("_OutlineColor");
            if (c == default && m != null && m.HasProperty("_OutlineColor")) c = m.GetColor("_OutlineColor");
            return c.a;
        }
        if (prop == "_Color")
        {
            Color c = mpb.GetColor("_Color");
            if (c == default && m != null && m.HasProperty("_Color")) c = m.GetColor("_Color");
            return c.a;
        }
        // フォールバック（ほぼ通らない想定）
        return r.color.a;
    }

    // 使えるアルファプロパティ名を探す
    string GetAlphaPropName(Material m)
    {
        if (m == null) return null;
        if (m.HasProperty("_OutlineColor")) return "_OutlineColor"; // アウトライン色直指定型
        if (m.HasProperty("_Color"))        return "_Color";        // 乗算色型（PiecesOutlineByAlpha これ）
        return null;
    }

    // レンダラー“個別”にアルファをセット（MPBで上書き。共有材は触らない）
    void SetRendererAlpha(SpriteRenderer r, float a)
    {
        if (!r) return;

        var m = r.sharedMaterial;
        string prop = GetAlphaPropName(m);

        var mpb = new MaterialPropertyBlock();
        r.GetPropertyBlock(mpb);

        if (prop == "_OutlineColor")
        {
            Color c = mpb.GetColor("_OutlineColor");
            if (c == default && m != null && m.HasProperty("_OutlineColor")) c = m.GetColor("_OutlineColor");
            c.a = a;
            mpb.SetColor("_OutlineColor", c);
            r.SetPropertyBlock(mpb);
            return;
        }
        if (prop == "_Color")
        {
            Color c = mpb.GetColor("_Color");
            if (c == default && m != null && m.HasProperty("_Color")) c = m.GetColor("_Color");
            c.a = a;
            mpb.SetColor("_Color", c);
            r.SetPropertyBlock(mpb);
            return;
        }

        // プロパティが無い超例外ケース：個別 material インスタンスへ
        var inst = r.material; // ← 個別化（共有汚染しない）
        if (inst.HasProperty("_Color"))
        {
            var c2 = inst.GetColor("_Color");
            c2.a = a;
            inst.SetColor("_Color", c2);
        }
    }

    // 親SRから子SRへ設定をざっくりコピー（必要な点だけ）※今は未使用
    void CopySpriteRendererSettings(SpriteRenderer src, SpriteRenderer dst)
    {
        dst.sprite = src.sprite;
        dst.color  = src.color;
        dst.flipX  = src.flipX;
        dst.flipY  = src.flipY;

        dst.drawMode = src.drawMode;
        if (src.drawMode != SpriteDrawMode.Simple)
        {
            dst.size = src.size;
            dst.adaptiveModeThreshold = src.adaptiveModeThreshold;
            dst.tileMode = src.tileMode;
        }

        dst.maskInteraction     = src.maskInteraction;
        dst.sortingLayerID      = src.sortingLayerID;
        dst.allowOcclusionWhenDynamic = src.allowOcclusionWhenDynamic;
        dst.shadowCastingMode   = src.shadowCastingMode;
        dst.receiveShadows      = src.receiveShadows;
    }

    Material FindMaterialByName(string matName)
    {
        // まずシーン/Editor上のロード済みから拾う
        var mats = Resources.FindObjectsOfTypeAll<Material>();
        var found = mats.FirstOrDefault(m => m != null && m.name == matName);
        if (found) return found;

#if UNITY_EDITOR
        // Editorならアセット検索
        string[] guids = AssetDatabase.FindAssets($"{matName} t:Material");
        foreach (var g in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null && mat.name == matName) return mat;
        }
#endif
        return null;
    }

#if UNITY_EDITOR
    // シーンDirtyマーク（EditorOnly）
    void EditorSceneManagerMarkDirty()
    {
        var scene = gameObject.scene;
        if (scene.IsValid() && scene.isLoaded)
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        }
    }
#endif
}
