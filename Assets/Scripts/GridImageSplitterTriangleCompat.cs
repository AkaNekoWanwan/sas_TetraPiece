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
/// 【互換性レイヤー】Triangle用の既存クラス
/// 内部的にはGridImageSplitterに委譲し、ShapeType.Triangleとして動作します。
/// 新規作成時はGridImageSplitterを直接使用することを推奨します。
/// </summary>
[System.Obsolete("Use GridImageSplitter with ShapeType.Triangle instead. This class exists for backward compatibility.", false)]
[ExecuteInEditMode]
[RequireComponent(typeof(Image))]
public class GridImageSplitterTriangleCompat : GridImageSplitter
{
#if UNITY_EDITOR
    private void Awake()
    {
        // 常にTriangleとして動作
        SetShapeType(ShapeType.Triangle);
    }

    private void Reset()
    {
        SetShapeType(ShapeType.Triangle);
    }
#endif
}
