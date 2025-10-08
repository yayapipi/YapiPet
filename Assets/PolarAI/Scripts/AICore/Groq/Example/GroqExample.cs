using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PolarAI.Scripts.AICore.Groq.Example
{
    public class GroqExample : MonoBehaviour
    {
        [Header("Groq Settings")] 
        
        [SerializeField]
        [Tooltip("Get Groq API: https://console.groq.com/keys")] 
        private string apiKey = "YOUR_GROQ_API_KEY";

        [SerializeField]
        [Tooltip("Check Groq Model: https://console.groq.com/home")] 
        private string model = "openai/gpt-oss-120b";

        [Range(0f, 2f)] 
        [SerializeField] private float temperature = 0.2f;

        [Header("UI (Unity UI)")] 
        [SerializeField]
        private Button sendButton; // 拖入要測試的 Button

        [SerializeField] private TMP_InputField inputField; // 也可留空，會用預設提示
        [SerializeField] private Text outputText; // 顯示結果

        private GroqAICore GroqAICore = new();

        private void Start()
        {
            GroqAICore.Initialize(apiKey);
            GroqAICore.SetModel(model);
            GroqAICore.SetTemperature(temperature);
            if (sendButton != null) sendButton.onClick.AddListener(OnClickSend);
            
        }

        private void OnDisable()
        {
            if (sendButton != null) sendButton.onClick.RemoveListener(OnClickSend);
        }

        private void OnClickSend()
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                Debug.Log("【Error】Please Set Groq API Key First.");
                return;
            }

            GroqAICore.SendChat("user", inputField.text, (content, isSuccess) =>
            {
                outputText.text = content;
            });
        }

      
    }
}