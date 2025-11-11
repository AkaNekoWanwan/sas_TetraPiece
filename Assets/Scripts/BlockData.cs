using UnityEngine;

[CreateAssetMenu(menuName = "Puzzle/BlockData")]
public class BlockData : ScriptableObject
{
    public string blockName;
    public Vector2Int position;   // 左下のグリッド座標（例：完成画像上で）
    public Vector2Int size;       // 幅と高さ（何マス分か）
    public GameObject prefab;     // 積み木のPrefab
}
