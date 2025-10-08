using System;
using Newtonsoft.Json;

namespace PolarAI.Scripts.AICore.Groq.Model
{
    [Serializable]
    public class ChatResponse
    {
        [JsonProperty("choices")]
        public Choice[] Choices;
    }
    
    [Serializable]
    public class Choice
    {
        [JsonProperty("index")] public int Index;
        [JsonProperty("message")] public ChatMessage Message;
    }

}