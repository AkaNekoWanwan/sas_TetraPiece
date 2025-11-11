using System.Collections.Generic;
using UnityEngine;

public class PuzzleImageManager : MonoBehaviour
{
    public Texture2D sourceImage;
    public GameObject cubePrefab;
    public float cellSize = 1f;

    void Start()
    {
        Apply2x2Puzzle();
    }

    void Apply2x2Puzzle()
    {
        int gridX = 2;
        int gridY = 2;

        // 1. 正方形にクロップ（中央寄せ）
        int cropSize = sourceImage.width;
        int startY = (sourceImage.height - cropSize) / 2;

        Texture2D squareImage = new Texture2D(cropSize, cropSize);
        squareImage.SetPixels(sourceImage.GetPixels(0, startY, cropSize, cropSize));
        squareImage.Apply();

        // 2. 分割サイズ
        int sliceWidth = cropSize / gridX;
        int sliceHeight = cropSize / gridY;

       for (int y = 0; y < gridY; y++)
{
    for (int x = 0; x < gridX; x++)
    {
        int px = x * sliceWidth;
        int py = y * sliceHeight; // ✅ 上下を反転させる

        Texture2D slice = new Texture2D(sliceWidth, sliceHeight);
        slice.SetPixels(squareImage.GetPixels(px, py, sliceWidth, sliceHeight));
        slice.Apply();

        Texture2D final = FlipTextureXY(slice);

        Vector3 pos = new Vector3(x * cellSize, y * cellSize, 0);
        var cube = Instantiate(cubePrefab, pos, Quaternion.identity);
        var mat = new Material(Shader.Find("Unlit/Texture"));
        mat.mainTexture = final;
        cube.GetComponent<Renderer>().material = mat;
    }
}

    }

    Texture2D FlipTextureXY(Texture2D original)
    {
        int width = original.width;
        int height = original.height;
        Texture2D flipped = new Texture2D(width, height);

        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                flipped.SetPixel(width - 1 - x, height - 1 - y, original.GetPixel(x, y));

        flipped.Apply();
        return flipped;
    }
}
