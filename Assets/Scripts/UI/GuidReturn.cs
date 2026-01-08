using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class GuidReturn : MonoBehaviour
{
    [SerializeField, Tooltip("CanvasGroup")] private CanvasGroup _canvasGroup = null;

    static public GuidReturn instance;

    private const int MAX_SHOW_COUNT = 3;
    private int _returnCount = 0;

    private void Awake() {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(this.gameObject);
        }
        else
        {
            Destroy(this.gameObject);
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _canvasGroup.alpha = 0f;
    }

    public void ShowGuidReturn(float delay = 1f)
    {
        if(MAX_SHOW_COUNT <= _returnCount)
            return;
        if(10 < PlayerPrefs.GetInt("totalLevel", 1) )
            return;
        StartCoroutine(FadeIn(delay));
    }

    public void AddReturnCount()
    {
        _returnCount++;
    }

    public void HideGuidReturn(float delay = 0f)
    {
        // FadeInコルーチンの中断
        StopAllCoroutines();
        StartCoroutine(FadeOut(delay));
    }

    private IEnumerator FadeIn(float delay = 3f)
    {
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        float duration = 1f; // フェードインにかける時間（秒）
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            _canvasGroup.alpha = Mathf.Clamp01(elapsed / duration);
            yield return null;
        }

        _canvasGroup.alpha = 1f;
    }

    private IEnumerator FadeOut(float delay = 0f)
    {
        float startAlpha = _canvasGroup.alpha;
        if(startAlpha <= 0f)
        {
            yield break; // すでに透明なら何もしない
        }

        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        float duration = 0.1f; // フェードアウトにかける時間（秒）
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            _canvasGroup.alpha = startAlpha - Mathf.Clamp01(elapsed / duration);
            yield return null;
        }

        _canvasGroup.alpha = 0f;
    }

    private void OnDisable() {
        if(instance == this)
            instance = null;
        StopAllCoroutines();
    }
}