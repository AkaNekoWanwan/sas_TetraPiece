using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

public class MainSceneLoader : MonoBehaviour
{
    private void Awake()
    {
        Application.targetFrameRate = 60; // フレームレートを60に設定
    }
    async void Start()
    {
        StartLoading();
        // PlayerPrefs.SetInt("totalLevel", 31); // デバッグ用に総レベル数を504に設定
        // PlayerPrefs.SetInt("Stage", 30); // デバッグ用に総レベル数を504に設定
        // GameDataManager.IsClear = true; // デバッグ用にクリア済みに設定
        // GameDataManager.isPlayHomePieceAnimation = true; // デバッグ用にホームピースアニメーションを有効化
    }

    public void StartLoading()
    {
        FadeManager.Instance.TransScene("MainScene", 0.0f, -1f, true);
    }
}
