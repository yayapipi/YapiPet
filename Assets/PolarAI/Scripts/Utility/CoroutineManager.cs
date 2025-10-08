using System.Collections;
using UnityEngine;

namespace Core
{
    public class CoroutineManager : MonoBehaviour
    {
        private static CoroutineManager instance;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
        }

        public static CoroutineManager Instance
        {
            get
            {
                if (instance == null)
                {
                    GameObject singletonObject = new GameObject();
                    instance = singletonObject.AddComponent<CoroutineManager>();
                    singletonObject.name = "CoroutineManagerSingleton";
                }

                return instance;
            }
        }

        public static void Trigger(IEnumerator method)
        {
            Instance.StartCoroutine(method);
        }
    }
}