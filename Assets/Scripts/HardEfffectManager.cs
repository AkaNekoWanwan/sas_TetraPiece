using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class HardEfffectManager : MonoBehaviour
{
    public Transform _hardLevelText = default;
    public Transform _hardLevelTextBanner = default;
    public CanvasGroup _canvasGroup = default;

    public void PlayHardAnimation(bool isHard)
    {
        this.gameObject.SetActive(isHard);
        if (!isHard)
            return;
        _canvasGroup.alpha = 0f;
        _hardLevelTextBanner.localScale = new Vector3(0f, 1f, 1f);
        _hardLevelText.localScale = Vector3.zero;

        Sequence sequence = DOTween.Sequence();

        sequence.AppendInterval(0.7f);
        sequence.Append(_canvasGroup.DOFade(1.0f, 0.3f).SetEase(Ease.Linear).SetLink(this.gameObject));
        sequence.Append(_hardLevelTextBanner.DOScale(Vector3.one, 0.2f).SetEase(Ease.Linear).SetLink(this.gameObject));
        sequence.Append(_hardLevelText.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack).SetLink(this.gameObject));
        sequence.AppendInterval(1.3f);
        sequence.Append(_canvasGroup.DOFade(0.0f, 0.3f).SetEase(Ease.Linear).SetLink(this.gameObject));
        // _canvasGroup.DOFade(1.0f, 0.3f);

        // sequence.Append(_canvasGroup.DOAlpha(1.0f, 0.3f).SetEase(Ease.Linear).SetLink(this.gameObject));
    }
}
