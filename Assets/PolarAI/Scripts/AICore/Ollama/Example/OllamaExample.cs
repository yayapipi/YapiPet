using System.Collections.Generic;
using PolarAI.Scripts.Core.Ollama.Model;
using UnityEngine;
using UnityEngine.UI;

namespace PolarAI.Scripts.Core.Ollama.Example
{
    public class OllamaExample : MonoBehaviour
    {
        [Header("Mode/Server")]
        public bool useTurbo = false;
        [Tooltip("Local Default：http://localhost:11434")]
        public string localHost = "http://localhost:11434";
        [Tooltip("Turbo Default：https://ollama.com")]
        public string turboHost = "https://ollama.com";

        [Header("Model & APIKey")]
        [Tooltip("Example：gpt-oss:20b 或 gpt-oss:120b（Turbo）...")]
        public string model = "gpt-oss:120b";
        [Tooltip("Only Required For Turbo Mode")]
        public string turboApiKey = "<YOUR_API_KEY>";

        [Header("聊天設定")]
        public bool stream = true;
        [TextArea(3, 10)]
        public string userInput = "為什麼天空是藍色的？";

        [Header("輸出")]
        [TextArea(10, 20)]
        public string output = "";

        [Header("UGUI 參考（Inspector 指派）")]
        public InputField userInputField;
        public Text outputText;
        public Button btnSend;
        public Button btnClearOutput;
        public Button btnClearMemory;


        private OllamaAICore _aiCore;
        private readonly List<OllamaMessage> _history = new List<OllamaMessage>();

        private void Awake()
        {
            PushStateToUI();
            WireUiEvents();
            BuildClient();
            if (outputText) outputText.text = output ?? "";
        }

        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                BuildClient();
            }
        }

        private void BuildClient()
        {
            OllamaClientOptions options;
            if (useTurbo)
            {
                options = OllamaClientOptions.DefaultTurbo(turboApiKey);
                if (!string.IsNullOrEmpty(turboHost)) options.Host = turboHost;
            }
            else
            {
                options = OllamaClientOptions.DefaultLocal();
                if (!string.IsNullOrEmpty(localHost)) options.Host = localHost;
            }

            _aiCore = new OllamaAICore(options);
        }

        [ContextMenu("Send Once (non-stream)")]
        public void SendOnce()
        {
            if (_aiCore == null) BuildClient();

            SyncFromUI();
            var messages = new List<OllamaMessage>();
            messages.AddRange(_history);
            messages.Add(new OllamaMessage { role = "user", content = userInput });

            output = "[Sending non-stream request...]\n";
            UpdateOutputUI();

            StopAllCoroutines();
            StartCoroutine(_aiCore.ChatOnce(
                model,
                messages,
                onCompleted: (text) =>
                {
                    output += text + "\n";
                    UpdateOutputUI();
                    _history.Add(new OllamaMessage { role = "user", content = userInput });
                    _history.Add(new OllamaMessage { role = "assistant", content = text });
                },
                onError: (err) =>
                {
                    output += $"[Error] {err}\n";
                    UpdateOutputUI();
                }
            ));
        }

        [ContextMenu("Send Stream")]
        public void SendStream()
        {
            if (_aiCore == null) BuildClient();

            SyncFromUI();
            var messages = new List<OllamaMessage>();
            messages.AddRange(_history);
            messages.Add(new OllamaMessage { role = "user", content = userInput });

            output = "[Streaming...]\n";
            UpdateOutputUI();

            StopAllCoroutines();
            StartCoroutine(_aiCore.ChatStream(
                model,
                messages,
                onToken: (token) =>
                {
                    output += token;
                    UpdateOutputUI();
                },
                onCompleted: (_) =>
                {
                    output += "\n[Done]\n";
                    UpdateOutputUI();
                    // 將完整輸出存回歷史（簡單做法：整段文本）
                    _history.Add(new OllamaMessage { role = "user", content = userInput });
                    _history.Add(new OllamaMessage { role = "assistant", content = output });
                },
                onError: (err) =>
                {
                    output += $"\n[Error] {err}\n";
                    UpdateOutputUI();
                }
            ));
        }

        // ============ UGUI 輔助 ============
        private void WireUiEvents()
        {
            
            if (userInputField)
            {
                userInputField.text = userInput;
                userInputField.onEndEdit.AddListener(v => { userInput = v; });
            }

            if (btnSend) btnSend.onClick.AddListener(() => { if (stream) SendStream(); else SendOnce(); });
            if (btnClearOutput) btnClearOutput.onClick.AddListener(OnClickClearOutput);
            if (btnClearMemory) btnClearMemory.onClick.AddListener(OnClickClearHistory);
        }

        private void SyncFromUI()
        {
            if (userInputField) userInput = userInputField.text;
            BuildClient();
        }

        private void PushStateToUI()
        {
            if (userInputField) userInputField.text = userInput;
            UpdateOutputUI();
        }

        private void UpdateOutputUI()
        {
            if (outputText) outputText.text = output ?? "";
        }

        // ============ UGUI Buttons ============
        public void OnClickClearOutput()
        {
            output = "";
            UpdateOutputUI();
        }

        public void OnClickClearHistory()
        {
            _history.Clear();
        }
    }
}