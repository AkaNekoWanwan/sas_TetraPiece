using UnityEngine;
using System.Threading.Tasks;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    public AudioClip discSound;
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

    private async void Start()
    {
        audioSource.mute = IsMute("IsSe");
        bgmAudioSource.mute = IsMute("IsMusic"); 
        if (bgmAudioSource != null && bgmAudioClip != null )
        {
            bgmAudioSource.clip = bgmAudioClip;
            bgmAudioSource.loop = true;
            bgmAudioSource.Play();
        }
    }

    // 決定音
    public void PlayDiscSound()
    {
        audioSource.pitch = 1f;
        audioSource.PlayOneShot(discSound);
    }

    public void PlayMergeSound(int comboCount = 0)
    {
        // comboCount += 5; // 最低音高を5半音上げる
        audioSource.pitch = Mathf.Pow(1.059463f, comboCount);
        audioSource.PlayOneShot(mergeSound);
    }

    public void PlayHoldSound()
    {
        audioSource.pitch = 1f;
        audioSource.PlayOneShot(holdSound);
    }

    public void PlayPlaceSound()
    {
        audioSource.pitch = 1f;
        audioSource.PlayOneShot(placeSound);
    }
    public void PlayClearSound()
    {
        audioSource.pitch = 1f;
        audioSource.volume = 0.3f;
        audioSource.PlayOneShot(clearSound);
    }
    public void PlayCardFlipSound()
    {
        audioSource.pitch = 1f;
        audioSource.volume = 0.3f;
        audioSource.PlayOneShot(cardFlipSound);
    }

    private bool IsMute(string key)
    {
        return PlayerPrefs.GetInt(key, 1) == 0 || GameConst.IsMute();
    }
}
