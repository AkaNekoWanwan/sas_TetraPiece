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
        // PlayerPrefs.SetInt("totalLevel", 306); // デバッグ用に総レベル数を306に設定
        // PlayerPrefs.SetInt("Stage", 305); // デバッグ用に総レベル数を306に設定
    }

    public void StartLoading()
    {
        FadeManager.Instance.TransScene("IOSMainScene", 0.0f, -1f);
    }
}
