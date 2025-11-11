using UnityEngine;
using System.Collections.Generic;

public class CollisionDetector : MonoBehaviour
{
    private HashSet<Rigidbody> touchingRigidbodies = new HashSet<Rigidbody>();
    private Rigidbody myRigidbody;
    
    void Awake()
    {
        myRigidbody = GetComponentInParent<Rigidbody>();
    }
    
    // void OnCollisionStay(Collision collision)
    // {
    //     Rigidbody otherRb = collision.rigidbody;
    //     if (otherRb != null && otherRb != myRigidbody)
    //     {
    //         touchingRigidbodies.Add(otherRb);
    //         // Debug.Log($"接触中: {myRigidbody.name} と {otherRb.name}");
    //     }
    // }
    
    // void OnCollisionExit(Collision collision)
    // {
    //     Rigidbody otherRb = collision.rigidbody;
    //     if (otherRb != null)
    //     {
    //         touchingRigidbodies.Remove(otherRb);
    //         // Debug.Log($"接触終了: {myRigidbody.name} と {otherRb.name}");
    //     }
    // }
    
    // public bool IsTouchingWith(Rigidbody other)
    // {
    //     return touchingRigidbodies.Contains(other);
    // }
    
    // public List<Rigidbody> GetTouchingRigidbodies()
    // {
    //     return new List<Rigidbody>(touchingRigidbodies);
    // }
    
    // // 定期的にクリーンアップ（破棄されたオブジェクトを除去）
    // void Update()
    // {
    //     touchingRigidbodies.RemoveWhere(rb => rb == null);
    // }
}