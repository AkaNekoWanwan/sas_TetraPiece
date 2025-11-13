using UnityEngine;
using UnityEngine.UI;

// マスクシェーダー(MaskShader.shader)のサポートクラス。
public class CustomMaskController : MonoBehaviour
{
    public Material contentMaterial;
    public RectTransform maskTransform;
    public RectTransform canvasTransform;

    void Update()
    {
        // UI上のマスクの座標をシェーダーに反映
        Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(null, maskTransform.position);
        Vector2 normalizedPos = screenPos / new Vector2(Screen.width, Screen.height);
        contentMaterial.SetVector("_MaskPos", new Vector4(normalizedPos.x, normalizedPos.y, 0, 0));

        // マスクのサイズをシェーダーに適用
        Vector2 maskSize = maskTransform.rect.size / canvasTransform.rect.size;
        contentMaterial.SetVector("_MaskSize", new Vector4(maskSize.x, maskSize.y, 0, 0));
    }
}
