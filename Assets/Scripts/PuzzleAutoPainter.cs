using UnityEngine;

public class PuzzleAutoPainter : MonoBehaviour
{
    public GameObject puzzleRoot;        // ブロックの親
    public Texture2D sourceImage;        // 貼りたい画像
    public Shader targetShader;          // 例: Unlit/Texture

    void Start()
    {
        if (puzzleRoot == null || sourceImage == null)
        {
            Debug.LogError("PuzzleRootかSourceImageが未設定です。");
            return;
        }

        Renderer[] renderers = puzzleRoot.GetComponentsInChildren<Renderer>();

        if (renderers.Length == 0)
        {
            Debug.LogWarning("Rendererが見つかりません。");
            return;
        }

        // 1. 全体のBoundingBox（完成形全体）を取得
        Bounds bounds = renderers[0].bounds;
        foreach (var r in renderers)
            bounds.Encapsulate(r.bounds);

        float boundsLeft = bounds.min.x;
        float boundsBottom = bounds.min.y;
        float boundsWidth = bounds.size.x;
        float boundsHeight = bounds.size.y;

        // 2. 各ブロックに画像の該当部分を切り出して貼る
        foreach (var r in renderers)
        {
            Bounds b = r.bounds;

            float normX = (b.min.x - boundsLeft) / boundsWidth;
            float normY = (b.min.y - boundsBottom) / boundsHeight;
            float normW = b.size.x / boundsWidth;
            float normH = b.size.y / boundsHeight;

            // ピクセル単位でクロップ
            int px = Mathf.RoundToInt(normX * sourceImage.width);
            int py = Mathf.RoundToInt(normY * sourceImage.height);
            int pw = Mathf.RoundToInt(normW * sourceImage.width);
            int ph = Mathf.RoundToInt(normH * sourceImage.height);

            // 切り出し + 補正
            px = Mathf.Clamp(px, 0, sourceImage.width - 1);
            py = Mathf.Clamp(py, 0, sourceImage.height - 1);
            pw = Mathf.Clamp(pw, 1, sourceImage.width - px);
            ph = Mathf.Clamp(ph, 1, sourceImage.height - py);

            Texture2D cropped = new Texture2D(pw, ph);
            cropped.SetPixels(sourceImage.GetPixels(px, py, pw, ph));
            cropped.Apply();

            // 上下左右反転
            Texture2D flipped = FlipTextureXY(cropped);

            // 新しいマテリアルを設定
            Material mat = new Material(targetShader != null ? targetShader : Shader.Find("Unlit/Texture"));
            mat.mainTexture = flipped;

// 複数マテリアル対応：最後のマテリアルだけ差し替え
Material[] mats = r.materials;
if (mats.Length > 0)
{
    mats[mats.Length - 1] = mat;
    r.materials = mats;
}
        }
    }

    Texture2D FlipTextureXY(Texture2D tex)
    {
        int width = tex.width;
        int height = tex.height;
        Texture2D flipped = new Texture2D(width, height);

        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                flipped.SetPixel(width - 1 - x, height - 1 - y, tex.GetPixel(x, y));

        flipped.Apply();
        return flipped;
    }
}
