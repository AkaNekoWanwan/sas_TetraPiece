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
    async void Start()
    {
        StartLoading();
    }

    public void StartLoading()
    {
        // ボタンのクリックイベントなどから呼び出す
        StartCoroutine(LoadSceneCoroutine("IOSMainScene"));
    }

    IEnumerator LoadSceneCoroutine(string sceneName)
    {
        // 非同期読み込みを開始
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);

        // 読み込みが完了するまで待機
        while (!asyncLoad.isDone)
        {
            Debug.Log($"読み込み中... {asyncLoad.progress * 100}%");
            yield return null; // 次のフレームまで待機
        }
    }
}
