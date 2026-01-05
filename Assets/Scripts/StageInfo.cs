using UnityEngine;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.IO;

#if UNITY_EDITOR
using UnityEngine.SceneManagement; // SceneManagerを使用するために必要
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEditor.SceneManagement; // 忘れずに using 宣言を追加
using System.Linq;
#endif

public class StageInfo : MonoBehaviour
{
    public AbstractGridImageSplitter _spritter;
    public Action<int, int, string> OnUpdateProgressBar;
    public string stageName;
    public bool isHard = false;

    public AbstractGridImageSplitter Spritter{
        get{
            if(_spritter == null)
            {
                _spritter = this.gameObject.GetComponentInChildren<AbstractGridImageSplitter>();
            }
            return _spritter;
        }
    }

#if UNITY_EDITOR
    public void SetUpStage()
    {
        Spritter.OnUpdateProgressBar = OnUpdateProgressBar;
        Spritter.CreatePiece();
    }
    public void SplitImage()
    {
        Spritter.Deletepiece();
        Spritter.SplitImage();
    }   
    public void Addressable()
    {
        Spritter.Addressable(true);
    }
    public void StageAddressable()
    {
        Spritter.AddressableStage();
        Spritter.Addressable(false);
    }
#endif
}

#if UNITY_EDITOR
    [CustomEditor(typeof(StageInfo))]
    [CanEditMultipleObjects]
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
            int totalCount = scripts.Length;


            if (GUILayout.Button("SetUp (選択全体に適用)"))
            {
                // 処理をUndo可能にするための記述（推奨）
                Undo.RecordObjects(scripts, "SetUp Stages"); 

                for (int i = 0; i < totalCount; i++)
                {
                    StageInfo script = scripts[i];
                    string title = $"ステージ生成中 ({i + 1}/{totalCount})";
                    string info = $"ステージ: {script.gameObject.name} をセットアップ中...";
                    float progress = (float)i / totalCount;
                    script.OnUpdateProgressBar += (current, total, subInfo) =>
                    {
                        float subProgress = (float)current / total;
                        EditorUtility.DisplayProgressBar(title, info + " " + subInfo, progress + subProgress / totalCount);
                    };
                    
                    // 進捗バーを表示・更新
                    EditorUtility.DisplayProgressBar(title, info, progress);

                    script.SetUpStage(); 
                    script.OnUpdateProgressBar = null; // イベントハンドラをリセット
                }
                EditorUtility.ClearProgressBar();
            }
            if (GUILayout.Button("Addressable (選択全体に適用)"))
            {
                // 処理をUndo可能にするための記述（推奨）
                Undo.RecordObjects(scripts, "Addressable Stages"); 

                for (int i = 0; i < totalCount; i++)
                {
                    StageInfo script = scripts[i];
                    string title = $"Addressable設定中 ({i + 1}/{totalCount})";
                    string info = $"ステージ: {script.gameObject.name} をAddressableに登録中...";
                    float progress = (float)i / totalCount;
                    script.OnUpdateProgressBar += (current, total, subInfo) =>
                    {
                        float subProgress = (float)current / total;
                        EditorUtility.DisplayProgressBar(title, info + " " + subInfo, progress + subProgress / totalCount);
                    };
                    
                    // 進捗バーを表示・更新
                    // キャンセルボタンを押された場合、処理を中断
                    if (EditorUtility.DisplayCancelableProgressBar(title, info, progress))
                    {
                        Debug.Log("Addressable設定がユーザーによって中断されました。");
                        break; // ループを抜ける
                    }

                    try{
                        script.Addressable(); 
                        script.OnUpdateProgressBar = null; // イベントハンドラをリセット
                    }
                    catch(Exception)
                    {
                        Debug.Log($"エラー：{script.gameObject.name}");
                        script.OnUpdateProgressBar = null; // イベントハンドラをリセット
                        continue;
                    }
                    // 各 StageInfo インスタンスに対して処理を実行   
                }
                
                // 処理が完了したら進捗バーを閉じる
                EditorUtility.ClearProgressBar();
                Debug.Log($"Addressable設定が完了しました。対象数: {totalCount} 件");
            }

            if(GUILayout.Button("StageのAddressable化(選択全体に適用)"))
            {
                // 処理をUndo可能にするための記述（推奨）
                Undo.RecordObjects(scripts, "Addressable Stage Stages"); 

                for (int i = 0; i < totalCount; i++)
                {
                    StageInfo script = scripts[i];
                    string title = $"Addressable設定中 ({i + 1}/{totalCount})";
                    string info = $"ステージ: {script.gameObject.name} をStageグループに登録中...";
                    float progress = (float)i / totalCount;
                    
                    // 進捗バーを表示・更新
                    EditorUtility.DisplayProgressBar(title, info, progress);
                    script.StageAddressable(); 
                }
                // 処理が完了したら進捗バーを閉じる
                EditorUtility.ClearProgressBar();
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

            // プレハブを保存するだけのボタン
            if (GUILayout.Button("プレハブ保存 (選択全体に適用)"))
            {
                // 処理をUndo可能にするための記述（推奨）
                Undo.RecordObjects(scripts, "Save Prefabs");    
                for (int i = 0; i < totalCount; i++)
                {
                    StageInfo script = scripts[i];
                    string title = $"プレハブ保存中 ({i + 1}/{totalCount})";
                    string info = $"ステージ: {script.gameObject.name} のプレハブを保存中...";
                    float progress = (float)i / totalCount;
                    
                    // 進捗バーを表示・更新
                    EditorUtility.DisplayProgressBar(title, info, progress);

                    // プレハブ保存処理
                    string prefabPath = $"Assets/Prefabs/Stages/{script.gameObject.name}.prefab";
                    PrefabUtility.SaveAsPrefabAssetAndConnect(script.gameObject, prefabPath, InteractionMode.UserAction);
                }
                // 処理が完了したら進捗バーを閉じる
                EditorUtility.ClearProgressBar();
            }
        }
    }
#endif