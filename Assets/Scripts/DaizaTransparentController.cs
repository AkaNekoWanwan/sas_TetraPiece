using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class DaizaTransparentController : MonoBehaviour
{
    public GameObject daiza;
    public MovePieces movePieces;
    public Rigidbody selectedRb;
    // Start is called before the first frame update
    void Start()
    {
        this.gameObject.layer = 8;
        daiza = GameObject.Find("Daiza");
        selectedRb =GetComponent<Rigidbody>();
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        // try
        // {
        //     if (this.gameObject.transform.parent.name.Contains("Group"))
        //     {
        //         if (movePieces.selectedRb == this.transform.parent.gameObject.GetComponent<Rigidbody>())
        //         {
        //             this.gameObject.layer = 10;
        //             var child = this.gameObject.transform.parent.GetComponentsInChildren<Transform>();
        //             foreach (var c in child)
        //             {

        //                 c.gameObject.layer = 10;
        //             }

        //         }
        //         else
        //         {

        //             if (this.gameObject.transform.parent.transform.position.y > daiza.transform.position.y + 1.2f)
        //             {
        //                 this.gameObject.layer = 0;
        //                 var child = this.gameObject.transform.parent.GetComponentsInChildren<Transform>();
        //                 foreach (var c in child)
        //                 {

        //                     c.gameObject.layer = 0;
        //                 }
        //             }
        //             else
        //             {
        //                 this.gameObject.layer = 8;
        //                 var child = this.gameObject.transform.parent.GetComponentsInChildren<Transform>();
        //                 foreach (var c in child)
        //                 {

        //                     c.gameObject.layer = 8;
        //                 }
        //             }
        //         }
        //     }
        //     else
        //     {


        //         if (movePieces.selectedRb != selectedRb && movePieces.puzzleChecker.isStart)
        //         {
        //             if (this.gameObject.transform.position.y > daiza.transform.position.y + 1.2f)
        //             {
        //                 this.gameObject.layer = 0;


        //             }
        //             else
        //             {
        //                 this.gameObject.layer = 8;

        //             }
        //         }
        //         else if (movePieces.selectedRb == selectedRb && movePieces.puzzleChecker.isStart)
        //         {
        //             this.gameObject.layer = 10;
        //         }

        //     }

        // }catch (Exception e)
        // {
        //     // Debug.LogError($"Error in DaizaTransparentController: {e.Message}");
        // }
        
    }
}
