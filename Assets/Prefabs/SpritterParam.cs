using UnityEngine;

[CreateAssetMenu(fileName = "SpritterParam", menuName = "Scriptable Objects/SpritterParam")]
public class SpritterParam : ScriptableObject
{
    public Material OutLineMaterial;
    public Material AnswerMaterial;
    public Material CellsMaterial;
    public Color32 AnswerColor;
    public Color32 OutLineColor;
    public Vector2 OutLineSize;
}
