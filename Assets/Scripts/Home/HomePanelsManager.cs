using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;  
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
    public Image baseImage = default;
    public int StartIndex = 0;
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
        return; 
        if(UnityEditor.EditorApplication.isPlaying)
            return;
        // GridImageSplitterHome spritter = this.gameObject.GetComponentInChildren<GridImageSplitterHome>();
        // HomePanels = spritter.HomePanels;
        RectTransform rect = this.transform.GetChild(0).GetComponent<RectTransform>();
        rect.anchoredPosition = new Vector2(0f, 50f);
    }
#endif
        
    public void Initialize() {
        int totalLevel = PlayerPrefs.GetInt("totalLevel", 1);
        for(int i = 0; i < HomePanels.Count; i++)
        {
            int cellNum = StartIndex + HomePanels[i].cellNumber;
            HomePanels[i].NumText.text = "" + cellNum;
            HomePanels[i].UpdateView(cellNum, totalLevel);
        }
    }

    public void PlayClearAnimation()
    {
        Sequence seq = DOTween.Sequence();
        
        baseImage.color = new Color32(255, 255, 255, 0);
        baseImage.enabled = true;
        for(int i = 0; i < HomePanels.Count; i++)
        {
            int cellNum = StartIndex + HomePanels[i].cellNumber;
            HomePanels[i].rectTransform.DOSizeDelta(new Vector2(216, 216), 0.2f).SetLink(HomePanels[i].gameObject);
        }
        seq.Append(baseImage.DOColor(new Color32(255, 255, 255, 255), 0.2f).SetLink(baseImage.gameObject));
        seq.SetLink(this.gameObject);
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