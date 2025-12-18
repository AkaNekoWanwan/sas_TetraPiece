using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System.Threading.Tasks;

public class Loading : MonoBehaviour
{
    [SerializeField, Tooltip("ローディング画像")] private Transform _loadingImage = default;
    [SerializeField, Tooltip("透明度")] private CanvasGroup _canvasGroup = default;
    [SerializeField, Tooltip("フェード時間")] private float _fadeDuration = 0.5f;
    [SerializeField, Tooltip("フェードするかどうか")] private bool _isFade = false;
    private string currentSceneName = "";

    public static Loading Instance;

    private void Awake() 
    {
        DontDestroyOnLoad(this.transform.parent.gameObject);
        currentSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(this.gameObject);
        }
    }

    async void Start()
    {
        _canvasGroup.alpha = 1f;
    }

    public void FadeOut()
    {
        if(_isFade) return;
        _isFade = true;
        _canvasGroup.DOFade(0f, _fadeDuration).SetLink(this.transform.parent.gameObject).OnComplete(()=>
        {
            Destroy(this.transform.parent.gameObject);
        });
    }

    async void FixedUpdate()
    {
        _loadingImage.Rotate(0f, 0f, -45f);   

        // シーン切り替え中は表示し続ける
        // シーンが切り替わったら非表示にする
        bool isLoaded = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != currentSceneName;
        if(isLoaded && this.gameObject.activeSelf)
        {
            FadeOut();
        }
    }
}
