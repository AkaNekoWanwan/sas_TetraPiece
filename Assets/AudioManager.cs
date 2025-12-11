using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    public AudioClip mergeSound;
    public AudioClip holdSound;
    public AudioClip placeSound;
    public AudioClip clearSound;
    public AudioClip cardFlipSound;
    public AudioClip bgmAudioClip;

    public AudioSource audioSource;
    public AudioSource bgmAudioSource;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        if (bgmAudioSource != null && bgmAudioClip != null )
        {
            bgmAudioSource.clip = bgmAudioClip;
            bgmAudioSource.loop = true;
            bgmAudioSource.Play();
        }

        audioSource.mute = IsMute("IsSe");
        bgmAudioSource.mute = IsMute("IsMusic");
    }

    public void PlayMergeSound()
    {
        audioSource.PlayOneShot(mergeSound);
    }

    public void PlayHoldSound()
    {
        audioSource.PlayOneShot(holdSound);
    }

    public void PlayPlaceSound()
    {
        audioSource.PlayOneShot(placeSound);
    }
    public void PlayClearSound()
    {
        audioSource.volume = 0.3f;
        audioSource.PlayOneShot(clearSound);
    }
    public void PlayCardFlipSound()
    {
        audioSource.volume = 0.3f;
        audioSource.PlayOneShot(cardFlipSound);
    }

    private bool IsMute(string key)
    {
        return PlayerPrefs.GetInt(key, 1) == 0 || GameConst.IsMute();
    }
}
