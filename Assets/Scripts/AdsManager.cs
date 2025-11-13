using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using System;


public class AdsManager : MonoBehaviour
{
    string adUnitId;
    int retryAttempt;
    public int buffer;

    public StageBanner _stageBanner;
    public RewardedAdManager _rewardedAdManager;

    public event System.Action OnInterstitialHidden
    {
        add{
            MaxSdkCallbacks.Interstitial.OnAdHiddenEvent += (adUnitId, adInfo) => value?.Invoke();
        }
        remove{
            MaxSdkCallbacks.Interstitial.OnAdHiddenEvent -= (adUnitId, adInfo) => value?.Invoke();
        }
    }

    public static AdsManager instance;
    public void Awake()
    {
        if (instance == null)
        {
            instance = this;
                        DontDestroyOnLoad(this.gameObject);
        }
        else
        {
            Destroy(this.gameObject);
        }
    }
    // Start is called before the first frame update
    void Start()
    { 
          adUnitId   = "6b416402c9a83019";

        MaxSdkCallbacks.OnSdkInitializedEvent += (MaxSdkBase.SdkConfiguration sdkConfiguration) =>
        {
           
            InitializeInterstitialAds();
            _stageBanner.InitializeBannerAds();
            _rewardedAdManager.InitializeRewardedAds();

        };
        MaxSdk.SetSdkKey("EpIDwy0bhJT7B76E65tdJt8Wkp20-IrR2Oc9sbxuS-6BseH7R3bQzSfFTN1u0Jvxh88rOvyh2rPH0WX81eO7Km");
        //MaxSdk.SetTestDeviceAdvertisingIdentifiers(new string[] { "87FBF16D-0FCB-4CF4-AB0C-C1625A66F250" });
        MaxSdk.SetUserId("USER_ID");
        MaxSdk.InitializeSdk();
        
    }

    // Update is called once per frame
    void FixedUpdate()
    {
     
    //  buffer++;
    //     if (buffer == 600)
    //     {
    //         ShowAd();
    //         buffer = 0;
    //     }
       
      
    }
       public void InitializeInterstitialAds()
    {
        // Attach callback
        MaxSdkCallbacks.Interstitial.OnAdLoadedEvent += OnInterstitialLoadedEvent;
        MaxSdkCallbacks.Interstitial.OnAdLoadFailedEvent += OnInterstitialLoadFailedEvent;
        MaxSdkCallbacks.Interstitial.OnAdDisplayedEvent += OnInterstitialDisplayedEvent;
        MaxSdkCallbacks.Interstitial.OnAdClickedEvent += OnInterstitialClickedEvent;
        MaxSdkCallbacks.Interstitial.OnAdHiddenEvent += OnInterstitialHiddenEvent;
        MaxSdkCallbacks.Interstitial.OnAdDisplayFailedEvent += OnInterstitialAdFailedToDisplayEvent;
        // Load the first interstitial
        LoadInterstitial();
    }

    private void LoadInterstitial()
    {
        MaxSdk.LoadInterstitial(adUnitId);
    }

    private void OnInterstitialLoadedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
    {
        // Interstitial ad is ready for you to show. MaxSdk.IsInterstitialReady(adUnitId) now returns 'true'

        // Reset retry attempt
        Debug.Log("Loaded interstitial ad successfully");
        retryAttempt = 0;
    }

    private void OnInterstitialLoadFailedEvent(string adUnitId, MaxSdkBase.ErrorInfo errorInfo)
    {
        // Interstitial ad failed to load 
        // AppLovin recommends that you retry with exponentially higher delays, up to a maximum delay (in this case 64 seconds)

        retryAttempt++;
        double retryDelay = Math.Pow(2, Math.Min(6, retryAttempt));
        Debug.Log($"Interstitial ad failed to load with error code: {errorInfo.Code} - {errorInfo.Message}. Retrying in {retryDelay} seconds");
        Invoke("LoadInterstitial", (float)retryDelay);
        LoadInterstitial();

    }
   // インプレッションごとの収益データを取得
    private void OnAdRevenuePaid(MaxSdkBase.AdInfo adInfo)
    {
        Debug.Log($"Ad Revenue Paid: {adInfo.Revenue}");
        Debug.Log($"eCPM: {adInfo.Revenue * 1000} USD"); // eCPMの計算
        Debug.Log($"Network Name: {adInfo.NetworkName}");
        Debug.Log($"Placement: {adInfo.Placement}");
    }
    private void OnInterstitialDisplayedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo) {
       //LoadInterstitial();

    }

    private void OnInterstitialAdFailedToDisplayEvent(string adUnitId, MaxSdkBase.ErrorInfo errorInfo, MaxSdkBase.AdInfo adInfo)
    {
        // Interstitial ad failed to display. AppLovin recommends that you load the next ad.
        LoadInterstitial();
    }

    private void OnInterstitialClickedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo) { }

    private void OnInterstitialHiddenEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
    {
        // Interstitial ad is hidden. Pre-load the next ad.
        LoadInterstitial();
    }
    public void ShowAd()
    {
        
        if (MaxSdk.IsInterstitialReady(adUnitId))
        {
            MaxSdk.ShowInterstitial(adUnitId);
           
        }
        else
        {
  
        }
    }
 


  
    

}
