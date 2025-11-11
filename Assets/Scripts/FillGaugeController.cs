using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;  

public class FillGaugeController : MonoBehaviour
{
    public Image fillGauge;
    public float nowValue;
    public PuzzleChecker pc;
    
    [Header("ゲージ調整")]
    public float smoothSpeed = 5f; // ゲージの滑らかさ
    public bool useSmoothing = true; // スムージングを使用するか
    
    [Header("進捗計算方式")]
    public ProgressCalculationMode calculationMode = ProgressCalculationMode.PieceProximity;
    
    [Header("感度設定")]
    [Range(0.5f, 5f)]
    public float proximityRange = 2f; // この範囲内のピースを「近い」とみなす
    [Range(0.1f, 2f)]
    public float progressCurve = 0.7f; // 進捗カーブ
    
    [Header("デバッグ")]
    public bool showDebugLog = false;
    
    private float targetFillAmount = 0f;
    
    public enum ProgressCalculationMode
    {
        PieceProximity,    // ピースの近接度ベース
        DistanceAverage,   // 平均距離ベース
        CompletionRatio    // 完成率ベース
    }
    
    void Start()
    {
        // fillGauge.fillAmount = 0f;
        // nowValue = 0f;
    }

    // void FixedUpdate()
    // {
    //     if (pc == null || !pc.isStart) return;
    //     pc.fg = this; // PuzzleCheckerから参照を更新
    //     float progress = 0f;
        
    //     switch (calculationMode)
    //     {
    //         case ProgressCalculationMode.PieceProximity:
    //             progress = CalculateProximityProgress();
    //             break;
    //         case ProgressCalculationMode.DistanceAverage:
    //             progress = CalculateDistanceProgress();
    //             break;
    //         case ProgressCalculationMode.CompletionRatio:
    //             progress = CalculateCompletionProgress();
    //             break;
    //     }
        
    //     // 進捗カーブを適用
    //     progress = Mathf.Pow(progress, progressCurve);
        
    //     targetFillAmount = progress;
    //     nowValue = progress;
        
    //     if (useSmoothing)
    //     {
    //         fillGauge.fillAmount = Mathf.Lerp(fillGauge.fillAmount, targetFillAmount, Time.fixedDeltaTime * smoothSpeed);
    //     }
    //     else
    //     {
    //         fillGauge.fillAmount = targetFillAmount;
    //     }
        
    //     // デバッグ情報
    //     if (showDebugLog && Time.fixedTime % 1f < Time.fixedDeltaTime)
    //     {
    //         Debug.Log($"🎯 進捗: {progress:F3} | モード: {calculationMode} | ゲージ: {fillGauge.fillAmount:F3}");
    //     }
    // }
    
    // // 方式1: ピースの近接度ベース計算
    // float CalculateProximityProgress()
    // {
    //     if (pc.relativeStates.Count() == 0) return 0f;
        
    //     int closeRelationships = 0;
    //     int totalRelationships = 0;
        
    //     foreach (var pair in pc.relativeStates)
    //     {
    //         GameObject a = pair.Key.Item1;
    //         GameObject b = pair.Key.Item2;
    //         if (a == null || b == null) continue;
            
    //         var expected = pair.Value;
    //         Vector3 currentOffset = b.transform.position - a.transform.position;
    //         float currentAngle = Mathf.DeltaAngle(a.transform.rotation.eulerAngles.z, b.transform.rotation.eulerAngles.z);
            
    //         float positionError = Vector3.Distance(currentOffset, expected.offset);
    //         float rotationError = Mathf.Abs(Mathf.DeltaAngle(currentAngle, expected.angle));
            
    //         totalRelationships++;
            
    //         // proximityRange以内なら「近い」とカウント
    //         if (positionError <= proximityRange && rotationError <= proximityRange * 10f)
    //         {
    //             closeRelationships++;
    //         }
    //     }
        
    //     if (totalRelationships == 0) return 0f;
        
    //     float ratio = (float)closeRelationships / totalRelationships;
        
    //     if (showDebugLog && Time.fixedTime % 1f < Time.fixedDeltaTime)
    //     {
    //         Debug.Log($"🔍 近接計算: {closeRelationships}/{totalRelationships} = {ratio:F3}");
    //     }
        
    //     return ratio;
    // }
    
    // // 方式2: 平均距離ベース計算
    // float CalculateDistanceProgress()
    // {
    //     if (pc.relativeStates.Count == 0) return 0f;
        
    //     float totalPositionScore = 0f;
    //     float totalRotationScore = 0f;
    //     int count = 0;
        
    //     foreach (var pair in pc.relativeStates)
    //     {
    //         GameObject a = pair.Key.Item1;
    //         GameObject b = pair.Key.Item2;
    //         if (a == null || b == null) continue;
            
    //         var expected = pair.Value;
    //         Vector3 currentOffset = b.transform.position - a.transform.position;
    //         float currentAngle = Mathf.DeltaAngle(a.transform.rotation.eulerAngles.z, b.transform.rotation.eulerAngles.z);
            
    //         float positionError = Vector3.Distance(currentOffset, expected.offset);
    //         float rotationError = Mathf.Abs(Mathf.DeltaAngle(currentAngle, expected.angle));
            
    //         // 誤差を0〜1のスコアに変換（小さいほど高スコア）
    //         float positionScore = Mathf.Clamp01(1f - (positionError / proximityRange));
    //         float rotationScore = Mathf.Clamp01(1f - (rotationError / (proximityRange * 20f)));
            
    //         totalPositionScore += positionScore;
    //         totalRotationScore += rotationScore;
    //         count++;
    //     }
        
    //     if (count == 0) return 0f;
        
    //     float averagePositionScore = totalPositionScore / count;
    //     float averageRotationScore = totalRotationScore / count;
    //     float combinedScore = (averagePositionScore + averageRotationScore) * 0.5f;
        
    //     if (showDebugLog && Time.fixedTime % 1f < Time.fixedDeltaTime)
    //     {
    //         Debug.Log($"📊 距離計算: 位置={averagePositionScore:F3}, 回転={averageRotationScore:F3}, 合計={combinedScore:F3}");
    //     }
        
    //     return combinedScore;
    // }
    
    // // 方式3: 完成率ベース計算
    // float CalculateCompletionProgress()
    // {
    //     if (pc.relativeStates.Count == 0) return 0f;
        
    //     int perfectRelationships = 0;
    //     int goodRelationships = 0;
    //     int totalRelationships = 0;
        
    //     foreach (var pair in pc.relativeStates)
    //     {
    //         GameObject a = pair.Key.Item1;
    //         GameObject b = pair.Key.Item2;
    //         if (a == null || b == null) continue;
            
    //         var expected = pair.Value;
    //         Vector3 currentOffset = b.transform.position - a.transform.position;
    //         float currentAngle = Mathf.DeltaAngle(a.transform.rotation.eulerAngles.z, b.transform.rotation.eulerAngles.z);
            
    //         float positionError = Vector3.Distance(currentOffset, expected.offset);
    //         float rotationError = Mathf.Abs(Mathf.DeltaAngle(currentAngle, expected.angle));
            
    //         totalRelationships++;
            
    //         // 完璧に近い（クリア判定の範囲内）
    //         if (positionError <= pc.positionThreshold && rotationError <= pc.rotationThreshold)
    //         {
    //             perfectRelationships++;
    //         }
    //         // まあまあ近い
    //         else if (positionError <= proximityRange && rotationError <= proximityRange * 10f)
    //         {
    //             goodRelationships++;
    //         }
    //     }
        
    //     if (totalRelationships == 0) return 0f;
        
    //     // 完璧=1.0、まあまあ=0.5、遠い=0.0でスコア計算
    //     float score = (perfectRelationships * 1.0f + goodRelationships * 0.5f) / totalRelationships;
        
    //     if (showDebugLog && Time.fixedTime % 1f < Time.fixedDeltaTime)
    //     {
    //         Debug.Log($"✅ 完成率: 完璧={perfectRelationships}, 良好={goodRelationships}, 全体={totalRelationships}, スコア={score:F3}");
    //     }
        
    //     return score;
    // }
    
    // // パブリックメソッド：外部からゲージをリセット
    // public void ResetGauge()
    // {
    //     fillGauge.fillAmount = 0f;
    //     nowValue = 0f;
    //     targetFillAmount = 0f;
    // }
    
    // // パブリックメソッド：ゲージを満タンにする（クリア時用）
    // public void SetFullGauge()
    // {
    //     fillGauge.fillAmount = 1f;
    //     nowValue = 1f;
    //     targetFillAmount = 1f;
    // }
    
    // // パブリックメソッド：計算方式を変更
    // public void SetCalculationMode(ProgressCalculationMode mode)
    // {
    //     calculationMode = mode;
    //     Debug.Log($"🔄 計算方式変更: {mode}");
    // }
    
    // [ContextMenu("デバッグ情報表示")]
    // public void ShowDebugInfo()
    // {
    //     Debug.Log($"=== ゲージ設定情報 ===");
    //     Debug.Log($"計算方式: {calculationMode}");
    //     Debug.Log($"近接範囲: {proximityRange}");
    //     Debug.Log($"進捗カーブ: {progressCurve}");
    //     Debug.Log($"現在の進捗: {nowValue:F3}");
    //     Debug.Log($"ゲージ表示値: {fillGauge.fillAmount:F3}");
    // }
}