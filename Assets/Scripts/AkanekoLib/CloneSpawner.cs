using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

/// <summary>
/// 消える時に自身の分身を作る関数。できるだけ呼ばれないのが望ましい
/// </summary>
public class CloneSpawner : MonoBehaviour
{
    private UnityEvent<CloneSpawner> _onDestroy = null;
    private bool _isCloneSpawn = false;
    private bool _isInitialize = false;

    public void Awake()
    { 
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _isCloneSpawn = false;
        // // Debug.Log("シーン再読み込みを検知したよ:" + _isCloneSpawn);
    }

    private void OnDestroy()
    {
        TryCloneSpawn();
    }
    public void Initialize()
    { 
        // // Debug.Log("初期化！");
        _isInitialize = true;
        _isCloneSpawn = true;
    }
    public void AddOnDestroy(UnityAction<CloneSpawner> callback)
    { 
        if(_onDestroy == null)
            _onDestroy = new UnityEvent<CloneSpawner>();
        _onDestroy.AddListener(callback); 
    }
    public void SetDestroyEvent(UnityEvent<CloneSpawner> callbackEvent)
    { 
        _onDestroy = callbackEvent;
    }
    public void SetIsCloneSpawn(bool isCloneSpawn)
    { 
        _isCloneSpawn = isCloneSpawn;
    }

    // シーンリロード時でなければ分身を作成
    private void TryCloneSpawn()
    {
        // // Debug.Log("壊れるよ！:" + this.gameObject.name + ", " + this.transform.parent + ", " + _isCloneSpawn + ", " + _isInitialize);
        if (_isCloneSpawn && _isInitialize && this.transform.parent != null && Application.isPlaying)
        {
            CloneSpawner clone = Instantiate(this); 
            clone.Initialize();
            clone.SetDestroyEvent(_onDestroy);       

            _onDestroy?.Invoke(clone);
        }
    }
}
