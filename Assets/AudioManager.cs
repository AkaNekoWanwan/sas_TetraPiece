using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    public AudioClip mergeSound;
    public AudioClip holdSound;
    public AudioClip placeSound;
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
        if (bgmAudioSource != null && bgmAudioClip != null)
        {
            bgmAudioSource.clip = bgmAudioClip;
            bgmAudioSource.loop = true;
            bgmAudioSource.Play();
        }
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
}
