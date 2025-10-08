using System;

namespace PolarAI.Scripts.Core.Ollama.Model
{
    [Serializable]
    public class OllamaMessage
    {
        public string role;    // "user" | "assistant" | "system"
        public string content;
    }
}