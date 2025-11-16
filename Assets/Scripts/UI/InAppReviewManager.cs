using System.Collections;
using UnityEngine;

public static class InAppReviewManager
{
    public static IEnumerator RequestReview()
    {
#if UNITY_IOS
        UnityEngine.iOS.Device.RequestStoreReview();
        yield break;

#elif UNITY_ANDROID
        var reviewManager = new Google.Play.Review.ReviewManager();
        var requestFlowOperation = reviewManager.RequestReviewFlow();
        yield return requestFlowOperation;
        if(requestFlowOperation.Error != Google.Play.Review.ReviewErrorCode.NoError)
        {
            // Log error. For example, using requestFlowOperation.Error.ToString().
            yield break;
        }
        var playReviewInfo = requestFlowOperation.GetResult();

        var launchFlowOperation = reviewManager.LaunchReviewFlow(playReviewInfo);
        yield return launchFlowOperation;
        playReviewInfo = null; // Reset the object
        if(launchFlowOperation.Error != Google.Play.Review.ReviewErrorCode.NoError)
        {
            // Log error. For example, using requestFlowOperation.Error.ToString().
            yield break;
        }
#else
        Debug.Log("レビュー促進表示");
#endif
        yield break;
    }
}