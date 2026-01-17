using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;

/// <summary>
/// シーン遷移時のフェードイン・アウトを制御するためのクラス .
/// </summary>
public class FadeManager : MonoBehaviour
{
	[SerializeField, Tooltip("ローディング画像")] private Transform _loadingImage = default;
    [SerializeField, Tooltip("透明度")] private CanvasGroup _canvasGroup = default;
    [SerializeField, Tooltip("フェード時間")] private float _fadeDuration = 0.5f;
	[SerializeField, Tooltip("起動時用Loadingテキスト")] private Text _loadingText = default;
	

	#region Singleton

	private static FadeManager instance;

	public static FadeManager Instance {
		get {
			if (instance == null) {
				instance = (FadeManager)FindAnyObjectByType (typeof(FadeManager));

				if (instance == null) {
					Debug.LogError (typeof(FadeManager) + "is nothing");
				}
			}

			return instance;
		}
	}

	#endregion Singleton

	/// <summary>
	/// デバッグモード .
	/// </summary>
	public bool DebugMode = true;
	/// <summary>フェード中の透明度</summary>
	private float fadeAlpha = 0;
	/// <summary>フェード中かどうか</summary>
	private bool isFading = false;
	/// <summary>フェード色</summary>
	public Color fadeColor = Color.black;
	private float _rotateDelay = 0.5f;
	private float _rotateTimer = 0.0f;


	public void Awake ()
	{
		if (this != Instance) {
			Destroy (this.gameObject);
			return;
		}

		_loadingText.text = "Loading...";
		DontDestroyOnLoad (this.gameObject);
	}

	private void Update() {
		if (this.isFading && _canvasGroup != null)
		{
			UpdateLoadingImage();
		}
	}

	private void UpdateLoadingImage()
	{
		if(_loadingImage != null)
		{
			_rotateTimer += Time.deltaTime;
			if (_rotateTimer < _rotateDelay)
				return;
			_rotateTimer = 0f;
			// _loadingImage.Rotate(0f, 0f, -45f);
			if(_loadingText != null)
			{
				if(_loadingText.text == "Loading...")
					_loadingText.text = "Loading";
				else
					_loadingText.text += ".";
			}
			
		}
	}

	public void OnGUI ()
	{
		// テスト
		// _loadingImage.gameObject.SetActive(true);
		// _canvasGroup.alpha = 1f;
		// _loadingImage.Rotate(0f, 0f, -45f);
		// Fade .
		if (this.isFading) {
			// 新しい方法
			if(_loadingImage != null && _canvasGroup != null)
			{
				_canvasGroup.gameObject.SetActive(true);
				_canvasGroup.alpha = this.fadeAlpha;
			}
			// 従来の方法
			else
			{
				//色と透明度を更新して白テクスチャを描画 .
				this.fadeColor.a = this.fadeAlpha;
				GUI.color = this.fadeColor;
				GUI.DrawTexture (new Rect (0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
			}
		}
		else if(_loadingImage != null && _canvasGroup != null)
		{
			_canvasGroup.gameObject.SetActive(false);
			_canvasGroup.alpha = 0f;
		}

		if (this.DebugMode) {
			if (!this.isFading) {
				//Scene一覧を作成 .
				//(UnityEditor名前空間を使わないと自動取得できなかったので決めうちで作成) .
				List<string> scenes = new List<string> ();
				scenes.Add ("SampleScene");
				//scenes.Add ("SomeScene1");
				//scenes.Add ("SomeScene2");


				//Sceneが一つもない .
				if (scenes.Count == 0) {
					GUI.Box (new Rect (10, 10, 200, 50), "Fade Manager(Debug Mode)");
					GUI.Label (new Rect (20, 35, 180, 20), "Scene not found.");
					return;
				}


				GUI.Box (new Rect (10, 10, 300, 50 + scenes.Count * 25), "Fade Manager(Debug Mode)");
				GUI.Label (new Rect (20, 30, 280, 20), "Current Scene : " + SceneManager.GetActiveScene ().name);

				int i = 0;
				foreach (string sceneName in scenes) {
					if (GUI.Button (new Rect (20, 55 + i * 25, 100, 20), "Load Level")) {
						LoadScene (sceneName, 1.0f);
					}
					GUI.Label (new Rect (125, 55 + i * 25, 1000, 20), sceneName);
					i++;
				}
			}
		}
	}

	/// <summary>
	/// 画面遷移 .
	/// </summary>
	/// <param name='scene'>シーン名</param>
	/// <param name='interval'>暗転にかかる時間(秒)</param>
	public void LoadScene (string scene, float interval)
	{
		StartCoroutine (TransSceneCoroutine (scene, interval, interval));
	}

	/// <summary>
	/// シーン遷移用コルーチン .
	/// </summary>
	/// <param name='scene'>シーン名</param>
	/// <param name='interval'>暗転にかかる時間(秒)</param>
	public void TransScene (string scene, float interval, bool isShowLoadingText = false)
	{
		SetisShowLoadingText(isShowLoadingText);
		StartCoroutine (TransSceneCoroutine (scene, interval, interval));
	}
		
	public void TransScene (string scene, float FadeInInterval, float FadeOutInterval, bool isShowLoadingText = false)
	{
		SetisShowLoadingText(isShowLoadingText);
		StartCoroutine (TransSceneCoroutine (scene, FadeInInterval, FadeOutInterval));
	}

	private void SetisShowLoadingText(bool isShow)
	{
		isShow = true; // 常に_loadingTextを表示するように変更
		if(_loadingText != null)
		{
			_loadingText.gameObject.SetActive(isShow);
			_loadingImage.gameObject.SetActive(!isShow);
		}
	}

	public IEnumerator TransSceneCoroutine (string scene, float FadeInInterval, float FadeOutInterval)
	{
		Debug.Log($"TransScene:フェードイン開始");
		if(FadeInInterval <= 0f)
		{
			Debug.Log("TransScene:即時フェードイン");
			this.isFading = true;
			this.fadeAlpha = 1f;
		}
		else
		{
			SetisShowLoadingText(false);
			//だんだん暗く .
			yield return StartCoroutine(FadeInCoroutine( ()=>{} , FadeInInterval, false));
		}

		//シーン切替（Async版でフレーム分割）
		// バックグラウンドロードの優先度を下げてフレームレートを優先
		var originalPriority = Application.backgroundLoadingPriority;
		Application.backgroundLoadingPriority = ThreadPriority.Low;
		
		AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(scene);
		// ロード優先度を下げてフレーム更新を優先
		asyncLoad.priority = (int)ThreadPriority.Low;
		UpdateLoadingImage();

		while (!asyncLoad.isDone)
        {
            Debug.Log($"TransScene:読み込み中... {asyncLoad.progress * 100}%");
			UpdateLoadingImage();
            yield return null; // 次のフレームまで待機（ここでローディング画像の回転が継続）
        }
		
		// 優先度を元に戻す
		Application.backgroundLoadingPriority = originalPriority;
		
		Debug.Log($"TransScene:フェードアウト");
		
		if(FadeOutInterval == 0f)
		{
			Debug.Log("TransScene:即時フェードアウト");
			this.isFading = false;
			this.fadeAlpha = 0f;
			yield break;
		}
		// フェードアウトしない(外部から制御する)場合
		if(FadeOutInterval < 0f)
		{
			yield break;
		}
		//だんだん明るく .
		yield return StartCoroutine(FadeOutCoroutine (FadeOutInterval));

		this.isFading = false;
	}

	// フェードイン
	public void FadeIn(UnityAction onFade, float interval, bool isCompleteOff = false)
	{
		SetisShowLoadingText(false);
		StartCoroutine (FadeInCoroutine (onFade, interval, isCompleteOff));
	}
	private IEnumerator FadeInCoroutine (UnityAction onFade, float interval, bool isCompleteOff)
	{
		if(interval <= 0f)
		{
			Debug.Log("即時フェードイン");
			this.isFading = true;
			this.fadeAlpha = 1f;
			if(isCompleteOff)
				this.fadeAlpha = 0f;
			onFade?.Invoke();
			yield break;
		}
		//だんだん暗く .
		this.isFading = true;
		float time = 0;
		while (time <= interval) {
			this.fadeAlpha = Mathf.Lerp (0f, 1f, time / interval);
			time += Time.deltaTime;
			yield return 0;
		}

		if(isCompleteOff)
			this.fadeAlpha = 0f;

		onFade?.Invoke();
	}
	// フェードアウト
	public void FadeOut(float interval)
	{
		StartCoroutine (FadeOutCoroutine (interval));
	}
	private IEnumerator FadeOutCoroutine (float interval)
	{
		if(interval <= 0)
		{
			this.isFading = false;
			this.fadeAlpha = 0f;
			yield break;
		}
		//だんだん明るく .
		float time = 0;
		this.fadeAlpha = 1f;
		while (time <= interval) {
			this.fadeAlpha = Mathf.Lerp (1f, 0f, time / interval);
			time += Time.deltaTime;
			yield return 0;
		}
		this.fadeAlpha = 0f;

		this.isFading = false;
	}
}
