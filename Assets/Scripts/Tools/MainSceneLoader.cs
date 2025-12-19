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
        FadeManager.Instance.TransScene("IOSMainScene", 0.0f, 0.5f);
    }
}
