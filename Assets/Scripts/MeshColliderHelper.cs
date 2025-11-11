using UnityEngine;
using System.Collections.Generic;
using System.Linq;
[System.Serializable]
public class ColliderSettings
{
    [Header("Mesh Collider Settings")]
    public bool isConvex = true;
    [Range(4, 2000)]
    public int cookingOptions = 30;
    public bool isTrigger = false;
    public PhysicsMaterial physicMaterial;
    
    [Header("Convex Resolution Settings")]
    [Range(4, 10000)]
    public int maxVertexCount = 256;
    [Range(0.001f, 1f)]
    public float skinWidth = 0.01f;
    
    [Header("High Vertex Count Handling")]
    public bool useMultipleColliders = true;
    [Range(2, 10)]
    public int maxCollidersPerObject = 4;
    public bool usePrimitiveApproximation = false;
    public PrimitiveType primitiveType = PrimitiveType.Cube;
    
    [Header("Generation Options")]
    public bool generateForChildren = true;
    public bool replaceExistingColliders = true;
    public bool preserveOriginalMesh = true;
    
    [Header("Advanced Mesh Processing")]
    public bool useAdvancedSimplification = true;
    [Range(0.01f, 0.5f)]
    public float simplificationRatio = 0.1f;
    public bool preserveEdgeFeatures = true;
}

public class MeshColliderHelper : MonoBehaviour
{
    [SerializeField] private ColliderSettings settings = new ColliderSettings();
    
    [Header("Preview")]
    [SerializeField] private bool showPreview = true;
    [SerializeField] private Color previewColor = Color.green;
    
    [Header("Debug Info")]
    [SerializeField] private List<ColliderInfo> generatedColliders = new List<ColliderInfo>();
    
    [System.Serializable]
    public class ColliderInfo
    {
        public GameObject gameObject;
        public int originalVertexCount;
        public int convexVertexCount;
        public int numberOfColliders;
        public bool wasSuccessful;
        public string processingMethod;
    }

    [ContextMenu("Generate Convex Mesh Colliders")]
    public void GenerateConvexMeshColliders()
    {
        generatedColliders.Clear();
        
        if (settings.generateForChildren)
        {
            GenerateCollidersRecursive(transform);
        }
        else
        {
            GenerateColliderForObject(gameObject);
        }
        
        Debug.Log($"Generated {generatedColliders.Count} mesh colliders with convex settings.");
    }
    
    [ContextMenu("Remove All Mesh Colliders")]
    public void RemoveAllMeshColliders()
    {
        if (settings.generateForChildren)
        {
            RemoveCollidersRecursive(transform);
        }
        else
        {
            RemoveCollidersFromObject(gameObject);
        }
        
        generatedColliders.Clear();
        Debug.Log("Removed all mesh colliders.");
    }
    
    [ContextMenu("Optimize Existing Colliders")]
    public void OptimizeExistingColliders()
    {
        generatedColliders.Clear();
        
        if (settings.generateForChildren)
        {
            OptimizeCollidersRecursive(transform);
        }
        else
        {
            OptimizeCollidersForObject(gameObject);
        }
        
        Debug.Log($"Optimized {generatedColliders.Count} existing mesh colliders.");
    }

    private void GenerateCollidersRecursive(Transform parent)
    {
        foreach (Transform child in parent)
        {
            GenerateColliderForObject(child.gameObject);
            GenerateCollidersRecursive(child);
        }
    }
    
    private void RemoveCollidersRecursive(Transform parent)
    {
        foreach (Transform child in parent)
        {
            RemoveCollidersFromObject(child.gameObject);
            RemoveCollidersRecursive(child);
        }
    }
    
    private void OptimizeCollidersRecursive(Transform parent)
    {
        foreach (Transform child in parent)
        {
            OptimizeCollidersForObject(child.gameObject);
            OptimizeCollidersRecursive(child);
        }
    }

    private void GenerateColliderForObject(GameObject obj)
    {
        MeshFilter meshFilter = obj.GetComponent<MeshFilter>();
        if (meshFilter == null || meshFilter.sharedMesh == null) return;

        Mesh originalMesh = meshFilter.sharedMesh;
        
        // 既存のMeshColliderを削除（設定に応じて）
        if (settings.replaceExistingColliders)
        {
            RemoveCollidersFromObject(obj);
        }

        ColliderInfo info = new ColliderInfo
        {
            gameObject = obj,
            originalVertexCount = originalMesh.vertexCount,
            numberOfColliders = 0,
            wasSuccessful = false
        };

        // 頂点数に応じて処理方法を決定
        if (originalMesh.vertexCount > 255 && settings.useMultipleColliders)
        {
            // 複数のColliderに分割
            info = GenerateMultipleColliders(obj, originalMesh);
        }
        else if (originalMesh.vertexCount > 255 && settings.usePrimitiveApproximation)
        {
            // プリミティブ形状で近似
            info = GeneratePrimitiveCollider(obj, originalMesh);
        }
        else
        {
            // 単一のConvex Collider
            info = GenerateSingleConvexCollider(obj, originalMesh);
        }

        generatedColliders.Add(info);
    }

    private ColliderInfo GenerateMultipleColliders(GameObject obj, Mesh originalMesh)
    {
        ColliderInfo info = new ColliderInfo
        {
            gameObject = obj,
            originalVertexCount = originalMesh.vertexCount,
            processingMethod = "Multiple Colliders",
            numberOfColliders = 0
        };

        // メッシュを複数の部分に分割
        List<Mesh> submeshes = SplitMeshIntoSubmeshes(originalMesh, settings.maxCollidersPerObject);
        
        foreach (Mesh submesh in submeshes)
        {
            // 各サブメッシュに対してConvex Colliderを作成
            Mesh convexMesh = CreateOptimizedConvexMesh(submesh);
            
            if (convexMesh.vertexCount <= 255)
            {
                MeshCollider meshCollider = obj.AddComponent<MeshCollider>();
                meshCollider.sharedMesh = convexMesh;
                meshCollider.convex = settings.isConvex;
                meshCollider.isTrigger = settings.isTrigger;
                
                if (settings.physicMaterial != null)
                {
                    meshCollider.material = settings.physicMaterial;
                }
                
                info.numberOfColliders++;
                info.convexVertexCount += convexMesh.vertexCount;
            }
        }

        info.wasSuccessful = info.numberOfColliders > 0;
        return info;
    }

    private ColliderInfo GeneratePrimitiveCollider(GameObject obj, Mesh originalMesh)
    {
        ColliderInfo info = new ColliderInfo
        {
            gameObject = obj,
            originalVertexCount = originalMesh.vertexCount,
            processingMethod = "Primitive Approximation",
            numberOfColliders = 1
        };

        // プリミティブ形状のColliderを追加
        switch (settings.primitiveType)
        {
            case PrimitiveType.Cube:
                BoxCollider boxCollider = obj.AddComponent<BoxCollider>();
                Bounds bounds = originalMesh.bounds;
                boxCollider.center = bounds.center;
                boxCollider.size = bounds.size;
                if (settings.physicMaterial != null)
                    boxCollider.material = settings.physicMaterial;
                break;
                
            case PrimitiveType.Sphere:
                SphereCollider sphereCollider = obj.AddComponent<SphereCollider>();
                bounds = originalMesh.bounds;
                sphereCollider.center = bounds.center;
                sphereCollider.radius = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z) * 0.5f;
                if (settings.physicMaterial != null)
                    sphereCollider.material = settings.physicMaterial;
                break;
                
            case PrimitiveType.Capsule:
                CapsuleCollider capsuleCollider = obj.AddComponent<CapsuleCollider>();
                bounds = originalMesh.bounds;
                capsuleCollider.center = bounds.center;
                capsuleCollider.height = bounds.size.y;
                capsuleCollider.radius = Mathf.Max(bounds.size.x, bounds.size.z) * 0.5f;
                if (settings.physicMaterial != null)
                    capsuleCollider.material = settings.physicMaterial;
                break;
        }

        info.convexVertexCount = GetPrimitiveVertexCount(settings.primitiveType);
        info.wasSuccessful = true;
        return info;
    }

    private ColliderInfo GenerateSingleConvexCollider(GameObject obj, Mesh originalMesh)
    {
        ColliderInfo info = new ColliderInfo
        {
            gameObject = obj,
            originalVertexCount = originalMesh.vertexCount,
            processingMethod = "Single Convex",
            numberOfColliders = 1
        };

        MeshCollider meshCollider = obj.GetComponent<MeshCollider>();
        if (meshCollider == null)
        {
            meshCollider = obj.AddComponent<MeshCollider>();
        }

        // Convex用のメッシュを生成
        Mesh convexMesh = CreateOptimizedConvexMesh(originalMesh);
        
        // MeshColliderの設定
        meshCollider.sharedMesh = convexMesh;
        meshCollider.convex = settings.isConvex;
        meshCollider.isTrigger = settings.isTrigger;
        if (settings.physicMaterial != null)
        {
            meshCollider.material = settings.physicMaterial;
        }

        info.convexVertexCount = convexMesh.vertexCount;
        info.wasSuccessful = meshCollider.sharedMesh != null;
        return info;
    }

    private List<Mesh> SplitMeshIntoSubmeshes(Mesh originalMesh, int maxSubmeshes)
    {
        List<Mesh> submeshes = new List<Mesh>();
        Vector3[] vertices = originalMesh.vertices;
        int[] triangles = originalMesh.triangles;

        // 空間分割による分割
        Bounds bounds = originalMesh.bounds;
        int divisionsPerAxis = Mathf.CeilToInt(Mathf.Pow(maxSubmeshes, 1f/3f));
        
        Vector3 cellSize = new Vector3(
            bounds.size.x / divisionsPerAxis,
            bounds.size.y / divisionsPerAxis,
            bounds.size.z / divisionsPerAxis
        );

        for (int x = 0; x < divisionsPerAxis; x++)
        {
            for (int y = 0; y < divisionsPerAxis; y++)
            {
                for (int z = 0; z < divisionsPerAxis; z++)
                {
                    if (submeshes.Count >= maxSubmeshes) break;

                    Vector3 cellMin = bounds.min + new Vector3(x * cellSize.x, y * cellSize.y, z * cellSize.z);
                    Vector3 cellMax = cellMin + cellSize;
                    
                    Mesh submesh = ExtractMeshInBounds(originalMesh, cellMin, cellMax);
                    if (submesh != null && submesh.vertexCount > 0)
                    {
                        submeshes.Add(submesh);
                    }
                }
            }
        }

        return submeshes;
    }

    private Mesh ExtractMeshInBounds(Mesh originalMesh, Vector3 boundsMin, Vector3 boundsMax)
    {
        Vector3[] vertices = originalMesh.vertices;
        int[] triangles = originalMesh.triangles;
        
        List<Vector3> newVertices = new List<Vector3>();
        List<int> newTriangles = new List<int>();
        Dictionary<int, int> vertexMapping = new Dictionary<int, int>();

        // 境界内の三角形を抽出
        for (int i = 0; i < triangles.Length; i += 3)
        {
            Vector3 v1 = vertices[triangles[i]];
            Vector3 v2 = vertices[triangles[i + 1]];
            Vector3 v3 = vertices[triangles[i + 2]];

            // 三角形の重心が境界内にあるかチェック
            Vector3 center = (v1 + v2 + v3) / 3f;
            
            if (center.x >= boundsMin.x && center.x <= boundsMax.x &&
                center.y >= boundsMin.y && center.y <= boundsMax.y &&
                center.z >= boundsMin.z && center.z <= boundsMax.z)
            {
                // 頂点をマッピング
                for (int j = 0; j < 3; j++)
                {
                    int originalIndex = triangles[i + j];
                    if (!vertexMapping.ContainsKey(originalIndex))
                    {
                        vertexMapping[originalIndex] = newVertices.Count;
                        newVertices.Add(vertices[originalIndex]);
                    }
                    newTriangles.Add(vertexMapping[originalIndex]);
                }
            }
        }

        if (newVertices.Count == 0) return null;

        Mesh newMesh = new Mesh();
        newMesh.vertices = newVertices.ToArray();
        newMesh.triangles = newTriangles.ToArray();
        newMesh.RecalculateNormals();
        newMesh.RecalculateBounds();

        return newMesh;
    }

    private int GetPrimitiveVertexCount(PrimitiveType primitiveType)
    {
        switch (primitiveType)
        {
            case PrimitiveType.Cube: return 8;
            case PrimitiveType.Sphere: return 24;
            case PrimitiveType.Capsule: return 32;
            default: return 8;
        }
    }

    private void RemoveCollidersFromObject(GameObject obj)
    {
        MeshCollider[] colliders = obj.GetComponents<MeshCollider>();
        for (int i = colliders.Length - 1; i >= 0; i--)
        {
            if (Application.isPlaying)
            {
                Destroy(colliders[i]);
            }
            else
            {
                DestroyImmediate(colliders[i]);
            }
        }
    }

    private void OptimizeCollidersForObject(GameObject obj)
    {
        MeshCollider meshCollider = obj.GetComponent<MeshCollider>();
        if (meshCollider == null || meshCollider.sharedMesh == null) return;

        Mesh originalMesh = meshCollider.sharedMesh;
        Mesh optimizedMesh = CreateOptimizedConvexMesh(originalMesh);
        
        meshCollider.sharedMesh = optimizedMesh;
        meshCollider.convex = settings.isConvex;
        meshCollider.isTrigger = settings.isTrigger;
        
        if (settings.physicMaterial != null)
        {
            meshCollider.material = settings.physicMaterial;
        }

        ColliderInfo info = new ColliderInfo
        {
            gameObject = obj,
            originalVertexCount = originalMesh.vertexCount,
            convexVertexCount = optimizedMesh.vertexCount,
            wasSuccessful = true
        };
        generatedColliders.Add(info);
    }

    private Mesh CreateOptimizedConvexMesh(Mesh originalMesh)
    {
        if (originalMesh.vertexCount <= 255)
        {
            // 255以下の場合はそのまま使用可能
            Mesh mesh = new Mesh();
            mesh.vertices = originalMesh.vertices;
            mesh.triangles = originalMesh.triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // 255を超える場合は高度な簡略化を使用
        if (settings.useAdvancedSimplification)
        {
            return CreateAdvancedSimplifiedMesh(originalMesh);
        }
        else
        {
            return SimplifyMeshForConvex(originalMesh);
        }
    }

    private Mesh CreateAdvancedSimplifiedMesh(Mesh originalMesh)
    {
        Vector3[] originalVertices = originalMesh.vertices;
        int[] originalTriangles = originalMesh.triangles;
        
        // ターゲット頂点数を計算
        int targetVertexCount = Mathf.Min(settings.maxVertexCount, originalVertices.Length);
        
        // エッジ保持を考慮した簡略化
        List<Vector3> simplifiedVertices = new List<Vector3>();
        List<int> simplifiedTriangles = new List<int>();
        
        // 1. 重要な頂点を特定（境界、角など）
        HashSet<int> importantVertices = FindImportantVertices(originalMesh);
        
        // 2. 重要な頂点を必ず含める
        Dictionary<int, int> vertexMapping = new Dictionary<int, int>();
        foreach (int importantVertex in importantVertices)
        {
            if (simplifiedVertices.Count >= targetVertexCount) break;
            
            vertexMapping[importantVertex] = simplifiedVertices.Count;
            simplifiedVertices.Add(originalVertices[importantVertex]);
        }
        
        // 3. 残りの頂点を均等にサンプリング
        float step = (float)originalVertices.Length / (targetVertexCount - simplifiedVertices.Count);
        for (int i = 0; i < originalVertices.Length && simplifiedVertices.Count < targetVertexCount; i++)
        {
            int index = Mathf.RoundToInt(i * step);
            if (index < originalVertices.Length && !vertexMapping.ContainsKey(index))
            {
                vertexMapping[index] = simplifiedVertices.Count;
                simplifiedVertices.Add(originalVertices[index]);
            }
        }
        
        // 4. 三角形を再構築（Delaunay風）
        GenerateConvexTriangles(simplifiedVertices, simplifiedTriangles);
        
        Mesh newMesh = new Mesh();
        newMesh.vertices = simplifiedVertices.ToArray();
        newMesh.triangles = simplifiedTriangles.ToArray();
        newMesh.RecalculateNormals();
        newMesh.RecalculateBounds();
        
        return newMesh;
    }

    private HashSet<int> FindImportantVertices(Mesh mesh)
    {
        HashSet<int> importantVertices = new HashSet<int>();
        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;
        
        // 境界ボックスの頂点付近
        Bounds bounds = mesh.bounds;
        float threshold = bounds.size.magnitude * 0.1f;
        
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 vertex = vertices[i];
            
            // 境界に近い頂点
            if (Mathf.Abs(vertex.x - bounds.min.x) < threshold ||
                Mathf.Abs(vertex.x - bounds.max.x) < threshold ||
                Mathf.Abs(vertex.y - bounds.min.y) < threshold ||
                Mathf.Abs(vertex.y - bounds.max.y) < threshold ||
                Mathf.Abs(vertex.z - bounds.min.z) < threshold ||
                Mathf.Abs(vertex.z - bounds.max.z) < threshold)
            {
                importantVertices.Add(i);
            }
        }
        
        // エッジ検出による重要な頂点
        Dictionary<int, List<int>> adjacency = BuildAdjacencyList(triangles);
        for (int i = 0; i < vertices.Length; i++)
        {
            if (adjacency.ContainsKey(i))
            {
                // 隣接頂点数が少ない（エッジ部分）または多い（ハブ）頂点
                int adjacentCount = adjacency[i].Count;
                if (adjacentCount <= 3 || adjacentCount >= 8)
                {
                    importantVertices.Add(i);
                }
            }
        }
        
        return importantVertices;
    }

    private Dictionary<int, List<int>> BuildAdjacencyList(int[] triangles)
    {
        Dictionary<int, List<int>> adjacency = new Dictionary<int, List<int>>();
        
        for (int i = 0; i < triangles.Length; i += 3)
        {
            int v1 = triangles[i];
            int v2 = triangles[i + 1];
            int v3 = triangles[i + 2];
            
            AddAdjacency(adjacency, v1, v2);
            AddAdjacency(adjacency, v1, v3);
            AddAdjacency(adjacency, v2, v1);
            AddAdjacency(adjacency, v2, v3);
            AddAdjacency(adjacency, v3, v1);
            AddAdjacency(adjacency, v3, v2);
        }
        
        return adjacency;
    }

    private void AddAdjacency(Dictionary<int, List<int>> adjacency, int from, int to)
    {
        if (!adjacency.ContainsKey(from))
        {
            adjacency[from] = new List<int>();
        }
        if (!adjacency[from].Contains(to))
        {
            adjacency[from].Add(to);
        }
    }

    private void GenerateConvexTriangles(List<Vector3> vertices, List<int> triangles)
    {
        // 簡易的なConvex Hull生成
        if (vertices.Count < 4) return;
        
        // 最初の四面体を作成
        triangles.Add(0); triangles.Add(1); triangles.Add(2);
        triangles.Add(0); triangles.Add(1); triangles.Add(3);
        triangles.Add(0); triangles.Add(2); triangles.Add(3);
        triangles.Add(1); triangles.Add(2); triangles.Add(3);
        
        // 残りの頂点に対してConvex Hullを拡張
        for (int i = 4; i < vertices.Count; i++)
        {
            // 簡略化のため、基本的な三角形のみ追加
            if (triangles.Count < vertices.Count * 6) // 安全な上限
            {
                triangles.Add(0);
                triangles.Add(i);
                triangles.Add((i + 1) % vertices.Count);
            }
        }
    }

    private Mesh SimplifyMeshForConvex(Mesh originalMesh)
    {
        Vector3[] originalVertices = originalMesh.vertices;
        List<Vector3> simplifiedVertices = new List<Vector3>();

        // ConvexHullアルゴリズムの簡易版
        // まず、境界ボックスの頂点を追加
        Bounds bounds = originalMesh.bounds;
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;
        
        simplifiedVertices.Add(new Vector3(min.x, min.y, min.z));
        simplifiedVertices.Add(new Vector3(max.x, min.y, min.z));
        simplifiedVertices.Add(new Vector3(min.x, max.y, min.z));
        simplifiedVertices.Add(new Vector3(max.x, max.y, min.z));
        simplifiedVertices.Add(new Vector3(min.x, min.y, max.z));
        simplifiedVertices.Add(new Vector3(max.x, min.y, max.z));
        simplifiedVertices.Add(new Vector3(min.x, max.y, max.z));
        simplifiedVertices.Add(new Vector3(max.x, max.y, max.z));

        // 残りの頂点を距離ベースでサンプリング
        float minDistance = bounds.size.magnitude / settings.maxVertexCount;
        
        foreach (Vector3 vertex in originalVertices)
        {
            bool canAdd = true;
            foreach (Vector3 existing in simplifiedVertices)
            {
                if (Vector3.Distance(vertex, existing) < minDistance)
                {
                    canAdd = false;
                    break;
                }
            }
            
            if (canAdd && simplifiedVertices.Count < settings.maxVertexCount)
            {
                simplifiedVertices.Add(vertex);
            }
        }

        // 新しいメッシュを作成
        Mesh newMesh = new Mesh();
        newMesh.vertices = simplifiedVertices.ToArray();
        
        // 簡単な三角形生成（ConvexHullの代替）
        List<int> triangles = new List<int>();
        for (int i = 0; i < simplifiedVertices.Count - 2; i++)
        {
            triangles.Add(0);
            triangles.Add(i + 1);
            triangles.Add(i + 2);
        }
        
        newMesh.triangles = triangles.ToArray();
        newMesh.RecalculateNormals();
        newMesh.RecalculateBounds();
        
        return newMesh;
    }

    private void OnDrawGizmos()
    {
        if (!showPreview) return;

        Gizmos.color = previewColor;
        
        if (settings.generateForChildren)
        {
            DrawGizmosRecursive(transform);
        }
        else
        {
            DrawGizmosForObject(gameObject);
        }
    }

    private void DrawGizmosRecursive(Transform parent)
    {
        foreach (Transform child in parent)
        {
            DrawGizmosForObject(child.gameObject);
            DrawGizmosRecursive(child);
        }
    }

    private void DrawGizmosForObject(GameObject obj)
    {
        MeshCollider collider = obj.GetComponent<MeshCollider>();
        if (collider != null && collider.sharedMesh != null)
        {
            Gizmos.matrix = obj.transform.localToWorldMatrix;
            Gizmos.DrawWireMesh(collider.sharedMesh);
        }
    }

    [ContextMenu("Print Collider Statistics")]
    public void PrintColliderStatistics()
    {
        Debug.Log("=== Mesh Collider Statistics ===");
        foreach (var info in generatedColliders)
        {
            if (info.gameObject != null)
            {
                float reduction = info.originalVertexCount > 0 ? 
                    (1f - (float)info.convexVertexCount / info.originalVertexCount) * 100f : 0f;
                    
                Debug.Log($"{info.gameObject.name}: {info.originalVertexCount} → {info.convexVertexCount} vertices " +
                         $"(-{reduction:F1}%) | Method: {info.processingMethod} | Colliders: {info.numberOfColliders}");
            }
        }
        
        int totalColliders = generatedColliders.Sum(info => info.numberOfColliders);
        int successfulObjects = generatedColliders.Count(info => info.wasSuccessful);
        
        Debug.Log($"=== Summary ===");
        Debug.Log($"Total Objects Processed: {generatedColliders.Count}");
        Debug.Log($"Successful: {successfulObjects}");
        Debug.Log($"Total Colliders Generated: {totalColliders}");
    }
}