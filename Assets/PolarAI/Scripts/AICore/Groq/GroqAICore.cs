using System;
using System.Collections;
using System.Text;
using Core;
using Newtonsoft.Json;
using PolarAI.Scripts.AICore.Groq.Model;
using UnityEngine;
using UnityEngine.Networking;

namespace PolarAI.Scripts.AICore.Groq
{
    public class GroqAICore
    {
        private string ApiKey = "YOUR_GROQ_API_KEY";
        private string AIModel = "openai/gpt-oss-120b";
        private float Temperature = 0.2f;
        private const string ChatCompletionsUrl = "https://api.groq.com/openai/v1/chat/completions";

        public void Initialize(string apiKey)
        {
            ApiKey = apiKey;
        }

        public void SetModel(string model)
        {
            AIModel = model;
        }

        public void SetTemperature(float temperature)
        {
            Temperature = temperature;
        }

        public void SendChat(string role, string prompt, Action<string, bool> onCompleted)
        {
            if (string.IsNullOrEmpty(ApiKey))
            {
                Debug.LogWarning("【Error】Please Initialize Groq API Key First.");
                return;
            }

            var req = new ChatRequest
            {
                Model = AIModel,
                Temperature = Temperature,
                Messages = new[]
                {
                    new ChatMessage(role, prompt)
                }
            };

            CoroutineManager.Instance.StartCoroutine(SendChatCoroutine(req, onCompleted));
        }

        public void SendChatList(ChatMessage[] chatMessages, Action<string, bool> onCompleted)
        {
            if (string.IsNullOrEmpty(ApiKey))
            {
                Debug.LogWarning("【Error】Please Initialize Groq API Key First.");
                return;
            }

            var req = new ChatRequest
            {
                Model = AIModel,
                Temperature = Temperature,
                Messages = chatMessages
            };

            CoroutineManager.Instance.StartCoroutine(SendChatCoroutine(req, onCompleted));
        }

        private IEnumerator SendChatCoroutine(ChatRequest request, Action<string, bool> onCompleted)
        {
            var json = JsonConvert.SerializeObject(request);
            var body = Encoding.UTF8.GetBytes(json);

            using var www = new UnityWebRequest(ChatCompletionsUrl, "POST");
            www.uploadHandler = new UploadHandlerRaw(body);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Authorization", "Bearer " + ApiKey);
            www.SetRequestHeader("Content-Type", "application/json");
            yield return www.SendWebRequest();

            var isError = www.result != UnityWebRequest.Result.Success;
            if (isError)
            {
                onCompleted?.Invoke($"【HTTP {www.responseCode}】{www.error}\n{www.downloadHandler.text}", false);
                yield break;
            }

            var responseText = www.downloadHandler.text;
            try
            {
                var resp = JsonConvert.DeserializeObject<ChatResponse>(responseText);
                var content = resp?.Choices is { Length: > 0 } ? resp.Choices[0].Message?.Content : null;

                if (string.IsNullOrEmpty(content))
                {
                    onCompleted?.Invoke("【Error】Reply Content Is Empty .\nRaw:\n" + responseText, false);
                }
                else
                {
                    onCompleted?.Invoke(content.Trim(), true);
                }
            }
            catch (Exception ex)
            {
                onCompleted?.Invoke("【JSON Deserialize Error】" + ex.Message + "\nRaw:\n" + responseText, false);
            }
        }
    }
}