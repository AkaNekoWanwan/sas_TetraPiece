using UnityEngine;
using UnityEngine.Experimental.Rendering;

public class RenderTextureFormatChecker : MonoBehaviour
{
    // 使いたい RenderTexture を指定（Inspector で設定可）
    public RenderTexture targetRenderTexture;

    void Awake()
    {
        // 高精度フォーマット（D32 SFloat S8 UInt）がサポートされているかチェック
        bool supportsHighPrecision = SystemInfo.IsFormatSupported(
            GraphicsFormat.D32_SFloat_S8_UInt,
            FormatUsage.Render
        );

        if (!supportsHighPrecision)
        {
            Debug.LogWarning("[RenderTextureFormatChecker] High precision depth/stencil not supported. Falling back to D24_UNorm_S8_UInt.");

            if (targetRenderTexture != null)
            {
                // 低精度フォーマットに切り替え
                targetRenderTexture.depthStencilFormat = GraphicsFormat.D24_UNorm_S8_UInt;
                targetRenderTexture.Release();
                targetRenderTexture.Create();
            }
        }
        else
        {
            // Debug.Log("[RenderTextureFormatChecker] High precision depth/stencil is supported on this device.");
        }
    }
}
