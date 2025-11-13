using UnityEngine;
#if UNITY_EDITOR
using UnityEngine.SceneManagement; // SceneManagerを使用するために必要
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEditor.SceneManagement; // 忘れずに using 宣言を追加
using System.Linq;
#endif

public class StageInfo : MonoBehaviour
{
    public string stageName;
    public bool isHard = false;

#if UNITY_EDITOR
    public void SetUpStage()
    {
        AbstractGridImageSplitter spritter = this.gameObject.GetComponentInChildren<AbstractGridImageSplitter>();
        spritter.CreatePiece();
    }
#endif
}

#if UNITY_EDITOR
    [CustomEditor(typeof(StageInfo))]
    public class StageInfoEditor : Editor
    {
        public void OnEnable()
        {
            // OnEnableで設定することでエラーが解消されます
            // base.canEditMultipleObjects = true; 
        }
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            StageInfo generator = (StageInfo)target;

            // シーンのルートにあるオブジェクトを取得する
            GameObject[] rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();

            foreach (GameObject obj in rootObjects)
            {
                Debug.Log("オブジェクト名: " + obj.name);
                StageInfo wordGenerator = obj.GetComponent<StageInfo>();
                // オブジェクトがWordGeneratorコンポーネントを持っているかチェック
                if (wordGenerator != null && wordGenerator != generator)
                {
                    wordGenerator.gameObject.SetActive(false);
                }
            }
            Debug.Log("オブジェクトが選択されました。");
            generator.gameObject.SetActive(true);

            // 選択されているすべてのStageInfoコンポーネントを取得
            StageInfo[] scripts = targets.Cast<StageInfo>().ToArray();

            if (GUILayout.Button("SetUp (選択全体に適用)"))
            {
                // 処理をUndo可能にするための記述（推奨）
                Undo.RecordObjects(scripts, "SetUp Stages"); 

                foreach (StageInfo script in scripts)
                {
                    // 各 StageInfo インスタンスに対して処理を実行
                    script.SetUpStage(); 
                }
            }
        }
    }
#endif