using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Initialize : MonoBehaviour
{
    void Awake()
    {
        // 初期化処理をここに記述
        // 例えば、ゲームオブジェクトの初期位置や回転を設定するなど
        //fps60にする
        Application.targetFrameRate = 60;
    }
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
