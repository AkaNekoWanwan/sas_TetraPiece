using UnityEngine;
using System.Collections.Generic;

public class PieceContactReporter : MonoBehaviour
 {
//     public MovePieces movePieces;
//     private Rigidbody rb;

//     void Start()
//     {
//         if (gameObject.name.Contains("Daiza"))
//         {
//             // 台座のピースはRigidbodyを追加しない
//             return;
//         }
//         rb = GetComponent<Rigidbody>();
//         this.gameObject.AddComponent<DaizaTransparentController>();
//         this.gameObject.GetComponent<DaizaTransparentController>().movePieces = movePieces;
//     }

//     void OnCollisionEnter(Collision collision)
//     {
//         var otherRb = collision.rigidbody;
//         if (otherRb != null && movePieces != null && rb != null)
//         {
//             Debug.Log($"OnCollisionEnter: {rb.name} が {otherRb.name} と接触開始");
            
//             // 従来の接触判定
//             movePieces.ReportTouching(rb, true);
            
//             // 新しい接触オブジェクト追跡
//             movePieces.ReportTouchingObject(rb, otherRb, true);
//         }
//     }

//     void OnCollisionStay(Collision collision)
//     {
//         if (movePieces != null && rb != null)
//         {
//             Debug.Log($"OnCollisionStay: {rb.name} is touching {collision.gameObject.name}");
//             movePieces.ReportTouching(rb, true);
//         }
//     }

//     void OnCollisionExit(Collision collision)
//     {
//         var otherRb = collision.rigidbody;
//         if (otherRb != null && movePieces != null && rb != null)
//         {
//             Debug.Log($"OnCollisionExit: {rb.name} が {otherRb.name} と接触終了");
            
//             // 接触終了を報告
//             movePieces.ReportTouchingObject(rb, otherRb, false);
            
//             // 他に接触している物体がないかチェック
//             bool stillTouching = false;
//             if (movePieces.touchingObjectsMap.ContainsKey(rb))
//             {
//                 stillTouching = movePieces.touchingObjectsMap[rb].Count > 0;
//             }
            
//             movePieces.ReportTouching(rb, stillTouching);
//         }
//     }
}