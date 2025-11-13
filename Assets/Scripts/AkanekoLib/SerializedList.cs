using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// シリアライズ可能なリスト。主に２次元以上のリストをシリアライザブルにしたい時に使う
/// </summary>
[System.Serializable]
public class SerializeList<T>
{
    [SerializeField]
    public List<T> list = new List<T>();
}
