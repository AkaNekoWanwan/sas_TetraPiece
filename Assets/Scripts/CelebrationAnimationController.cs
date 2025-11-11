using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System.Data.Common;
public class CelebrationAnimationController : MonoBehaviour
{
    public Vector3 targetPos;
    public Vector3 lastPos;
    public MeshRenderer meshRenderer;
    public GameObject alienObject;
    public int celebType;
    public SpriteRenderer screanx;
    public GameObject hibi;
    public MeshRenderer roboRenderer;
    public GameObject[] batteries;
    public GameObject face;
    public GameObject fire;
    public Vector3 iniScaFire;
    public Vector3 iniScaText;
    public GameObject iqText;
    public GameObject[] pancakes;
    public GameObject[] dishes;
    public GameObject panPare;
    public GameObject dishPare;
    public GameObject panPareTarget;
    public GameObject[] splashs;
    public Vector3 splash1Scale;
    public Vector3 splash2Scale;
    public Vector3 splash3Scale;
    public Vector3 splash4Scale;
    public Vector3 splash5Scale;
    public GameObject boy;
    public GameObject pieces;
    // Start is called before the first frame update
    void Start()
    {
        if (celebType == 0)
        {
            //ufo
            meshRenderer.material.DOFade(0f, 0f);
            meshRenderer.enabled = false; // Initially hide the mesh renderer

        }
        else if (celebType == 1)
        {
            screanx.DOFade(0f, 0f);
            screanx.enabled = false; // Initially hide the sprite renderer

        }
        else if (celebType == 2)
        {
            hibi.SetActive(false); // Initially hide the hibi object
        }
        else if (celebType == 3)
        {

            foreach (GameObject battery in batteries)
            {
                battery.SetActive(false); // Initially hide all battery objects
            }
            face.SetActive(false); // Initially hide the face object
        }
        else if (celebType == 4)
        {
            fire.SetActive(false); // Initially hide the fire object
            iniScaFire = fire.transform.localScale; // Store the initial scale of the fire object
            fire.transform.localScale = Vector3.zero; // Start with scale zero
        }
        else if (celebType == 5)
        {
            // IQ text
            iniScaText = iqText.transform.localScale;
            iqText.transform.localScale = Vector3.zero; // Start with scale zero
            iqText.SetActive(false); // Initially hide the IQ text
        }
        else if (celebType == 6)
        {
            // pancake

        }
        else if (celebType == 7)
        {
            // splash
          splash1Scale = splashs[0].transform.localScale;
          splash2Scale = splashs[1].transform.localScale;
          splash3Scale = splashs[2].transform.localScale;
          splash4Scale = splashs[3].transform.localScale;
          splash5Scale = splashs[4].transform.localScale;
            foreach (GameObject splash in splashs)
            {
                splash.transform.localScale = Vector3.zero; // Start with scale zero    
            }

        }
        
        
        // StartAnimation();

    }

    // Update is called once per frame
    void Update()
    {

    }
    public void StartAnimation()
    {
        if (celebType == 0)
        {
            // UFO animation
            this.transform.DOLocalMove(targetPos, 1f).SetEase(Ease.InSine).OnComplete(() =>
             {
                 meshRenderer.enabled = true;
                 alienObject.transform.DOScale(Vector3.zero, 0.5f).SetDelay(0.5f);
                 alienObject.transform.DOMove(this.transform.position, 0.5f).SetDelay(0.5f).SetEase(Ease.InOutSine);

                 meshRenderer.material.DOFade(1f, 0.2f).OnComplete(() =>
                 {

                 });
                 this.transform.DOLocalMove(lastPos, 1f).SetDelay(1f).SetEase(Ease.InSine).OnComplete(() =>
                 {
                     meshRenderer.material.DOFade(0f, 0.2f);
                 });
             });
        }
        else if (celebType == 1)
        {
            screanx.enabled = true; // Show the sprite renderer
            screanx.DOFade(1f, 0.5f).SetDelay(1f);
        }
        else if (celebType == 2)
        {
            hibi.SetActive(true); // Show the hibi object
            hibi.transform.localScale = Vector3.zero; // Start with scale zero
            hibi.transform.DOScale(Vector3.one * 0.7f, 0.1f).SetDelay(1.2f);

        }
        else if (celebType == 3)
        {
            for (int i = 0; i < batteries.Length; i++)
            {
                GameObject battery = batteries[i];
                battery.SetActive(true);
                battery.transform.localScale = Vector3.zero;

                float delay = 1.2f + i * 0.25f;
                if (i == batteries.Length - 1)
                {
                    // 最後の1つが終わったら material を変更
                    battery.transform.DOScale(Vector3.one * 0.7f, 0.1f)
                        .SetDelay(delay)
                        .OnComplete(() =>
                        {
                            face.SetActive(true); // 最後のバッテリーが表示された後に顔を表示
                            face.transform.localScale = Vector3.zero; // Start with scale zero
                            face.transform.DOScale(Vector3.one * 0.01f, 0.1f).SetDelay(0.3f);
                        });
                }
                else
                {
                    battery.transform.DOScale(Vector3.one * 0.7f, 0.1f)
                        .SetDelay(delay * 0.7f);
                }
            }

        }
        else if (celebType == 4)
        {
            fire.SetActive(true); // Show the fire object
            fire.transform.DOScale(iniScaFire, 0.5f).SetDelay(0.3f).SetEase(Ease.InSine);
        }
        else if (celebType == 5)
        {
            iqText.SetActive(true); // Show the IQ text
            iqText.transform.DOScale(iniScaText, 0.5f).SetDelay(0.3f).SetEase(Ease.OutBounce);
        }
        else if (celebType == 6)
        {
            foreach (GameObject pancake in pancakes)
            {
                pancake.transform.parent = panPare.transform;
            }
            foreach (GameObject dish in dishes)
            {
                dish.transform.parent = dishPare.transform;
            }
            panPare.transform.DOMove(panPareTarget.transform.position, 0.5f).SetDelay(0.5f).SetEase(Ease.InSine).OnComplete(() =>
            {

            });
        }
        else if (celebType == 7)
        {
            splashs[0].transform.DOScale(splash1Scale, 0.5f).SetDelay(0.7f).SetEase(Ease.OutBounce);
            splashs[1].transform.DOScale(splash2Scale, 0.5f).SetDelay(0.9f).SetEase(Ease.OutBounce);
            splashs[2].transform.DOScale(splash3Scale, 0.5f).SetDelay(0.9f).SetEase(Ease.OutBounce);
            splashs[3].transform.DOScale(splash4Scale, 0.5f).SetDelay(1.2f).SetEase(Ease.OutBounce);
            splashs[4].transform.DOScale(splash5Scale, 0.5f).SetDelay(1.2f).SetEase(Ease.OutBounce);
        }
        else if (celebType == 8)
        {
            
            pieces.transform.DOMove(pieces.transform.position,0.9f).OnComplete(() =>
            {
        pieces.SetActive(false);

            boy.SetActive(true); //
            boy.GetComponent<Animator>().enabled = true;
            });
        
        }
    }

    }
