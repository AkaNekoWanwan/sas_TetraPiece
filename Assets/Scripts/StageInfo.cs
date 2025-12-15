using UnityEngine;
#if UNITY_EDITOR
using UnityEngine.SceneManagement; // SceneManagerを使用するために必要
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEditor.SceneManagement; // 忘れずに using 宣言を追加
using System.Linq;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
#endif


public class StageInfo : MonoBehaviour
{
    public string stageName;
    public bool isHard = false;

#if UNITY_EDITOR
    public bool isAddressabble = false;

    public void SetUpStage()
    {
        AbstractGridImageSplitter spritter = this.gameObject.GetComponentInChildren<AbstractGridImageSplitter>();
        spritter.CreatePiece();
    }
    public void SplitImage()
    {
        AbstractGridImageSplitter spritter = this.gameObject.GetComponentInChildren<AbstractGridImageSplitter>();
        spritter.Deletepiece();
        spritter.SplitImage();
    }
    public void Addressable()
    {
        AbstractGridImageSplitter spritter = this.gameObject.GetComponentInChildren<AbstractGridImageSplitter>();
        spritter.Addressable();
    }
#endif
}

#if UNITY_EDITOR
    [CustomEditor(typeof(StageInfo))]
    [CanEditMultipleObjects]
    public class StageInfoEditor : Editor
    {
        // public bool canEditMultipleObjects => true;
        // public void OnEnable()
        // {
        //     // ベースの canEditMultipleObjects に値を設定します。
        //     base.canEditMultipleObjects = true;
        // }
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            StageInfo generator = (StageInfo)target;

            // シーンのルートにあるオブジェクトを取得する
            GameObject[] rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();

            foreach (GameObject obj in rootObjects)
            {
                // Debug.Log("オブジェクト名: " + obj.name);
                StageInfo wordGenerator = obj.GetComponent<StageInfo>();
                // オブジェクトがWordGeneratorコンポーネントを持っているかチェック
                if (wordGenerator != null && wordGenerator != generator)
                {
                    // wordGenerator.gameObject.SetActive(false);
                }
            }
            // Debug.Log("オブジェクトが選択されました。");
            // generator.gameObject.SetActive(true);

// 選択されているすべてのStageInfoコンポーネントを取得
            StageInfo[] scripts = targets.Cast<StageInfo>().ToArray();
            int totalCount = scripts.Length;


            if (GUILayout.Button("SetUp (選択全体に適用)"))
            {
                // 処理をUndo可能にするための記述（推奨）
                Undo.RecordObjects(scripts, "SetUp Stages"); 

                for (int i = 0; i < totalCount; i++)
                {
                    StageInfo script = scripts[i];
                    string title = $"Addressable設定中 ({i + 1}/{totalCount})";
                    string info = $"ステージ: {script.gameObject.name} をセットアップ中...";
                    float progress = (float)i / totalCount;
                    
                    // 進捗バーを表示・更新
                    EditorUtility.DisplayProgressBar(title, info, progress);

                    script.SetUpStage(); 
                }
                // 処理が完了したら進捗バーを閉じる
                EditorUtility.ClearProgressBar();
            }
            
            if (GUILayout.Button("Addressable (選択全体に適用)"))
            {
                // 処理をUndo可能にするための記述（推奨）
                Undo.RecordObjects(scripts, "Addressable Stages"); 

                for (int i = 0; i < totalCount; i++)
                {
                    StageInfo script = scripts[i];
                    if(script.isAddressabble)
                        return;
                    string title = $"Addressable設定中 ({i + 1}/{totalCount})";
                    string info = $"ステージ: {script.gameObject.name} をAddressableに登録中...";
                    float progress = (float)i / totalCount;
                    
                    // 進捗バーを表示・更新
                    // キャンセルボタンを押された場合、処理を中断
                    if (EditorUtility.DisplayCancelableProgressBar(title, info, progress))
                    {
                        Debug.Log("Addressable設定がユーザーによって中断されました。");
                        break; // ループを抜ける
                    }

                    try{
                        script.Addressable(); 
                        script.isAddressabble = true;
                    }
                    catch(Exception)
                    {
                        Debug.Log($"エラー：{script.gameObject.name}");
                        continue;
                    }
                    // 各 StageInfo インスタンスに対して処理を実行   
                }
                
                // 処理が完了したら進捗バーを閉じる
                EditorUtility.ClearProgressBar();
                Debug.Log($"Addressable設定が完了しました。対象数: {totalCount} 件");
            }
            
            if (GUILayout.Button("画像分割"))
            {
                // 処理をUndo可能にするための記述（推奨）
                Undo.RecordObjects(scripts, "Split Image"); 

                for (int i = 0; i < totalCount; i++)
                {
                    StageInfo script = scripts[i];
                    string title = $"画像分割中 ({i + 1}/{totalCount})";
                    string info = $"ステージ: {script.gameObject.name} の画像を分割中...";
                    float progress = (float)i / totalCount;
                    
                    // 進捗バーを表示・更新
                    EditorUtility.DisplayProgressBar(title, info, progress);

                    script.SplitImage(); 
                }
                // 処理が完了したら進捗バーを閉じる
                EditorUtility.ClearProgressBar();
            }
        }
    }
#endif