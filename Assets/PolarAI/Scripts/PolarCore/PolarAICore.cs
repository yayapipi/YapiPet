using PolarAI.Scripts.Model;
using UnityEngine;

namespace PolarAI.Scripts.Core
{

    public class PolarAICore : MonoBehaviour
    {
        public SerializableDictionary<AIServices, string> APIKeyDict;
        public static PolarAICore Instance;
        

        private void Awake()
        {
            Instance = this;
        }

        public void InitialKey(SerializableDictionary<AIServices, string> apiKeyDict)
        {
            foreach (var apiKey in apiKeyDict)
            {
                
            }
        }
    }
}