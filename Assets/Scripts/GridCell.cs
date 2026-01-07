using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class GridCell : MonoBehaviour
{
    public bool isOccupied = false;
    public bool isUpSide;

    public Transform occupiedByChild;

    [Header("Grid Coordinates")]
    public int gridX;
    public int gridY;

    public static Color32 outLineColor = default;
    public Color32 currentOutLineColor = default;
    public List<UnityEngine.UI.Outline> outLines = null;

    // ★追加（最小）：大量生成中は OnValidate を止めるためのフラグ
    public static bool SuppressValidate = false;

#if UNITY_EDITOR
    private bool _isActive = true;

    private void OnValidate()
    {
        // ★追加（最小）：生成中は止める
        if (SuppressValidate) return;

        // 既存のガード（そのまま）
        if(!_isActive)
            return;

        Debug.Log("ああああああああああ:1");
        if(UnityEditor.EditorApplication.isPlaying)
            return;
        Debug.Log("ああああああああああ:2");

        // ★追加（最小）：再入防止を try/finally で確実に（中身は変えない）
        _isActive = false;
        try
        {
            // アウトラインを1本に戻す処理
            for(int i = outLines.Count - 1; i >= 1; i--)
            {
                var outLine = outLines[i];
                DestroyImmediate(outLine);
                outLines.RemoveAt(i);
            }
            outLines[0].effectDistance = Vector2.one * 1f;
        // }

        // AbstractGridImageSplitter spritter = transform.parent.GetComponentInParent<AbstractGridImageSplitter>();
    
        // if(spritter != null )
        // {
        //     if(spritter.GetShapeType() == ShapeType.Triangle)
        //     {
        //         // this.transform.localScale = Vector3.one * 0.9f;
        //         this.transform.localScale = Vector3.one;
        //     }
        //     else
        //     {
        //         this.transform.localScale = Vector3.one;
        //     }
        // }

        Debug.Log($"ああああああああああ:3,{outLines.Count}, {this.gameObject.name}, {this.transform.parent.parent.parent.name}");

        // if(outLines.Count == 0)
        //     outLines.Add(GetComponent<UnityEngine.UI.Outline>());

        // if(outLines.Count == 0)
        //     return;

        // アウトラインを1本に戻す処理
        // for(int i = outLines.Count - 1; i >= 1; i--)
        // {
        //     var outLine = outLines[i];
        //     DestroyImmediate(outLine);
        //     outLines.RemoveAt(i);
        // }
        // if(outLines.Count == 0)
        //     return;
        // outLines[0].effectDistance = Vector2.one * 5f;

        // アウトラインを2本にする処理
        // if(pieceListController == null || pieceListController.ShapeType != ShapeType.Square)
        // {
        //     if(outLines.Count == 1)
        //     {
        //         UnityEngine.UI.Outline newOutLine = this.gameObject.AddComponent<UnityEngine.UI.Outline>();
        //         outLines.Add(newOutLine);
        //     }
        // }
            // SetUpGridPos();
            outLines = GetComponents<UnityEngine.UI.Outline>().ToList();
            GridPieceListController pieceListController = transform.parent.parent.GetComponentInChildren<GridPieceListController>();
            if(outLines != null)
            {
                foreach(var outline in outLines)
                {
                    int activeOutlineCount = 0; // ※元の挙動を変えないため、そのまま
                    if(outline != null)
                    {
                        if(activeOutlineCount == 0)
                        {
                            outline.enabled = true;
                            activeOutlineCount++;
                            outline.effectDistance = Vector2.one * 1f;
                        }
                        else
                            outline.enabled = false;
                    }
                }
            }

            Debug.Log($"ああああああああああ:3,{outLines.Count}, {this.gameObject.name}, {this.transform.parent.parent.parent.name}");

            for(int i = 0; i < outLines.Count; i++)
            {
                var outLine = outLines[i];
                AbstractGridImageSplitter spritter = GetComponentInParent<AbstractGridImageSplitter>();
                if(spritter == null)
                    return;
                SpritterParam param = spritter._param;

                if(!IsEqualsColor(param.OutLineColor, outLine.effectColor))
                {
                    if(IsEqualsColor(param.OutLineColor, currentOutLineColor) && i == 0)
                    {
                        currentOutLineColor = outLine.effectColor;
                        param.OutLineColor = outLine.effectColor;
                    }
                    else
                    {
                        currentOutLineColor = param.OutLineColor;
                        outLine.effectColor = param.OutLineColor;
                    }
                }
                if(i == 0)
                {
                    if(pieceListController == null || pieceListController.ShapeType == ShapeType.Square)
                    {
                        outLine.effectDistance = Vector2.one * 2f;
                    }
                    else
                        outLine.effectDistance = Vector2.one * 1f;
                }
                else
                    outLine.effectDistance = Vector2.one * 1f;
            }
        }
        finally
        {
            _isActive = true;
        }
    }

    private bool IsEqualsColor(Color32 colorA, Color32 colorB)
    {
        if(colorA.r != colorB.r)
            return false;
        if(colorA.g != colorB.g)
            return false;
        if(colorA.b != colorB.b)
            return false;
        if(colorA.a != colorB.a)
            return false;
        return true;
    }
#endif
}
