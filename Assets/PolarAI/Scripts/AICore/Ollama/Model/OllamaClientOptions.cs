namespace PolarAI.Scripts.Core.Ollama.Model
{
    public class OllamaClientOptions
    {
        public OllamaMode Mode = OllamaMode.Local;

        // Local 預設 http://localhost:11434
        // Turbo 預設 https://ollama.com
        public string Host;

        // 僅 Turbo 需要
        // 預設使用 Authorization: Bearer <API_KEY>
        public string ApiKey;
        public string ApiKeyHeaderName = "Authorization";
        public string ApiKeyHeaderPrefix = "Bearer ";

        // 連線/回應逾時（秒）
        public int Timeout = 180;

        public static OllamaClientOptions DefaultLocal()
        {
            return new OllamaClientOptions
            {
                Mode = OllamaMode.Local,
                Host = "http://localhost:11434",
                ApiKey = null,
                ApiKeyHeaderName = "Authorization",
                ApiKeyHeaderPrefix = "Bearer ",
                Timeout = 180
            };
        }

        public static OllamaClientOptions DefaultTurbo(string apiKeyPlaceholder = "<YOUR_API_KEY>")
        {
            return new OllamaClientOptions
            {
                Mode = OllamaMode.Turbo,
                Host = "https://ollama.com",
                ApiKey = apiKeyPlaceholder,
                ApiKeyHeaderName = "Authorization",
                ApiKeyHeaderPrefix = "Bearer ",
                Timeout = 180
            };
        }
    }
}