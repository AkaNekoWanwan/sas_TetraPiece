using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;

public static class VibrationManager
{
    // ---------- 定数宣言 ----------
    // ---------- ゲームオブジェクト参照変数宣言 ----------
    // ---------- プレハブ ----------
    // ---------- プロパティ ----------
    // ---------- クラス変数宣言 ----------
    // ---------- インスタンス変数宣言 ----------
    // ---------- Unity組込関数 ----------
    // ---------- Public関数 ----------
		public static void VibrateShort()
		{
            // if(PlayerPrefs.GetInt("Effect_ON", 1) == 0)
            //     return;
// #if UNITY_EDITOR
                Debug.Log("VibrateShort");
// #endif
#if UNITY_ANDROID && !UNITY_EDITOR
				UniAndroidVibration.Vibrate(5);
#endif
#if !UNITY_EDITOR && UNITY_IOS
				PlaySystemSound(1519);
#endif
		}
        public static void VibrateLong()
        {
            // if(PlayerPrefs.GetInt("Effect_ON", 1) == 0)
            //     return;
#if UNITY_EDITOR
            Debug.Log("VibrateLong");
            // Handheld.Vibrate();
#endif
#if UNITY_ANDROID && !UNITY_EDITOR
			UniAndroidVibration.Vibrate(100);
#endif
#if !UNITY_EDITOR && UNITY_IOS
			PlaySystemSound(1519);
#endif
        }
    // ---------- Private関数 ----------
}
