using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class HardEfffectManager : MonoBehaviour
{
    public static HardEfffectManager Instance;
    public Transform _hardLevelText = default;
    public Transform _hardLevelTextBanner = default;
    public CanvasGroup _canvasGroup = default;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void PlayHardAnimation(bool isHard)
    {
        if(GameDataManager.IsHome)
            return;
        this.gameObject.SetActive(isHard);
        if (!isHard)
            return;
        _canvasGroup.alpha = 0f;
        _hardLevelTextBanner.localScale = new Vector3(0f, 1f, 1f);
        _hardLevelText.localScale = Vector3.zero;

        Sequence sequence = DOTween.Sequence();

        sequence.AppendInterval(0.5f);
        sequence.Append(_canvasGroup.DOFade(1.0f, 0.2f).SetEase(Ease.Linear).SetLink(this.gameObject));
        sequence.Append(_hardLevelTextBanner.DOScale(Vector3.one, 0.1f).SetEase(Ease.Linear).SetLink(this.gameObject));
        sequence.Append(_hardLevelText.DOScale(Vector3.one, 0.1f).SetEase(Ease.OutBack).SetLink(this.gameObject));
        sequence.AppendInterval(0.45f);
        sequence.Append(_canvasGroup.DOFade(0.0f, 0.2f).SetEase(Ease.Linear).SetLink(this.gameObject));
        // _canvasGroup.DOFade(1.0f, 0.3f);

        // sequence.Append(_canvasGroup.DOAlpha(1.0f, 0.3f).SetEase(Ease.Linear).SetLink(this.gameObject));
    }
}
