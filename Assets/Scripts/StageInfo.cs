using UnityEngine;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.IO;
using UnityEngine.UI;

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
    private void OnValidate() {
        // プレハブインスタンスまたはプレハブアセット内では変更できないのでスキップ
        if (PrefabUtility.IsPartOfPrefabInstance(this) || PrefabUtility.IsPartOfPrefabAsset(this))
        {
            return;
        }

        // 下の階層にScrollRectコンポーネントがあったらそのコンポーネントを削除する(そのオブジェクト自体は消さない)
        ScrollRect scrollRectComponent = this.GetComponentInChildren<ScrollRect>();
        if(scrollRectComponent != null)
        {
            // OnValidate内ではDestroyImmediateが使えないため、次のフレームで削除
            EditorApplication.delayCall += () => 
            {
                if (scrollRectComponent != null)
                {
                    DestroyImmediate(scrollRectComponent);
                }
            };
        }
        
        // 下の階層に「ScrollRect」という名前のオブジェクトがある場合、その中の「PieceList」オブジェクトの親を「ScrollRect」の親にしたのち、「ScrollRect」オブジェクトを削除する
        Transform scrollRectTransform = this.transform.Find("Canvas/ScrollRect");
        if(scrollRectTransform != null)
        {
            Transform pieceListTransform = scrollRectTransform.Find("PieceList");  
            if(pieceListTransform == null)
            {
                pieceListTransform = this.transform.Find("Canvas/PieceList");
            }
            if(pieceListTransform != null)
            {
                Transform parentTransform = scrollRectTransform.parent;
                GameObject scrollRectObj = scrollRectTransform.gameObject;
                
                pieceListTransform.SetParent(parentTransform);
                
                // OnValidate内ではDestroyImmediateが使えないため、次のフレームで削除
                EditorApplication.delayCall += () =>
                {
                    if (scrollRectObj != null)
                    {
                        DestroyImmediate(scrollRectObj);
                    }
                };
            }
        }
    }
 
    // 引数：セルの再生成をせずに既存のセルからピースを再生成するかどうか
    public void SetUpStage(bool isReSetPiecesOnly = false)
    {
        OnUpdateProgressBar?.Invoke(0, 100, "Spritter取得中...");
        if(Spritter == null)
        {
            Debug.LogError("Spritterが設定されていません。");
            return;
        }
        Spritter.OnUpdateProgressBar = OnUpdateProgressBar;
        Spritter.CreatePiece(isReSetPiecesOnly);
    }
    public void SplitImage()
    {
        // Spritter.Deletepiece();
        // Spritter.SplitImage();
        Spritter.SplitImageProcess();
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

            // Spritter設定の表示・編集
            if (generator.Spritter != null) 
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Spritter Settings", EditorStyles.boldLabel);
                
                int newCols = EditorGUILayout.IntField("Cols", generator.Spritter.cols);
                if (newCols != generator.Spritter.cols)
                {
                    Undo.RecordObject(generator.Spritter, "Change Cols");
                    generator.Spritter.cols = newCols;
                    EditorUtility.SetDirty(generator.Spritter);
                }
                int newRows = EditorGUILayout.IntField("Rows", generator.Spritter.rows);
                if (newRows != generator.Spritter.rows)
                {
                    Undo.RecordObject(generator.Spritter, "Change Rows");
                    generator.Spritter.rows = newRows;
                    EditorUtility.SetDirty(generator.Spritter);
                }
                // _pieceNumを編集可能に
                int newPieceNum = EditorGUILayout.IntField("Piece Num", generator.Spritter._pieceNum);
                if (newPieceNum != generator.Spritter._pieceNum)
                {
                    Undo.RecordObject(generator.Spritter, "Change Piece Num");
                    generator.Spritter._pieceNum = newPieceNum;
                    EditorUtility.SetDirty(generator.Spritter);
                }
                bool newIsSkip = EditorGUILayout.Toggle("Is Skip StageReSetProcess", generator.Spritter.isSkip);
                if (newIsSkip != generator.Spritter.isSkip)
                {
                    Undo.RecordObject(generator.Spritter, "Change Is Skip StageReSetProcess");
                    generator.Spritter.isSkip = newIsSkip;
                    EditorUtility.SetDirty(generator.Spritter);
                }  

                string newSeed = EditorGUILayout.TextField("Piece Create Seed", generator.Spritter.PieceCreateSeed);
                if (newSeed != generator.Spritter.PieceCreateSeed)
                {
                    Undo.RecordObject(generator.Spritter, "Change Piece Create Seed");
                    generator.Spritter.PieceCreateSeed = newSeed;
                    EditorUtility.SetDirty(generator.Spritter);
                }              
                
                EditorGUILayout.Space();
            }

            // シーンのルートにあるオブジェクトを取得する
            GameObject[] rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();

            foreach (GameObject obj in rootObjects)
            {
                // Debug.Log("オブジェクト名: " + obj.name);
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
                        EditorUtility.DisplayCancelableProgressBar(title, info + " " + subInfo, progress + subProgress / totalCount);
                    };
                    
                    // 進捗バーを表示・更新
                    if(EditorUtility.DisplayCancelableProgressBar(title, info, progress))
                    {
                        Debug.Log("ステージ生成がユーザーによって中断されました。");
                        break; // ループを抜ける
                    }

                    script.SetUpStage(); 
                    script.OnUpdateProgressBar = null; // イベントハンドラをリセット
                }
                EditorUtility.ClearProgressBar();
            }
            if (GUILayout.Button("ピースの再配置 (選択全体に適用)"))
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
                        EditorUtility.DisplayCancelableProgressBar(title, info + " " + subInfo, progress + subProgress / totalCount);
                    };
                    
                    // 進捗バーを表示・更新
                    if(EditorUtility.DisplayCancelableProgressBar(title, info, progress))
                    {
                        Debug.Log("ピース再配置がユーザーによって中断されました。");
                        break; // ループを抜ける
                    }
    
                    script.SetUpStage(true); 
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

            // if(GUILayout.Button("StageのAddressable化(選択全体に適用)"))
            // {
            //     // 処理をUndo可能にするための記述（推奨）
            //     Undo.RecordObjects(scripts, "Addressable Stage Stages"); 

            //     for (int i = 0; i < totalCount; i++)
            //     {
            //         StageInfo script = scripts[i];
            //         string title = $"Addressable設定中 ({i + 1}/{totalCount})";
            //         string info = $"ステージ: {script.gameObject.name} をStageグループに登録中...";
            //         float progress = (float)i / totalCount;
                    
            //         // 進捗バーを表示・更新
            //         EditorUtility.DisplayProgressBar(title, info, progress);
            //         script.StageAddressable(); 
            //     }
            //     // 処理が完了したら進捗バーを閉じる
            //     EditorUtility.ClearProgressBar();
            // }

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
                    if(EditorUtility.DisplayCancelableProgressBar(title, info, progress))   
                    {
                        Debug.Log("画像分割がユーザーによって中断されました。");
                        break; // ループを抜ける
                    }

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