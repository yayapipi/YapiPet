using System;
using System.Collections.Generic;

namespace PolarAI.AICore.XAI.Grok.Model
{
	[Serializable]
	public sealed class GrokImageGenerationRequest
	{
		public string model;
		public string prompt;
		public int width = 1024;
		public int height = 1024;
		public string format = "png"; // png or jpeg 等
		public string response_format = "b64_json"; // b64_json 或 url
		public int n = 1;
	}

	[Serializable]
	public sealed class GrokImageData
	{
		public string b64_json; // base64 圖片
		public string url; // 有些回應可能提供 URL
	}

	[Serializable]
	public sealed class GrokImageGenerationResponse
	{
		public List<GrokImageData> data;
	}
}


