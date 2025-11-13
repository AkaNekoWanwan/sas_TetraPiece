using System;
using UnityEngine;
using UnityEngine.Diagnostics;

/// <summary>
/// 意図的にクラッシュを起こすコード。firebase Chashlytics実装テスト用。
/// </summary>
public class CrashlyticsTester : MonoBehaviour {
    void Start () {
      Utils.ForceCrash( ForcedCrashCategory.AccessViolation );
    }
}