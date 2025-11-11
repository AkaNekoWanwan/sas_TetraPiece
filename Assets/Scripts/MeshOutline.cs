using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class MeshOutline : MonoBehaviour
{
    public Color outlineColor = Color.black;
    [Range(0f, 0.1f)]
    public float outlineWidth = 0.02f;

    private GameObject outlineObject;
    private Material outlineMaterial;

    void Start()
    {
        CreateOutline();
    }

    void CreateOutline()
    {
        // アウトライン用オブジェクト作成
        outlineObject = new GameObject("Outline");
        outlineObject.transform.SetParent(transform);
        outlineObject.transform.localPosition = Vector3.zero;
        outlineObject.transform.localRotation = Quaternion.identity;
        outlineObject.transform.localScale = Vector3.one;

        // Meshコピー
        MeshFilter sourceFilter = GetComponent<MeshFilter>();
        MeshRenderer sourceRenderer = GetComponent<MeshRenderer>();

        MeshFilter outlineFilter = outlineObject.AddComponent<MeshFilter>();
        outlineFilter.sharedMesh = sourceFilter.sharedMesh;

        MeshRenderer outlineRenderer = outlineObject.AddComponent<MeshRenderer>();
        outlineMaterial = new Material(Shader.Find("Unlit/OutlineSilhouette"));
        outlineMaterial.SetColor("_OutlineColor", outlineColor);
        outlineMaterial.SetFloat("_Scale", 1f + outlineWidth);

        outlineRenderer.material = outlineMaterial;

        // レンダリング順を後ろに
        outlineRenderer.material.renderQueue = 3000; // Transparentより後
    }

    void Update()
    {
        if (outlineMaterial != null)
        {
            outlineMaterial.SetFloat("_Scale", 1f + outlineWidth);
            outlineMaterial.SetColor("_OutlineColor", outlineColor);
        }
    }
}
