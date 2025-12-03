using UnityEngine;
using UnityEngine.UI;

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
    public UnityEngine.UI.Outline outLine = null;
    // public bool _isSetGridPos = false;

    // アウトラインの色合を変えたら全体に共有
    private void OnValidate() {
        // SetUpGridPos();
        if(outLine == null)
            outLine = GetComponent<UnityEngine.UI.Outline>();
        if(outLine == null)
            return;

        AbstractGridImageSplitter spritter = GetComponentInParent<AbstractGridImageSplitter>();
        if(spritter == null)
            return;
        SpritterParam param = spritter._param;

        if(!IsEqualsColor(param.OutLineColor, outLine.effectColor))
        {
            if(IsEqualsColor(param.OutLineColor, currentOutLineColor))
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
