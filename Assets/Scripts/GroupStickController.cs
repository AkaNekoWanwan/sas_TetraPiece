using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GroupStickController : MonoBehaviour
{
    [Header("安定化設定")]
    public Quaternion targetRotation; // 目標回転角度
    public float stabilizeForce = 50f; // 復帰力の強さ
    public float dampingForce = 10f;   // 減衰力（振動を抑える）
    public float maxTorque = 400f;     // 最大トルク制限

    [Header("安定化再始動の判定設定")]
    public float reactivateAngleThreshold = 9f; // 再始動角度
    private bool isInStableZone = false;        // 安定ゾーン判定

    [Header("判定設定")]
    public float angleThreshold = 6f;  // 安定とみなす角度差
    public bool isStabilizing = true;

    private Rigidbody rb;
    private bool isGrounded = false;
    public int buffer;

    void Start()
    {
        // rb = GetComponent<Rigidbody>();
        // if (rb == null)
        // {
        //     Debug.LogError("Rigidbody not found!");
        //     enabled = false;
        //     return;
        // }

        // SetTargetRotation(Quaternion.Euler(0f, 180f, 180f));

        // // 慣性対策（任意）
        // rb.inertiaTensor = Vector3.one;
        // rb.inertiaTensorRotation = Quaternion.identity;
        // rb.centerOfMass = Vector3.zero;
    }

   void FixedUpdate()
{
    //     if (buffer < 100)
    //     {
    //         buffer++;
    //         return; // 初期化中は処理をスキップ
    // }
    // if (!isStabilizing || rb == null || !isGrounded) return;

    // ApplyStabilizingTorque();
    // ApplyXStayForce();

    // rb.angularVelocity *= 0.98f;
}


//     void ApplyStabilizingTorque()
//     {
//         Vector3 currentEuler = transform.rotation.eulerAngles;
//         Vector3 targetEuler = targetRotation.eulerAngles;

//         float angleX = Mathf.DeltaAngle(currentEuler.x, targetEuler.x);
//         float angleY = Mathf.DeltaAngle(currentEuler.y, targetEuler.y);
//         float angleZ = Mathf.DeltaAngle(currentEuler.z, targetEuler.z);

//         float angleMagnitude = new Vector3(angleX, angleY, angleZ).magnitude;

//         // ヒステリシス判定
//         if (isInStableZone)
//         {
//             if (angleMagnitude > reactivateAngleThreshold)
//             {
//                 isInStableZone = false;
//             }
//             else
//             {
//                 return;
//             }
//         }
//         else
//         {
//             if (angleMagnitude < angleThreshold)
//             {
//                 isInStableZone = true;
//                 return;
//             }
//         }

//         // 各軸のトルク計算
//         float torqueX = (angleX * stabilizeForce) - (rb.angularVelocity.x * dampingForce);
//         float torqueY = (angleY * stabilizeForce) - (rb.angularVelocity.y * dampingForce);
//         float torqueZ = (angleZ * stabilizeForce) - (rb.angularVelocity.z * dampingForce);

//         torqueX = Mathf.Clamp(torqueX, -maxTorque, maxTorque);
//         torqueY = Mathf.Clamp(torqueY, -maxTorque, maxTorque);
//         torqueZ = Mathf.Clamp(torqueZ, -maxTorque, maxTorque);

//         Vector3 rawTorque = new Vector3(torqueX, torqueY, torqueZ);

//         // 🔽 スムージング（急激なトルクを緩和）
//         Vector3 smoothedTorque = Vector3.Lerp(Vector3.zero, rawTorque, 0.3f); // ← 0.3fで緩やかに
//         rb.AddTorque(smoothedTorque, ForceMode.Force);
//     }

//     void ApplyXStayForce()
//     {
//         float vx = rb.velocity.x;
//         float dampingStrength = 50f;
//         float correctiveForceX = -vx * dampingStrength;
//         rb.AddForce(new Vector3(correctiveForceX, 0f, 0f), ForceMode.Force);
//     }

//     public void SetTargetRotation(Quaternion newTarget)
//     {
//         targetRotation = newTarget;
//         Debug.Log($"🎯 新しい目標角度設定: {gameObject.name} → {newTarget.eulerAngles}");
//     }

//     public void SetTargetToZero()
//     {
//         SetTargetRotation(Quaternion.identity);
//     }

//     public void SetTargetTo180Y()
//     {
//         SetTargetRotation(Quaternion.Euler(0f, 180f, 0f));
//     }

//     public void LockCurrentRotation()
//     {
//         targetRotation = transform.rotation;
//         Debug.Log($"🔒 現在角度をロック: {gameObject.name} → {targetRotation.eulerAngles}");
//     }

//     public void SetStabilizing(bool enable)
//     {
//         isStabilizing = enable;
//         Debug.Log($"⚡ 安定化 {(enable ? "有効" : "無効")}: {gameObject.name}");

//         if (!enable && rb != null)
//         {
//             rb.angularVelocity = Vector3.zero;
//         }
//     }

//     public void SetGrounded(bool grounded)
//     {
//         isGrounded = grounded;

//         if (grounded)
//         {
//             stabilizeForce = 250f;
//             dampingForce = 10f;
//         }
//         else
//         {
//             stabilizeForce = 5f;
//             dampingForce = 5f;
//         }
//     }

//     void OnDestroy()
//     {
//         Debug.Log($"🗑️ GroupStickController破棄: {gameObject.name}");
//     }
//     void OnCollisionStay(Collision collision)
// {
//         if (collision.gameObject.layer != 9)
//         {
//             SetGrounded(true);
//             Debug.Log($"🛬 衝突検知: {gameObject.name} → {collision.gameObject.name}");
//         }
// }

// void OnCollisionExit(Collision collision)
// {
  
//         SetGrounded(false);
// }

}
