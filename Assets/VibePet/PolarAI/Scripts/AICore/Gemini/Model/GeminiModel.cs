using System;
using Newtonsoft.Json;

namespace PolarAI.Scripts.Core.Gemini.Model
{
    [Serializable]
    public class GenerateContentRequest
    {
        [JsonProperty("contents")] public ContentReq[] contents;
        [JsonProperty("generation_config")] public GenerationConfig generation_config; // optional
    }

    [Serializable]
    public class GenerationConfig
    {
        [JsonProperty("temperature")] public float temperature;
        [JsonProperty("top_p")] public float top_p;
    }

    [Serializable]
    public class ContentReq
    {
        [JsonProperty("role")] public string role;
        [JsonProperty("parts")] public PartReq[] parts;
    }

    [Serializable]
    public class PartReq
    {
        public string text;
        public InlineDataReq inline_data;
        public static PartReq FromText(string t) => new PartReq { text = t, inline_data = null };
        public static PartReq FromInlineData(InlineDataReq d) => new PartReq { text = null, inline_data = d };
    }

    [Serializable]
    public class InlineDataReq
    {
        [JsonProperty("mime_type")] public string mime_type; // e.g., "image/png"
        [JsonProperty("data")] public string data; // base64
    }

    [Serializable]
    public class GenContentResponse
    {
        [JsonProperty("candidates")] public Candidate[] candidates;
    }

    [Serializable]
    public class Candidate
    {
        [JsonProperty("content")] public ContentResp content;
    }

    [Serializable]
    public class ContentResp
    {
        [JsonProperty("parts")] public PartResp[] parts;
    }

    [Serializable]
    public class PartResp
    {
        public string text;
        public InlineDataResp inlineData;
    }

    [Serializable]
    public class InlineDataResp
    {
        public string mimeType; // e.g., image/png
        public string data; // base64
    }
}