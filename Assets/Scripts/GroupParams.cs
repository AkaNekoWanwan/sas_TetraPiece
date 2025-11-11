using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GroupParams : MonoBehaviour
{
    public GameObject pairs;
    public Vector3 relatedPosition; // グループの相対位置
    public Quaternion relatedRotation; // グループの相対回転
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {

        //pairsがあればそれに合わせて角度と位置替える
        if (pairs != null)
        {
            // pairsの位置と回転を取得
        

            // グループの相対位置と回転を適用
            transform.localPosition =new Vector3(-relatedPosition.x, relatedPosition.y, relatedPosition.z);
        }
    }
}
