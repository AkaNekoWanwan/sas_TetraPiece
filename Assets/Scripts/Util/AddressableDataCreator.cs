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

public class AddressableDataCreator : MonoBehaviour
{
    public List<AbstractGridImageSplitter> _allSplitters;
#if UNITY_EDITOR
    public void CreateData()
    {
        
    }
    public void GetSpritters()
    {
        _allSplitters = new List<AbstractGridImageSplitter>();
        var splitters = Resources.FindObjectsOfTypeAll<AbstractGridImageSplitter>().ToList();
        
        foreach (var splitter in splitters) {
            //Hierarchy上のものでなければスルー
            if (!AssetDatabase.GetAssetOrScenePath(splitter).Contains(".unity")) {
                continue;
            }
            if(splitter.isPrefs || splitter.isCreative)
                continue;
            _allSplitters.Add(splitter);

            splitter.transform.name = splitter.transform.parent.parent.name + "_Splitter";
        }
    }
#endif
}



#if UNITY_EDITOR
[CustomEditor(typeof(AddressableDataCreator))]
public class AddressableDataCreatorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        AddressableDataCreator script = (AddressableDataCreator)target;

        if (GUILayout.Button("CreateData"))
        {
            script.CreateData();
        }
        if (GUILayout.Button("GetSpritters"))
        {
            script.GetSpritters();
        }
        GUILayout.Space(10);
        DrawDefaultInspector();
    }
}
#endif
