using System;
using Newtonsoft.Json;

namespace PolarAI.Scripts.AICore.Groq.Model
{
    [Serializable]
    public class ChatMessage
    {
        [JsonProperty("role")] public string Role;
        [JsonProperty("content")] public string Content;

        public ChatMessage(string role, string content)
        {
            Role = role;
            Content = content;
        }
    }

    [Serializable]
    public class ChatRequest
    {
        [JsonProperty("model")] public string Model;
        [JsonProperty("messages")] public ChatMessage[] Messages;
        [JsonProperty("temperature")] public float Temperature;
    }
    
    public enum GroqRole
    {
        system,
        user
    }
}