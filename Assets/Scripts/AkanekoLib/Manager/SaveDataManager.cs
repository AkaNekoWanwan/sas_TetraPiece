using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using System;


/// <summary>
/// セーブデータマネージャー。
/// SaveAllメソッドを読んでセーブする。
/// </summary>
public static class SaveDataManager
{
    #region Fields and Properties
    private static Dictionary<string, object> _cache = new Dictionary<string, object>();
    private static bool _needsSave = false;

    //　ゲームごとに追記するデータ--------------------
    // 例。セーブしたい要素を追加するときは以下をコピペして「Money」の部分を書き換える。型もint、float、stringに対応してる。
    // public static int Money
    // {
    //     get => GetValue("Money", 0);
    //     set => SetValue("Money", value);
    // }
    public static int Level
    {
        get => GetValue("Level", 0);
        set => SetValue("Level", value);
    }


    //　ゲームごとに追記するデータ ここまで------------

    #endregion

    #region Custom public Methods

    // セーブ
    public static void SaveAll()
    {
        if (!_needsSave) return;

        foreach (var kvp in _cache)
        {
            if (kvp.Value is int intValue)
                PlayerPrefs.SetInt(kvp.Key, intValue);
            else if (kvp.Value is float floatValue)
                PlayerPrefs.SetFloat(kvp.Key, floatValue);
            else if (kvp.Value is string stringValue)
                PlayerPrefs.SetString(kvp.Key, stringValue);
        }

        PlayerPrefs.Save();
        _needsSave = false;
    }

    #endregion
    #region Custom private Methods

    private static T GetValue<T>(string key, T defaultValue)
    {
        if (_cache.TryGetValue(key, out object cachedValue))
            return (T)cachedValue;

        T value = defaultValue;
        if (typeof(T) == typeof(int))
            value = (T)(object)PlayerPrefs.GetInt(key, (int)(object)defaultValue);
        else if (typeof(T) == typeof(float))
            value = (T)(object)PlayerPrefs.GetFloat(key, (float)(object)defaultValue);
        else if (typeof(T) == typeof(string))
            value = (T)(object)PlayerPrefs.GetString(key, (string)(object)defaultValue);
        else
            Debug.LogError($"Unsupported type: {typeof(T)}");

        _cache[key] = value;
        return value;
    }

    private static void SetValue<T>(string key, T value)
    {
        _cache[key] = value;
        _needsSave = true;
    }

    #endregion
}
