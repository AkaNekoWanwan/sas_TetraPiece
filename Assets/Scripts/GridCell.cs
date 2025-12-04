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

    // 今までは親 (PieceDragController) を持ってた
    // public PieceDragController occupiedBy;

    // 子オブジェクト単位にしたいので Transform に変更
    public Transform occupiedByChild;

    [Header("Grid Coordinates")]
    public int gridX;
    public int gridY;

    public static Color32 outLineColor = default;
    public Color32 currentOutLineColor = default;
    public List<UnityEngine.UI.Outline> outLines = null;
    // public bool _isSetGridPos = false;

    // アウトラインの色合を変えたら全体に共有
#if UNITY_EDITOR
    private bool _isActice = false;

    private void OnValidate() {
        if(!_isActice)
            return;
        Debug.Log("ああああああああああ:1");
        if(UnityEditor.EditorApplication.isPlaying)
            return;
        Debug.Log("ああああああああああ:2");
        // SetUpGridPos();
        if(outLines == null)
            outLines = GetComponents<UnityEngine.UI.Outline>().ToList();
        if(outLines == null)
            return;
        Debug.Log($"ああああああああああ:3,{outLines.Count}");

        // if(outLines.Count == 0)
        //     outLines.Add(GetComponent<UnityEngine.UI.Outline>());

        GridPieceListController pieceListController = transform.parent.parent.GetComponentInChildren<GridPieceListController>();
        if(pieceListController == null || pieceListController.ShapeType != ShapeType.Square)
        {
            if(outLines.Count == 1)
            {
                UnityEngine.UI.Outline newOutLine = this.gameObject.AddComponent<UnityEngine.UI.Outline>();
                outLines.Add(newOutLine);
            }
        }

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
                    outLine.effectDistance = Vector2.one * 5f;
                }
                else
                    outLine.effectDistance = Vector2.one * 2f;
            }
            else
                outLine.effectDistance = Vector2.one * 3f;
                
        }
    }

    // アウトラインの色合わせよう
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
    // グリッドの座標設定
    // public void SetUpGridPos()
    // {
    //     // if(_isSetGridPos)
    //     //     return;
    //     // _isSetGridPos = true;
    //     Vector2Int pos = TextParser.ParseAnswerCoordinates(this.gameObject.name);
    //     gridX = pos.x;
    //     gridY = pos.y;
    // }
}
