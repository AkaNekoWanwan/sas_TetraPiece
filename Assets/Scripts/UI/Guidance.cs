using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System.Collections;

public class Guidance : MonoBehaviour
{
    [SerializeField] private RectTransform guidanceRect;
    [SerializeField] private Vector2 startAnchorPos = new Vector2(0f, -200f);
    [SerializeField] private Vector2 endAnchorPos = new Vector2(0f, -200f);
    [SerializeField] private float animationDuration = 0.5f;
    [SerializeField] private int loopNumber = -1; // 無限ループ
    public static Guidance Instance;
    private Vector3 _initScale = Vector3.one;

    private Sequence guidanceSequence;
    private bool isShowing = false;

    void Awake()
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
    void Start()
    {
        guidanceRect.gameObject.SetActive(false);
        _initScale = guidanceRect.localScale;
    }

    // Update is called once per frame
    void Update()
    {
        if(isShowing && guidanceRect.gameObject.activeSelf && Input.GetMouseButtonDown(0))
        {
            guidanceRect.gameObject.SetActive(false);
            isShowing = false;
            if(guidanceSequence != null)
            {
                guidanceSequence.Kill();
            }
        }
    }

    public void ShowGuidance()
    {
        guidanceRect.gameObject.SetActive(true);
        StartCoroutine(ShowGuidanceCoroutine());
    }

    private IEnumerator ShowGuidanceCoroutine()
    {
        yield return new WaitForSeconds(0.5f);

        if(guidanceSequence != null)
        {
            guidanceSequence.Kill();
        }
        guidanceSequence = DOTween.Sequence();
        guidanceSequence.SetLink(this.gameObject);
        guidanceRect.anchoredPosition = startAnchorPos;

        guidanceSequence.AppendInterval(0.3f);
        guidanceSequence.Append(guidanceRect.DOScale(_initScale * 0.9f, 0.2f).SetEase(Ease.OutCubic));
        guidanceSequence.AppendInterval(0.05f);
        guidanceSequence.Append(guidanceRect.DOAnchorPos(endAnchorPos, animationDuration).SetEase(Ease.OutCubic));
        guidanceSequence.AppendInterval(0.3f);
        guidanceSequence.SetLoops(loopNumber);
        isShowing = true;
        while(isShowing)
        {
            yield return null;
        }
    }
}
