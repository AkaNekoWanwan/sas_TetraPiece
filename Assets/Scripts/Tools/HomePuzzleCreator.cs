using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement; // SceneManagerを使用するために必要
using System; // ShapeTypeを使用するために必要
using System.Threading.Tasks;
using System.Threading;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

public class HomePuzzleCreator : MonoBehaviour
{
#if UNITY_EDITOR
    public string PrefabSavePath = "Assets/Prefabs/HomePuzzles"; // プレハブ保存先ディレクトリ
    public List<StageInfo> _stageInfos = default;
    public List<Sprite> _sprites = default;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public void CreatePiece()
    {
        for(int i = 0; i < _stageInfos.Count; i++)
        {
            StageInfo info = _stageInfos[i];
            AbstractGridImageSplitter spritter = info.gameObject.GetComponentInChildren<AbstractGridImageSplitter>();
            spritter.uniqueId = 10000 + i;
            spritter.index = i;
            Image image = info.gameObject.GetComponentInChildren<Image>();
            if(i < _sprites.Count)
            {
                image.sprite = _sprites[i];
            }
            info.gameObject.name = $"HomePanels{(i + 1).ToString("D3")}";
            info.SplitImage();

            SaveAsPrefab.Save(info.gameObject, PrefabSavePath);
        }
    }
#endif
}

#if UNITY_EDITOR
[CustomEditor(typeof(HomePuzzleCreator), true)]
public class HomePuzzleCreatorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        HomePuzzleCreator script = (HomePuzzleCreator)target;

        GUILayout.Space(10);

        if (GUILayout.Button("Auto Create piece"))
        {
            script.CreatePiece();
        }
    }
}
#endif
