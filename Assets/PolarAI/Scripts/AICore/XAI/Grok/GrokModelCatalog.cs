namespace PolarAI.AICore.XAI.Grok
{
	/// <summary>
	/// Grok 模型目錄。實際可用清單以 xAI 官方文件為準；
	/// 這裡提供常用名稱並允許以字串覆寫。
	/// </summary>
	public static class GrokModelCatalog
	{
		// Chat / Reasoning 類（依 xAI Console Models 更新）
		public const string Grok4FastReasoning = "grok-4-fast-reasoning";
		public const string Grok4FastNonReasoning = "grok-4-fast-non-reasoning";
		public const string Grok4_0709 = "grok-4-0709";
		public const string Grok3 = "grok-3";
		public const string Grok3Mini = "grok-3-mini";
		public const string Grok2_1212_us_east_1 = "grok-2-1212us-east-1";
		public const string Grok2_1212_eu_west_1 = "grok-2-1212eu-west-1";
		public const string Grok2Vision_1212_us_east_1 = "grok-2-vision-1212us-east-1";
		public const string Grok2Vision_1212_eu_west_1 = "grok-2-vision-1212eu-west-1";

		// 圖片生成（Console 顯示）
		public const string Grok2Image_1212 = "grok-2-image-1212";
	}
}


