using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
public class PhaseController : MonoBehaviour
{
    public int pType;
        public GameObject[] piees;
    public GameObject[] answerPieces;
    public GameObject[] pieces2;
    public GameObject[] answerPieces2;
    public GameObject[] waters;
    public Vector3[] waterScales;
    public GameObject parentObject;
    public GameObject flame;
    public bool isPhased;
    public bool isAnotherPhased;
    public bool isCrashed;
    public GameObject jouro;
    public GameObject flower;
    public Vector3 iniScaleFloaer;
    // Start is called before the first frame update
    void Start()
    {
        if (pType == 1)
        {
            var i = 0;
            foreach (GameObject piece in waters)
            {
                waterScales[i] = piece.transform.localScale; // Store the initial scale of each water piece
                piece.transform.localScale = Vector3.zero; // Start with scale zero
                i++;
            }
            iniScaleFloaer = flower.transform.localScale; // Store the initial scale of the floaer object
            flower.transform.localScale = Vector3.zero; // Start with scale zero
            flower.SetActive(false); // Initially hide the floaer object
        }
        
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if (pType == 0)
        {
            if (isPhased == false)
            {
                float distance = Vector3.Distance(piees[0].transform.position, answerPieces[0].transform.position);
                float distance2 = Vector3.Distance(piees[1].transform.position, answerPieces[1].transform.position);
                if (distance < 0.1f && distance2 < 0.1f)
                {
                    isPhased = true;
                    flame.transform.parent = parentObject.transform;
                    foreach (GameObject piece in piees)
                    {
                        piece.transform.parent = parentObject.transform;
                    }
                    foreach (GameObject answerPiece in answerPieces)
                    {
                        answerPiece.transform.parent = parentObject.transform;
                    }
                    parentObject.transform.DOLocalMoveX(-20f, 2f).SetDelay(0.8f);

                }
            }
            else
            {
                if (isAnotherPhased == true)
                {
                    float distance5 = Vector3.Distance(piees[1].transform.position, pieces2[3].transform.position);
                    Debug.Log("Distance5: " + distance5);
                    if (distance5 < 3.5f)
                    {
                        if (isCrashed == false)
                        {
                            foreach (GameObject piece in pieces2)
                            {
                                //爆散アニメーション
                                piece.gameObject.AddComponent<Rigidbody>().isKinematic = false;
                                piece.gameObject.GetComponent<Rigidbody>().AddExplosionForce(12000f, piece.transform.position, 30f);
                                piece.transform.parent = null;
                            }

                            // foreach (GameObject piece in piees)
                            // {
                            //     piece.gameObject.AddComponent<Rigidbody>().isKinematic = false;
                            //     piece.gameObject.GetComponent<Rigidbody>().AddExplosionForce(4000f, piece.transform.position, 10f);
                            //     piece.transform.parent = null;
                            // }

                            isCrashed = true;
                        }
                    }
                }
            }

            if (isAnotherPhased == false)
            {
                float distance3 = Vector3.Distance(pieces2[0].transform.position, answerPieces2[0].transform.position);
                float distance4 = Vector3.Distance(pieces2[1].transform.position, answerPieces2[1].transform.position);
                if (distance3 < 0.1f && distance4 < 0.1f)
                {
                    isAnotherPhased = true;
                }
            }

        }
        else if (pType == 1)
        {
            if (isPhased == false)
            {
                float distance = Vector3.Distance(piees[0].transform.position, answerPieces[0].transform.position);
                float distance2 = Vector3.Distance(piees[1].transform.position, answerPieces[1].transform.position);
                if (distance < 0.1f && distance2 < 0.1f)
                {
                    isPhased = true;

                    Debug.Log("Phase 1 completed");
                }
            }
            
            if( isAnotherPhased == false)
            {
                float distance3 = Vector3.Distance(pieces2[0].transform.position, answerPieces2[0].transform.position);
                float distance4 = Vector3.Distance(pieces2[1].transform.position, answerPieces2[1].transform.position);
                float distance5 = Vector3.Distance(pieces2[2].transform.position, answerPieces2[2].transform.position);
                float distance6 = Vector3.Distance(pieces2[3].transform.position, answerPieces2[3].transform.position);
                float distance7 = Vector3.Distance(pieces2[4].transform.position, answerPieces2[4].transform.position);
                float distance8 = Vector3.Distance(pieces2[5].transform.position, answerPieces2[5].transform.position);
                float distance9 = Vector3.Distance(pieces2[6].transform.position, answerPieces2[6].transform.position);
                Debug.Log("Distance3: " + distance3);
                if (distance3 < 0.1f && distance4 < 0.1f &&
                    distance5 < 0.1f && distance6 < 0.1f &&
                    distance7 < 0.1f && distance8 < 0.1f &&
                    distance9 < 0.1f && isPhased == true)
                {
                
                    
                    isAnotherPhased = true;
                    var i = 1;
                    foreach (GameObject piece in pieces2)
                    {
                        piece.transform.parent=jouro.transform;
                    }
                    foreach (GameObject piece in waters)
                    {
                        piece.transform.DOScale(waterScales[i - 1], 0.2f).SetEase(Ease.InSine).SetDelay(0.15f * i).OnComplete(() =>
                        {
                            piece.transform.DOScale(0f, 1f).SetEase(Ease.InSine).SetDelay(0.7f);
                        });
                        i++;
                    }
                       jouro.transform.DOLocalMoveX(-120f, 0.7f).SetDelay(1f).OnComplete(() =>
                    {
                        flower.SetActive(true); // Show the floaer object
                        flower.transform.DOScale(iniScaleFloaer, 0.35f).SetDelay(0.1f).SetEase(Ease.InSine);
                    });

                    Debug.Log("Phase 2 completed");

                }
            }
        }

        }
}
