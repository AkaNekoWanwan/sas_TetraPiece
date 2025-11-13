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
        _hardLevelTextBanner.localScale = new Vector3(0f, 1f, 0f);
        _hardLevelText.localScale = Vector3.zero;

        // Sequence sequence = 
    }
}
