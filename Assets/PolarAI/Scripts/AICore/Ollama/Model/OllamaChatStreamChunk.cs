using System;

namespace PolarAI.Scripts.Core.Ollama.Model
{
    [Serializable]
    public class OllamaChatStreamChunk
    {
        public string model;
        public string created_at;
        public OllamaMessage message;
        public bool done;
        public string done_reason;
    }
}