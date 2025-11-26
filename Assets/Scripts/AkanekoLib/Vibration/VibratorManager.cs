using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System.Runtime.InteropServices;

public static class VibratorManager
{
#if UNITY_ANDROID
    private static AndroidJavaClass unityPlayerClass;
    private static AndroidJavaObject unityActivity;
    private static AndroidJavaObject vibrator;
    private static int androidSdkVersion = -1;
#endif

    public static void Vibrate(long milliseconds)
    {
        if(PlayerPrefs.GetInt("IsVib", 1) == 0)
            return;
        // 振動の強さをデフォルト値で指定
        Vibrate(milliseconds, -1);
    }

    public static void Vibrate(long milliseconds, int amplitude)
    {
        if(PlayerPrefs.GetInt("IsVib", 1) == 0)
            return;
#if UNITY_EDITOR
        Debug.Log($"Vibrate {milliseconds}, {amplitude}");

#elif UNITY_ANDROID
        InitializeVibrator();

        if (!vibrator.Call<bool>("hasVibrator"))
        {
            Debug.Log("Vibrator not found.");
            return;
        }

        // Android 8.0 (API 26) 以上かどうかの判定
        if (androidSdkVersion >= 26)
        {
            // VibrationEffect.createOneShot を使用
            AndroidJavaClass vibrationEffectClass = new AndroidJavaClass("android.os.VibrationEffect");
            AndroidJavaObject vibrationEffect = vibrationEffectClass.CallStatic<AndroidJavaObject>("createOneShot", milliseconds, amplitude == -1 ? vibrationEffectClass.GetStatic<int>("DEFAULT_AMPLITUDE") : amplitude);
            vibrator.Call("vibrate", vibrationEffect);
        }
        else
        {
            // Android 8.0 未満では従来のメソッドを使用
            vibrator.Call("vibrate", milliseconds);
        }
#elif !UNITY_EDITOR && UNITY_IOS
        VibrationManager.Vibrate(VibrationType.Short);
#endif
    }

    public static void Vibrate(long[] pattern, int repeat)
    {
        if(PlayerPrefs.GetInt("IsVib", 1) == 0)
            return;
        // 振幅を指定しないパターン振動
        Vibrate(pattern, null, repeat);
    }

    public static void Vibrate(long[] pattern, int[] amplitudes, int repeat)
    {
        if(PlayerPrefs.GetInt("IsVib", 1) == 0)
            return;
#if UNITY_EDITOR
        Debug.Log($"Vibrate {pattern}, {amplitudes}, {repeat}");

#elif UNITY_ANDROID
        InitializeVibrator();

        if (!vibrator.Call<bool>("hasVibrator"))
        {
            Debug.Log("Vibrator not found.");
            return;
        }
        
        // Android 8.0 (API 26) 以上かどうかの判定
        if (androidSdkVersion >= 26 && amplitudes != null && amplitudes.Length > 0)
        {
            // VibrationEffect.createWaveform (タイミングと振幅)
            AndroidJavaClass vibrationEffectClass = new AndroidJavaClass("android.os.VibrationEffect");
            AndroidJavaObject vibrationEffect = vibrationEffectClass.CallStatic<AndroidJavaObject>("createWaveform", pattern, amplitudes, repeat);
            vibrator.Call("vibrate", vibrationEffect);
        }
        else
        {
            // Android 8.0 未満、または振幅が指定されていない場合
            AndroidJavaClass vibrationEffectClass = new AndroidJavaClass("android.os.VibrationEffect");
            AndroidJavaObject vibrationEffect = vibrationEffectClass.CallStatic<AndroidJavaObject>("createWaveform", pattern, repeat);
            vibrator.Call("vibrate", vibrationEffect);
        }
#elif !UNITY_EDITOR && UNITY_IOS
        VibrationManager.Vibrate(VibrationType.Short);
#endif
    }

#if UNITY_ANDROID
    private static void InitializeVibrator()
    {
        if (androidSdkVersion == -1)
        {
            androidSdkVersion = new AndroidJavaClass("android.os.Build$VERSION").GetStatic<int>("SDK_INT");
        }
        if (unityPlayerClass == null)
        {
            unityPlayerClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        }
        if (unityActivity == null)
        {
            unityActivity = unityPlayerClass.GetStatic<AndroidJavaObject>("currentActivity");
        }
        if (vibrator == null)
        {
            AndroidJavaObject context = unityActivity.Call<AndroidJavaObject>("getApplicationContext");
            vibrator = context.Call<AndroidJavaObject>("getSystemService", "vibrator");
        }
    }
#endif
}