using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;

public class HomePanel : MonoBehaviour
{
    public int cellNumber = 1; // 1スタート
    public List<GameObject> hideableObjs = default;
    public Text NumText = default;
    public RectTransform rectTransform = default;

    public void UpdateView(int cellNum, int currentTotalLevel)
    {
        if(cellNum < currentTotalLevel)
        {
            if(cellNum == currentTotalLevel - 1 && GameDataManager.isPlayHomePieceAnimation)
            {
                PlayAnimation();
                GameDataManager.isPlayHomePieceAnimation = false;
            }
            else
                Open();
        }
    }

    private void PlayAnimation()
    {
        Sequence seq = DOTween.Sequence();
        seq.AppendInterval(0.55f);
        seq.AppendCallback(() =>
        {
            AudioManager.Instance.PlayCardFlipSound();
        });
        seq.AppendInterval(0.25f);
        seq.Append(this.transform.DOScale(new Vector3(0f, 1f, 1f), 0.25f));
        seq.AppendCallback(() =>
        {
            Open();
        });
        seq.Append(this.transform.DOScale(Vector3.one, 0.25f));
        seq.SetLink(this.gameObject);
    }

    private void Open()
    {
        for(int i = 0; i < hideableObjs.Count; i++)
        {
            hideableObjs[i].SetActive(false);
        }
    }
}
