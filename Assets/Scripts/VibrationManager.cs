using UnityEngine;

#if UNITY_IOS
using System.Runtime.InteropServices;
#endif

public static class VibrationManager
{
    /// <summary>
    /// 振動
    /// </summary>
    public static void Vibrate(VibrationType type)
    {
        if (SystemInfo.supportsVibration)
        {
            switch (type)
            {
                case VibrationType.Short:
                    {
                        PlaySystemSound(1519); // iOSの場合はSoundIdで1519を指定
                        Vibrate(20); // Androidの場合は20ミリ秒（任意のミリ秒に変更可能）
                    }
                    break;
                case VibrationType.Medium:
                    {
                        PlaySystemSound(1520); // iOSの場合はSoundIdで1520を指定
                        Vibrate(50); // Androidの場合は50ミリ秒（任意のミリ秒に変更可能）
                    }
                    break;
                case VibrationType.Long:
                    {
                        PlaySystemSound(1521); // iOSの場合はSoundIdで1521を指定
                        Vibrate(100); // Androidの場合は100ミリ秒（任意のミリ秒に変更可能）
                    }
                    break;
            }
        }
    }

    // iOS設定
#if UNITY_IOS && !UNITY_EDITOR
    [DllImport ("__Internal")]
    static extern void _playSystemSound(int n);
#endif

    private static void PlaySystemSound(int n) //引数にIDを渡す
    {
#if UNITY_IOS && !UNITY_EDITOR
        if (n > 0)
        {
            _playSystemSound(n);
        }
        else
        {
            Handheld.Vibrate();
        }
#endif
    }

    // Android設定
#if UNITY_ANDROID && !UNITY_EDITOR
    public static AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
    public static AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
    public static AndroidJavaObject vibrator = currentActivity.Call<AndroidJavaObject>("getSystemService", "vibrator");
#endif

    private static void Vibrate(long milliseconds)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (milliseconds >= 1000)
        {
            Handheld.Vibrate();
        }
        else
        {
            vibrator.Call("vibrate", milliseconds);
        }
#endif
    }
}