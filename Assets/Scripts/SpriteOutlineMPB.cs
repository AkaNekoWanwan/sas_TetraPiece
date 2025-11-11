using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteOutlineMPB : MonoBehaviour
{
    [Header("Common")]
    [Tooltip("アウトライン色（Aで不透明度）")]
    public Color outlineColor = new Color(1f, 1f, 1f, 0.9f);

    [Tooltip("不透明度の乗算（対応シェーダでは _Intensity / _Overlay に入る）")]
    [Range(0f, 600f)] public float strength = 1f;

    [Tooltip("アルファ境界のしきい値（線を出す境界）")]
    [Range(0f, 1f)] public float alphaThreshold = 0.25f;

    [Header("Screen-px stable shader (Stroke*) 向け")]
    [Tooltip("画面ピクセル基準の太さ（_StrokeWidthPx）")]
    [Min(0f)] public float strokeWidthPx = 6f;

    [Tooltip("X,Y方向のアウトラインずらし（px） _StrokeOffset")]
    public Vector2 strokeOffsetPx = Vector2.zero;

    [Tooltip("エッジの柔らかさ（px） _EdgeSoftnessPx")]
    [Min(0f)] public float edgeSoftnessPx = 1f;

    [Tooltip("ミップの影響を避けるためLOD0固定サンプリング（対応シェーダのみ）")]
    public bool forceLOD0 = true;

    [Header("Simple texel shader (Outline*) 向け")]
    [Tooltip("テクセル基準の太さ（_OutlineSize など）")]
    [Min(0f)] public float outlineSizeTexels = 23f;

    [Header("Options")]
    [Tooltip("Inspector編集時/毎フレーム自動反映")]
    public bool autoApply = true;

    // ===== Internals =====
    private SpriteRenderer sr;
    private MaterialPropertyBlock mpb;

    // よく使われるプロパティを事前にID化
    static readonly int PID_Color          = Shader.PropertyToID("_Color");
    static readonly int PID_AlphaThreshold = Shader.PropertyToID("_AlphaThreshold");

    static readonly int PID_StrokeColor    = Shader.PropertyToID("_StrokeColor");
    static readonly int PID_StrokeWidthPx  = Shader.PropertyToID("_StrokeWidthPx");
    static readonly int PID_StrokeOffset   = Shader.PropertyToID("_StrokeOffset");
    static readonly int PID_EdgeSoftnessPx = Shader.PropertyToID("_EdgeSoftnessPx");
    static readonly int PID_ForceLOD0      = Shader.PropertyToID("_FORCE_LOD0");
    static readonly int PID_Intensity      = Shader.PropertyToID("_Intensity");
    static readonly int PID_Overlay        = Shader.PropertyToID("_Overlay");

    static readonly int PID_OutlineColor   = Shader.PropertyToID("_OutlineColor");
    static readonly int PID_OutlineSize    = Shader.PropertyToID("_OutlineSize");

    // 太さ名はシェーダごとにバラつくので総当たり候補
    static readonly int[] PID_OutlineWidthCandidates = new int[]
    {
        // テクセル基準の名前たち
        Shader.PropertyToID("_OutlineSize"),
        Shader.PropertyToID("_OutlineWidth"),
        Shader.PropertyToID("_OutlineThickness"),
        Shader.PropertyToID("_Outline"),
        // 画面px基準も一応
        Shader.PropertyToID("_StrokeWidthPx"),
    };

    void OnEnable()
    {
        if (!sr) sr = GetComponent<SpriteRenderer>();
        if (mpb == null) mpb = new MaterialPropertyBlock();
        Apply();
    }

    void Start() => Apply();

    void LateUpdate()
    {
        if (autoApply) Apply();
    }

    void OnValidate()
    {
        if (!autoApply) return;
        if (!sr) sr = GetComponent<SpriteRenderer>();
        if (mpb == null) mpb = new MaterialPropertyBlock();
        Apply();
    }

    [ContextMenu("Apply Now")]
    void ApplyNow() => Apply();

    public void Apply()
    {
        if (!sr) return;

        // 現在の MPB を取得して追記していく（共有マテリアルは触らない）
        sr.GetPropertyBlock(mpb);

        var mat = sr.sharedMaterial; // null の場合もある（SpriteRendererデフォルト）

        // --- 共通 ---
        SafeSetColor (mat, PID_Color,          outlineColor);   // 一部シェーダでTint用途
        SafeSetFloat (mat, PID_AlphaThreshold, alphaThreshold);

        // --- Stroke* 系（画面px安定シェーダ）---
        SafeSetColor (mat, PID_StrokeColor,    outlineColor);
        SafeSetFloat (mat, PID_StrokeWidthPx,  strokeWidthPx);
        SafeSetVector(mat, PID_StrokeOffset,   new Vector4(strokeOffsetPx.x, strokeOffsetPx.y, 0f, 0f));
        SafeSetFloat (mat, PID_EdgeSoftnessPx, edgeSoftnessPx);
        SafeSetFloat (mat, PID_ForceLOD0,      forceLOD0 ? 1f : 0f);

        // 強度（どちらかがあれば効く）
        SafeSetFloat (mat, PID_Intensity,      strength);
        SafeSetFloat (mat, PID_Overlay,        strength);

        // --- Outline* 系（テクセル基準シェーダ）---
        // 色は Outline / Stroke の両方へ流す（どちらでも対応）
        SafeSetColor (mat, PID_OutlineColor,   outlineColor);
        SafeSetColor (mat, PID_StrokeColor,    outlineColor);

        // 太さは候補名へ総当たりで流し込む
        foreach (var pid in PID_OutlineWidthCandidates)
            SafeSetFloat(mat, pid, outlineSizeTexels);

        // 反映
        sr.SetPropertyBlock(mpb);

#if UNITY_EDITOR
        SceneView.RepaintAll();
#endif
    }

    void OnDisable()
    {
        if (sr) sr.SetPropertyBlock(null);
    }

    // ========== Safe setters ==========
    // 「そのマテリアルがプロパティを持っているときだけ」MPBへ書く
    void SafeSetFloat(Material mat, int id, float value)
    {
        if (mat == null || mat.HasProperty(id))
            mpb.SetFloat(id, value);
    }

    void SafeSetColor(Material mat, int id, Color value)
    {
        if (mat == null || mat.HasProperty(id))
            mpb.SetColor(id, value);
    }

    void SafeSetVector(Material mat, int id, Vector4 value)
    {
        if (mat == null || mat.HasProperty(id))
            mpb.SetVector(id, value);
    }

#if UNITY_EDITOR
    // ========== Debug ==========
    [ContextMenu("Debug/Print Outline Props")]
    void DebugPrintOutlineProps()
    {
        if (!sr) sr = GetComponent<SpriteRenderer>();
        var mat = sr ? sr.sharedMaterial : null;
        if (!mat)
        {
            Debug.LogWarning("[SpriteOutlineMPB] No material on SpriteRenderer.");
            return;
        }

        string[] names = {
            "_OutlineColor","_StrokeColor","_Color",
            "_OutlineSize","_OutlineWidth","_OutlineThickness","_Outline",
            "_StrokeWidthPx","_StrokeOffset","_EdgeSoftnessPx",
            "_AlphaThreshold","_FORCE_LOD0","_Intensity","_Overlay"
        };

        var found = names.Where(n => mat.HasProperty(n)).ToArray();
        Debug.Log($"[SpriteOutlineMPB] Material '{mat.name}' has: {string.Join(", ", found)}", mat);
    }
#endif
}
