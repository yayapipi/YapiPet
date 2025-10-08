using System;
using System.Collections.Generic;

namespace PolarAI.Scripts.Core.Ollama.Model
{
    [Serializable]
    public class OllamaChatRequest
    {
        public string model;
        public List<OllamaMessage> messages;
        public bool stream = true;
    }
}