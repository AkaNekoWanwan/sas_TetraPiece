using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 【互換性レイヤー】Hex用の既存クラス
/// 内部的にはGridImageSplitterに委譲し、ShapeType.Hexとして動作します。
/// 新規作成時はGridImageSplitterを直接使用することを推奨します。
/// </summary>
[System.Obsolete("Use GridImageSplitter with ShapeType.Hex instead. This class exists for backward compatibility.", false)]
[ExecuteInEditMode]
[RequireComponent(typeof(Image))]
public class GridImageSplitterHexCompat : GridImageSplitter
{
#if UNITY_EDITOR
    private void Awake()
    {
        // 常にHexとして動作
        SetShapeType(ShapeType.Hex);
    }

    private void Reset()
    {
        SetShapeType(ShapeType.Hex);
    }
#endif
}
