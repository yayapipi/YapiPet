using System.Collections.Generic;

namespace Akashic.Scripts.Model.Server.AI
{
    public class ChatGPTRequest
    {
        public string model { get; set; }
        public List<ChatGptMessage> messages { get; set; }
        public double temperature { get; set; }
        public object max_tokens { get; set; }
        public ResponseFormat response_format { get; set; }
        public bool stream { get; set; }
    }
    
    public class ChatGptMessage
    {
        public string role { get; set; }
        public string content { get; set; }
    }

    public class ResponseFormat
    {
        public string type { get; set; }
    }

}