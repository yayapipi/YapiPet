using System;

namespace PolarAI.AICore.XAI.Grok
{
	/// <summary>
	/// 純 C# 設定類別（非 ScriptableObject）。
	/// 可在 Inspector 於 MonoBehaviour 欄位中直接配置；
	/// 也可於程式碼動態指定。
	/// </summary>
	[Serializable]
	public sealed class GrokClientOptions
	{
		public string baseUrl = "https://api.x.ai/v1";
		public string defaultModel = GrokModelCatalog.Grok4FastReasoning;
		public string apiKey = string.Empty;

		public int requestTimeoutSeconds = 120;
		public int maxRetries = 3;
		public float retryBaseDelaySeconds = 1f;
		public float minDelayBetweenRequestsSeconds = 0.25f;
		public float maxRetryDelaySeconds = 30f;

		public string ResolveApiKey()
		{
			if (!string.IsNullOrEmpty(apiKey)) return apiKey;
			var env = Environment.GetEnvironmentVariable("XAI_API_KEY");
			return string.IsNullOrEmpty(env) ? string.Empty : env;
		}
	}
}


