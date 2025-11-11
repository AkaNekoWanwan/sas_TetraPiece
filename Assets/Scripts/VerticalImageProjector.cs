using UnityEngine;

public class GridImageProjector : MonoBehaviour
{
    public Texture2D sourceImage;
    public Shader shader;
    public Transform rootParent;

    public int horizontalDivisions = 2; // 横分割数（列数）
    public int verticalDivisions = 3;   // 縦分割数（行数）
    void Start()
    {
        if (sourceImage == null || rootParent == null) return;

        Renderer[] renderers = rootParent.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        // 全体の配置範囲を取得
        Bounds allBounds = renderers[0].bounds;
        foreach (var r in renderers) allBounds.Encapsulate(r.bounds);
        Vector3 min = allBounds.min;
        Vector3 max = allBounds.max;

        float totalWidth = max.x - min.x;
        float totalHeight = max.y - min.y;

        foreach (var r in renderers)
        {
            // 各Rendererの中心位置から行・列を決定
            Vector3 center = r.bounds.center;

            float normX = (center.x - min.x) / totalWidth;
            float normY = (center.y - min.y) / totalHeight;

            int col = Mathf.Clamp(Mathf.FloorToInt(normX * horizontalDivisions), 0, horizontalDivisions - 1);
            int row = Mathf.Clamp(Mathf.FloorToInt(normY * verticalDivisions), 0, verticalDivisions - 1);

            // オフセット・スケール計算
            Vector2 offset = new Vector2(
                col / (float)horizontalDivisions,
                row / (float)verticalDivisions
            );
            Vector2 scale = new Vector2(
                1f / horizontalDivisions,
                1f / verticalDivisions
            );

            // マテリアル設定（インスタンス化して割り当て）
            Material mat = new Material(shader != null ? shader : Shader.Find("Unlit/Texture"));
            mat.mainTexture = sourceImage;
            mat.mainTextureScale = scale;
            mat.mainTextureOffset = offset;

            r.material = mat;
        }
    }

}
