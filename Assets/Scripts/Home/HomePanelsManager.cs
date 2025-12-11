using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEngine.SceneManagement; // SceneManagerを使用するために必要
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEditor.SceneManagement; // 忘れずに using 宣言を追加
using System.Linq;
#endif

public class HomePanelsManager : MonoBehaviour
{
    public List<HomePanel> HomePanels = default;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
#if UNITY_EDITOR
    public void SplitImage()
    {
        GridImageSplitterHome spritter = this.gameObject.GetComponentInChildren<GridImageSplitterHome>();
        spritter.Deletepiece();
        spritter.SplitImage();
        HomePanels = spritter.HomePanels;
    }

    private void OnValidate() {
        if(UnityEditor.EditorApplication.isPlaying)
            return;
        GridImageSplitterHome spritter = this.gameObject.GetComponentInChildren<GridImageSplitterHome>();
        HomePanels = spritter.HomePanels;
    }
#endif
        
    private void Start() {
        int totalLevel = PlayerPrefs.GetInt("totalLevel", 1);
        int startIndex = ( totalLevel - 1 ) / 30 * 30;
        for(int i = 0; i < HomePanels.Count; i++)
        {
            int cellNum = startIndex + HomePanels[i].cellNumber;
            Debug.Log($"数字セット！：totalLevel:{totalLevel}, cellNum:{cellNum}, i:{i}, startIndex:{startIndex}, ");
            // HomePanels[i].cellNumber = cellNum;
            HomePanels[i].NumText.text = "" + cellNum;
            HomePanels[i].UpdateView(totalLevel);
        }
    }
}
#if UNITY_EDITOR
    [CustomEditor(typeof(HomePanelsManager))]
    public class HomePanelsManagerEditor : Editor
    {
        public void OnEnable()
        {
            // OnEnableで設定することでエラーが解消されます
            // base.canEditMultipleObjects = true; 
        }
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            HomePanelsManager generator = (HomePanelsManager)target;

            // シーンのルートにあるオブジェクトを取得する
            GameObject[] rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();

            foreach (GameObject obj in rootObjects)
            {
                Debug.Log("オブジェクト名: " + obj.name);
                HomePanelsManager wordGenerator = obj.GetComponent<HomePanelsManager>();
                // オブジェクトがWordGeneratorコンポーネントを持っているかチェック
                if (wordGenerator != null && wordGenerator != generator)
                {
                    wordGenerator.gameObject.SetActive(false);
                }
            }
            Debug.Log("オブジェクトが選択されました。");
            generator.gameObject.SetActive(true);

            // 選択されているすべてのStageInfoコンポーネントを取得
            HomePanelsManager[] scripts = targets.Cast<HomePanelsManager>().ToArray();

            if (GUILayout.Button("画像分割"))
            {
                // 処理をUndo可能にするための記述（推奨）
                Undo.RecordObjects(scripts, "SetUp Stages"); 

                foreach (HomePanelsManager script in scripts)
                {
                    // 各 StageInfo インスタンスに対して処理を実行
                    script.SplitImage(); 
                }
            }
        }
    }
#endif