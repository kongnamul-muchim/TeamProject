using UnityEngine;

namespace HideAndInk.Core.Utilities
{
    /// <summary>
    /// MonoBehaviour Singleton 베이스 클래스
    /// DontDestroyOnLoad + 자동 생성 + 중복 제거 지원
    /// </summary>
    /// <typeparam name="T">파생 클래스 타입</typeparam>
    public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static T _instance;
        private static readonly object _lock = new object();
        private static bool _applicationIsQuitting;

        /// <summary>
        /// 전역 인스턴스 접근 (스레드 안전)
        /// </summary>
        public static T Instance
        {
            get
            {
                if (_applicationIsQuitting)
                {
                    Debug.LogWarning($"[Singleton] Instance of {typeof(T)} requested after application quit. Returning null.");
                    return null;
                }

                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = FindObjectOfType<T>();

                        if (_instance == null)
                        {
                            var singletonGO = new GameObject($"[{typeof(T).Name}]");
                            _instance = singletonGO.AddComponent<T>();
                            DontDestroyOnLoad(singletonGO);
                        }
                    }

                    return _instance;
                }
            }
        }

        protected virtual void Awake()
        {
            if (_instance != null && _instance != this as T)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this as T;
            DontDestroyOnLoad(gameObject);
        }

        protected virtual void OnDestroy()
        {
            if (_instance == this as T)
            {
                _instance = null;
            }
        }

        private void OnApplicationQuit()
        {
            _applicationIsQuitting = true;
        }
    }
}
