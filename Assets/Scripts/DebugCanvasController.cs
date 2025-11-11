using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System;

public class DebugCanvasController : MonoBehaviour
{
    // private static DebugCanvasController instance;

    public int nowCode;
    public int preCode;
    public GameObject DebugCanvas;
    public Vector3 iniScaCanvas;
    public GameObject LevelChangers;
    // public DebugTestText[] debugTestTexts;

    // 期待されるシーケンス
    private List<int> correctSequence = new List<int> { 9, 7, 1, 1, 7, 9, 1 };

    // ユーザーの入力を記録するシーケンス
    public List<int> inputSequence = new List<int>();

    // void Awake()
    // {
    //     // シングルトンパターンを適用
    //     if (instance == null)
    //     {
    //         instance = this;
    //         DontDestroyOnLoad(gameObject);
    //     }
    //     else
    //     {
    //         Destroy(gameObject);
    //         return;
    //     }

    //     // シーンをまたいで引き継ぐために入力シーケンスをロード
    //     LoadInputSequence();
    // }

    void Start()
    {
        iniScaCanvas = Vector3.one;

        // PlayerPrefsに記録されている情報から、DebugCanvasを開く状態にする
        if (PlayerPrefs.GetInt("DebugCanvasOpen") == 1)
        {
            DebugCanvas.transform.localScale = iniScaCanvas;
            
   
            DebugCanvas.SetActive(true);
        }
        else
        {
            DebugCanvas.transform.localScale = Vector3.zero;
               DebugCanvas.SetActive(false);
        }

        // if (PlayerPrefs.GetInt("LevelChanger") == 1)
        // {
        //     LevelChangers.SetActive(true);
        // }
        // else
        // {
        //     LevelChangers.SetActive(false);
        // }
    }

    public void PushDebugButton(int i)
    {
        preCode = nowCode;
        nowCode = i;
        Debug.Log("PushDebugButton: " + i);

        // シーケンスが一致しているか確認
        if (correctSequence[inputSequence.Count] == i)
        {
            inputSequence.Add(i);

            // シーケンスが完全に一致した場合
            if (inputSequence.Count == correctSequence.Count)
            {
                Debug.Log("シーケンスが完全に一致しました");
                OpenCanvas();
                inputSequence.Clear(); // シーケンスをクリア
            }
        }
        else
        {
            // 一致しない場合、シーケンスをリセット
            inputSequence.Clear();

            // ここで最初のボタンが正しければ再入力を開始
            if (correctSequence[0] == i)
            {
                inputSequence.Add(i);
            }
        }

        // 入力シーケンスを保存
        SaveInputSequence();
    }

    public void OpenCanvas()
    {
       

        DebugCanvas.SetActive(true);
        DebugCanvas.transform.DOScale(iniScaCanvas, 0.5f);

        // Canvasが開いている状態を保存
        PlayerPrefs.SetInt("DebugCanvasOpen", 1);
        PlayerPrefs.Save();
    }

    public void CloseCanvas()
    {
      

        DebugCanvas.transform.DOScale(Vector3.zero, 0.5f).OnComplete(() =>
        {
            DebugCanvas.SetActive(false);

            // Canvasが閉じている状態を保存
            PlayerPrefs.SetInt("DebugCanvasOpen", 0);
            PlayerPrefs.Save();
        });
    }

    void OnApplicationQuit()
    {
        // アプリケーションが終了する直前に呼び出されます
        PlayerPrefs.SetInt("DebugCanvasOpen", 0);
        PlayerPrefs.Save();
    }

    void OnApplicationPause(bool isPaused)
    {
        if (isPaused)
        {
            // アプリがバックグラウンドに移行する際にフラグを設定
            PlayerPrefs.SetInt("DebugCanvasOpen", 0);
            PlayerPrefs.Save();
        }
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            // アプリがフォーカスを失った際にフラグを設定
            PlayerPrefs.SetInt("DebugCanvasOpen", 0);
            PlayerPrefs.Save();
        }
    }

    public void ActiavateLevelChanger()
    {
        // if (PlayerPrefs.GetInt("LevelChanger") == 1)
        // {
        //     LevelChangers.SetActive(false);
        //     PlayerPrefs.SetInt("LevelChanger", 0);
        // }
        // else
        // {
        //     LevelChangers.SetActive(true);
        //     PlayerPrefs.SetInt("LevelChanger", 1);
        // }
    }

    public void ResetAppData()
    {
        // PlayerPrefs.SetInt("isSegmented", 0);
        // GameObject.Find("AdsManager").GetComponent<UserSegment>().StartDatas(1);
        // foreach (DebugTestText debugTestText in debugTestTexts)
        // {
        //     debugTestText.StartDisp();
        // }
        // PlayerPrefs.SetInt("isReload", 1);
    }

   

    private void SaveInputSequence()
    {
        PlayerPrefs.SetInt("InputSequenceCount", inputSequence.Count);
        for (int i = 0; i < inputSequence.Count; i++)
        {
            PlayerPrefs.SetInt("InputSequence_" + i, inputSequence[i]);
        }
        PlayerPrefs.Save();
    }

    private void LoadInputSequence()
    {
        inputSequence.Clear();
        int count = PlayerPrefs.GetInt("InputSequenceCount", 0);
        for (int i = 0; i < count; i++)
        {
            inputSequence.Add(PlayerPrefs.GetInt("InputSequence_" + i, 0));
        }
    }

    void Update()
{
    if (Input.GetMouseButtonDown(0)) // タップを検知
    {
        // Debug.Log("Tapped");
        // Vector2 tapPosition = Input.mousePosition; // 画面上のタップ座標
        // int tappedRegion = GetTappedRegion(tapPosition); // タップ位置からエリアを計算
        // Debug.Log("Tapped Region: " + tappedRegion);
        // if (tappedRegion != -1) // 無効なエリアでなければ
        // {
        //     ProcessInput(tappedRegion);
        // }
    }
}

// **タップされたエリアを計算**
private int GetTappedRegion(Vector2 tapPosition)
{
    int cols = 3; // 横3列
    int rows = 3; // 縦3行
    float cellWidth = Screen.width / cols; // 各エリアの幅
    float cellHeight = Screen.height / rows; // 各エリアの高さ

    // 列（X方向）
    int colIndex = (int)(tapPosition.x / cellWidth);
    
    // 行（Y方向、上から数える）
    int rowIndex = rows - 1 - (int)(tapPosition.y / cellHeight);

    // 9分割の番号を求める（左上が1、右下が9）
    int regionNumber = rowIndex * cols + colIndex + 1;

    if (regionNumber >= 1 && regionNumber <= 9)
    {
        return regionNumber;
    }
    return -1; // 無効なエリア
}


// **タップ順を記録し、シーケンスを判定**
private void ProcessInput(int tappedRegion)
{
    Debug.Log("Tapped: " + tappedRegion);

    if (correctSequence[inputSequence.Count] == tappedRegion)
    {
        inputSequence.Add(tappedRegion);

        if (inputSequence.Count == correctSequence.Count)
        {
            Debug.Log("シーケンスが完全に一致しました");
            OpenCanvas();
            inputSequence.Clear();
        }
    }
    else
    {
        inputSequence.Clear();

        if (correctSequence[0] == tappedRegion)
        {
            inputSequence.Add(tappedRegion);
        }
    }
}
}
