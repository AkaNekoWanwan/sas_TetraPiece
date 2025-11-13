using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Globalization;
using AppLovinMax.ThirdParty.MiniJson;
using AppLovinMax.Internal;
using UnityEngine.Events;
using com.adjust.sdk;
using System;

/// <summary>
/// リワードマネージャー。
/// 参考：https://developers.applovin.com/ja/max/unity/ad-formats/interstitial-ads/
/// </summary>
public class RewardedAdManager : MonoBehaviour
{
#if UNITY_IOS
    string adUnitId = "«iOS-ad-unit-ID»";   // プロジェクトごとに異なるIOS用のリワードのAd Unit Id
#else // UNITY_ANDROID
    string adUnitId = "b84d01fecdfa7f08";   // プロジェクトごとに異なるANDROID用、エディタ用のリワードのAd Unit Id
#endif

    int retryAttempt;

    public MaxSdkBase.AdInfo _adInfo = null;    // インステ広告マネージャーを参考に追加
    private UnityEvent _onLoaded = default;     // インステ広告マネージャーを参考に追加

    private UnityEvent _onReceivedReward = null;    // 自身で追加。リワードを受け取った瞬間の処理
    private UnityEvent _onFailureReward = null;     // 自身で追加。リワードを受け取れなかった瞬間の処理
    private bool _isReceivedReward = false;

    static public RewardedAdManager instance = null;
    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            transform.parent = null;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void InitializeRewardedAds()
    {
        // Attach callback
        MaxSdkCallbacks.Rewarded.OnAdLoadedEvent += OnRewardedAdLoadedEvent;
        MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent += OnRewardedAdLoadFailedEvent;
        MaxSdkCallbacks.Rewarded.OnAdDisplayedEvent += OnRewardedAdDisplayedEvent;
        MaxSdkCallbacks.Rewarded.OnAdClickedEvent += OnRewardedAdClickedEvent;
        MaxSdkCallbacks.Rewarded.OnAdRevenuePaidEvent += OnRewardedAdRevenuePaidEvent;
        MaxSdkCallbacks.Rewarded.OnAdHiddenEvent += OnRewardedAdHiddenEvent;
        MaxSdkCallbacks.Rewarded.OnAdDisplayFailedEvent += OnRewardedAdFailedToDisplayEvent;
        MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent += OnRewardedAdReceivedRewardEvent;

        // Load the first rewarded ad
        LoadRewardedAd();
    }

    private void LoadRewardedAd()
    {
        MaxSdk.LoadRewardedAd(adUnitId);
    }

    private void OnRewardedAdLoadedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
    {
        // Rewarded ad is ready for you to show. MaxSdk.IsRewardedAdReady(adUnitId) now returns 'true'.

        // Reset retry attempt
        retryAttempt = 0;
        // インステ広告マネージャーを参考に追加
        _adInfo = adInfo;
        _onLoaded?.Invoke();
    }

    private void OnRewardedAdLoadFailedEvent(string adUnitId, MaxSdkBase.ErrorInfo errorInfo)
    {
        // Rewarded ad failed to load
        // AppLovin recommends that you retry with exponentially higher delays, up to a maximum delay (in this case 64 seconds).

        retryAttempt++;
        double retryDelay = Mathf.Pow(2, Mathf.Min(6, retryAttempt));

        Invoke("LoadRewardedAd", (float) retryDelay);
    }

    private void OnRewardedAdDisplayedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo) {}

    private void OnRewardedAdFailedToDisplayEvent(string adUnitId, MaxSdkBase.ErrorInfo errorInfo, MaxSdkBase.AdInfo adInfo)
    {
        // Rewarded ad failed to display. AppLovin recommends that you load the next ad.
        LoadRewardedAd();
    }

    private void OnRewardedAdClickedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo) {}

    private void OnRewardedAdHiddenEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
    {
        // Rewarded ad is hidden. Pre-load the next ad
        LoadRewardedAd();

        if(_isReceivedReward)
        {

        }
        else
        {
            OnEvent(ref _onFailureReward);
            ResetEvent(ref _onReceivedReward);
        }
    }

    private void OnRewardedAdReceivedRewardEvent(string adUnitId, MaxSdk.Reward reward, MaxSdkBase.AdInfo adInfo)
    {
        // The rewarded ad displayed and the user should receive the reward.
        _isReceivedReward = true;
        OnEvent(ref _onReceivedReward);
        ResetEvent(ref _onFailureReward);
    }

    private void OnRewardedAdRevenuePaidEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
    {
        // Ad revenue paid. Use this callback to track user revenue.

        // 以下、インステマネージャーを参考に追加
        // Ad revenue
        double revenue = adInfo.Revenue;

        // Miscellaneous data
        string countryCode = MaxSdk.GetSdkConfiguration().CountryCode; // "US" for the United States, etc - Note: Do not confuse this with currency code which is "USD"!
        string networkName = adInfo.NetworkName; // Display name of the network that showed the ad (e.g. "AdColony")
        string adUnitIdentifier = adInfo.AdUnitIdentifier; // The MAX Ad Unit ID
        string placement = adInfo.Placement; // The placement this ad's postbacks are tied to

        TrackAdRevenue(adInfo);
    }
    /// <summary>
    /// インステマネージャーを参考に追加
    /// </summary>
    /// <param name="adInfo"></param>
    private void TrackAdRevenue(MaxSdkBase.AdInfo adInfo)
    {
        AdjustAdRevenue adjustAdRevenue = new AdjustAdRevenue("applovin_max_sdk");

        // adjustAdRevenue.SetRevenue(adInfo.Revenue, "USD");
        // adjustAdRevenue.AdRevenueNetwork = adInfo.NetworkName;
        // adjustAdRevenue.AdRevenueUnit = adInfo.AdUnitIdentifier;
        // adjustAdRevenue.AdRevenuePlacement = adInfo.Placement;

        // Adjust.TrackAdRevenue(adjustAdRevenue);

        // AdjustAdRevenue adjustAdRevenue = new AdjustAdRevenue(AdjustConfig.AdjustAdRevenueSourceAppLovinMAX);

        // adjustAdRevenue.setRevenue(adInfo.Revenue, "USD");
        // adjustAdRevenue.setAdRevenueNetwork(adInfo.NetworkName);
        // adjustAdRevenue.setAdRevenueUnit(adInfo.AdUnitIdentifier);
        // adjustAdRevenue.setAdRevenuePlacement(adInfo.Placement);

        // AdjustSdk.Adjust.trackAdRevenue(adjustAdRevenue);
    }

    // 自分で追加
    private void AddCallBack(ref UnityEvent events, UnityAction callback)
    {
        if(events == null)
            events = new UnityEvent();
        if(callback == null)
            return;
        events.AddListener(callback);
    }
    private void OnEvent(ref UnityEvent events )
    {
        events?.Invoke();
        ResetEvent(ref events);
    }
    private void ResetEvent(ref UnityEvent events )
    {
        events?.RemoveAllListeners();
    }



    /// <summary>
    /// リワード視聴
    /// </summary>
    public void ShowReward(UnityAction onSuccess, UnityAction onfailure)
    {
        try
        {
            if (MaxSdk.IsRewardedAdReady(adUnitId))
            {
                // FirebaseManager.Instance.EventWatchReward(true);
                MaxSdk.ShowRewardedAd(adUnitId);

                // 成功時の処理
                AddCallBack(ref _onReceivedReward, onSuccess);
                // 広告を最後まで見なかった時の処理
                AddCallBack(ref _onFailureReward, onfailure);
            }
            else
            {
                // FirebaseManager.Instance.EventWatchReward(false);
                // ステージスタートイベントの発火を試行
                GameDataManager.TryEventStageStart();
                // 失敗時の処理(広告が読み込めなかった)
                onfailure?.Invoke();
            }
        }
        catch (Exception ex)
        {
            // エラーログを記録
            // Firebase.Crashlytics.Crashlytics.LogException(ex);
            Debug.LogError("Show Inste Error Catch: " + ex.Message);
            // 失敗時の処理(例外処理が発生した)
            onfailure?.Invoke();
        }
    }
    // インステ広告マネージャーを参考に追加
    public void AddOnLoadedCallback(UnityAction onLoaded)
    {
        if(_onLoaded == null)
            _onLoaded = new UnityEvent();
        _onLoaded.AddListener(onLoaded);
    }
}
