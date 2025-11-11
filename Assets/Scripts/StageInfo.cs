using UnityEngine;
#if UNITY_EDITOR
using UnityEngine.SceneManagement; // SceneManagerを使用するために必要
using Unity.EditorCoroutines.Editor;
using UnityEditor;
#endif

public class StageInfo : MonoBehaviour
{
    public string stageName;
    
}

#if UNITY_EDITOR
    [CustomEditor(typeof(StageInfo))]
    public class StageInfoEditor : Editor
    {
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
        }
    }
#endif