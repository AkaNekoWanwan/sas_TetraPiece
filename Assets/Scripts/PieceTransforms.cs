using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PieceTransforms : MonoBehaviour
{
    public Vector3 iniPosition;
    public MovePieces movePieces;
        public Quaternion iniRotation;
    public bool isCubeLike;
    public bool isDummy;

    private int frameCounter = 0;

    void Awake()
    {
             iniPosition = transform.position;
        iniRotation = transform.rotation;
    }
    void Start()
    {
   
        this.gameObject.AddComponent<CollisionDetector>();


        // Meshが立方体に近いかどうか判定
        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            Bounds bounds = mf.sharedMesh.bounds;
            Vector3 size = bounds.size;

            float x = size.x;
            float y = size.y;
            float z = size.z;

            // 3辺の長さが全て似ていれば立方体っぽいとみなす（10%以内）
            float max = Mathf.Max(x, y, z);
            float min = Mathf.Min(x, y, z);

            if (min / max > 0.9f)
            {
                isCubeLike = true;
                Debug.Log($"{name} は立方体に近い形状です");
            }
        }
    }

    void FixedUpdate()
    {
        frameCounter++;

        if (frameCounter % 6 != 0) return;

        // Collider[] overlaps = Physics.OverlapBox(
        //     transform.position,
        //     transform.localScale * 0.5f,
        //     transform.rotation
        // );

        // foreach (var col in overlaps)
        // {
        //     if (col.CompareTag("Ground"))
        //     {
        //         float randomYOffset = Random.Range(2f, 4f);
        //         transform.position += new Vector3(0f, randomYOffset, 0f);
        //         iniPosition = transform.position;
        //         break;
        //     }
        // }
    }
}
