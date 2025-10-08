using System;
using System.Collections.Generic;

namespace PolarAI.AICore.XAI.Grok.Model
{
	[Serializable]
	public sealed class GrokChatMessage
	{
		public string role; // system, user, assistant
		public string content; // 純文字；進階多模內容可另擴充
	}

	[Serializable]
	public sealed class GrokChatRequest
	{
		public string model;
		public List<GrokChatMessage> messages;
		public float temperature = 0.7f;
		public int max_tokens = 1024;
		public bool stream = false;
	}

	[Serializable]
	public sealed class GrokChatChoice
	{
		public int index;
		public GrokChatMessage message;
		public string finish_reason;
	}

	[Serializable]
	public sealed class GrokChatResponse
	{
		public string id;
		public long created;
		public string model;
		public List<GrokChatChoice> choices;
	}
}


