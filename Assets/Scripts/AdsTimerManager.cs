using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AdsTimerManager : MonoBehaviour
{
    private float _elapsedTime = 0f;
    private bool _isCounter = true;
    public bool counter_flag = false;
    
    static public AdsTimerManager instance;

    public float ElapsedTime{ get => _elapsedTime; set => _elapsedTime = value; }
    public bool IsCounter{ get => _isCounter; set => _isCounter = value; }
    
    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    void Start()
    {
        Application.targetFrameRate = 60;
        _isCounter = true;
    }
    void Update()
    {
        if (_isCounter == true)
        {
            _elapsedTime += Time.deltaTime;
        }
    }
}