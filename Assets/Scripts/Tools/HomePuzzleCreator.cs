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
    public List<HomePanelsManager> _panelManagers = default;
    public List<Sprite> _sprites = default;
    
    // スプライトリストの重複を削除
    private void RemoveDuplicateSprites()
    {
        List<Sprite> distinctSprites = _sprites
            .Where(sprite => sprite != null)
            .Distinct()
            .ToList();
        _sprites = distinctSprites;
    }
    
    // 使用している画像の名前を更新する
    public void UpdateSpriteFileName()
    {
        RemoveDuplicateSprites();
        
        // ファイル名を更新
        SpriteFileNameUtil.UpdateSpriteFileNames(_sprites, prefix: "Collection", startNumber: 1, enableLog: true);
    }
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public void CreatePiece()
    {
        RemoveDuplicateSprites();
        
        for(int i = 0; i < _panelManagers.Count; i++)
        {
            HomePanelsManager panelManager = _panelManagers[i];
            AbstractGridImageSplitter spritter = panelManager.gameObject.GetComponentInChildren<AbstractGridImageSplitter>();
            spritter.uniqueId = 10000 + i;
            spritter.index = i;
            Image image = panelManager.baseImage;
            if(i < _sprites.Count)
            {
                image.sprite = _sprites[i];
            }
            panelManager.gameObject.name = $"HomePanels{(i + 1).ToString("D3")}";
            panelManager.SplitImage();

            SaveAsPrefab.Save(panelManager.gameObject, PrefabSavePath);
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
        if(UnityEditor.EditorApplication.isPlaying)
            return;
        DrawDefaultInspector();

        HomePuzzleCreator script = (HomePuzzleCreator)target;

        GUILayout.Space(10);

        if (GUILayout.Button("Auto Create piece"))
        {
            script.CreatePiece();
        }
        
        if (GUILayout.Button("Update Sprite FileName"))
        {
            script.UpdateSpriteFileName();
        }
    }
}
#endif
