using UnityEngine;

namespace AbxrLib.Runtime.Common
{
    public class CoroutineRunner : MonoBehaviour
    {
        private static CoroutineRunner _instance;

        public static CoroutineRunner Instance => _instance;

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
            }
            else if (_instance != this)
            {
                // Prevent duplicates
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }
    }
}