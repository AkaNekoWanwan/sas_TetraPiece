using UnityEngine;

namespace AkanekoLib
{
    public class Singleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        [Header("Singletion Properties")]
        public bool DontDestroyOnLoading = false;

        private static T _instance = null;
        public static T Instance
        {
            get
            {
                if (_instance == null)
                {
                    // シーン内でインスタンスを探す
                    _instance = FindObjectOfType<T>();
                    // // Debug.Log("_instance:" + _instance + ", " + typeof(T).Name);
                    if (_instance == null)
                    {
                        // 見つからなければ新しいGameObjectに追加する
                        GameObject obj = new GameObject(typeof(T).Name);
                        _instance = obj.AddComponent<T>();
                    }
                }
                return _instance;
            }
        }

        protected virtual void Awake()
        {
            if (_instance == null)
            {
                _instance = this as T;
                if (DontDestroyOnLoading)
                    DontDestroyOnLoad(this.gameObject);
            }
            else if (Instance.GetInstanceID() != this.GetInstanceID())
            {
                Debug.LogWarning($"Singleton {typeof(T).Name} の重複インスタンスを検出。新しいインスタンスを破棄します。", this);
                Destroy(gameObject);
            }
        }
        // protected virtual void AwakeUnique(){}
    }
}