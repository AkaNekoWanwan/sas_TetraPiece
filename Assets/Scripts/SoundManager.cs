using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public AudioSource se;
    // Start is called before the first frame update
    void Start()
    {

se=GetComponent<AudioSource>();
      
    }

    // Update is called once per frame
    void Update()
    {

    }
    public void PlaySound()
    {
      
            se.Play();
        
    }
}
