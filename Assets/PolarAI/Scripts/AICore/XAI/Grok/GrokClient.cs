using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using PolarAI.AICore.XAI.Grok.Model;

namespace PolarAI.AICore.XAI.Grok
{
	/// <summary>
	/// Grok (xAI) API Client。提供 Chat 與 Image 生成。
	/// </summary>
	public sealed class GrokClient
	{
		private readonly GrokClientOptions _options;

		public GrokClient(GrokClientOptions options)
		{
			_options = options ?? new GrokClientOptions();
		}

		public IEnumerator ChatCoroutine(GrokChatRequest request, Action<GrokChatResponse> onSuccess, Action<string> onError)
		{
			if (request == null) { onError?.Invoke("request is null"); yield break; }
			if (string.IsNullOrEmpty(request.model)) request.model = _options.defaultModel;

			string apiKey = _options.ResolveApiKey();
			if (string.IsNullOrEmpty(apiKey)) { onError?.Invoke("API Key is empty. Set via options or env XAI_API_KEY."); yield break; }

			string url = _options.baseUrl + "/chat/completions";
			var json = JsonUtility.ToJson(request);
			yield return SendWithRetry(url, json, (responseText) =>
			{
				try
				{
					var resp = JsonUtility.FromJson<GrokChatResponse>(responseText);
					onSuccess?.Invoke(resp);
				}
				catch (Exception ex)
				{
					onError?.Invoke("Parse error: " + ex.Message + "; raw: " + responseText);
				}
			}, onError);
		}

		public IEnumerator GenerateImageCoroutine(GrokImageGenerationRequest request, Action<Texture2D> onSuccess, Action<string> onError)
		{
			if (request == null) { onError?.Invoke("request is null"); yield break; }
			if (string.IsNullOrEmpty(request.model)) request.model = GrokModelCatalog.Grok2Image_1212;

			string apiKey = _options.ResolveApiKey();
			if (string.IsNullOrEmpty(apiKey)) { onError?.Invoke("API Key is empty. Set via options or env XAI_API_KEY."); yield break; }

			string url = _options.baseUrl + "/images/generations";
			var json = JsonUtility.ToJson(request);

			int attempt = 0;
			int maxRetries = _options.maxRetries;
			float baseDelay = _options.retryBaseDelaySeconds;
			float maxDelay = _options.maxRetryDelaySeconds;
			float minBetween = _options.minDelayBetweenRequestsSeconds;

			while (true)
			{
				if (minBetween > 0f) yield return new WaitForSecondsRealtime(minBetween);

				using (var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
				{
					byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
					req.uploadHandler = new UploadHandlerRaw(bodyRaw);
					req.downloadHandler = new DownloadHandlerBuffer();
					req.SetRequestHeader("Content-Type", "application/json");
					req.SetRequestHeader("Authorization", "Bearer " + apiKey);
					req.timeout = _options.requestTimeoutSeconds;

					yield return req.SendWebRequest();

					bool success = req.result == UnityWebRequest.Result.Success;
					int status = (int)req.responseCode;
					string responseText = req.downloadHandler != null ? req.downloadHandler.text : string.Empty;

					if (success && status >= 200 && status < 300)
					{
						// 先嘗試解析，決定後續動作；避免在 try/catch 內 yield
						string downloadUrl = null;
						Texture2D parsedTexture = null;
						string parseError = null;

						try
						{
							var resp = JsonUtility.FromJson<GrokImageGenerationResponse>(responseText);
							if (resp == null || resp.data == null || resp.data.Count == 0)
							{
								parseError = "Invalid image response";
							}
							else
							{
								var data = resp.data[0];
								if (!string.IsNullOrEmpty(data.b64_json))
								{
									byte[] imageBytes = Convert.FromBase64String(data.b64_json);
									var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
									if (!tex.LoadImage(imageBytes))
									{
										parseError = "Failed to load image from bytes";
									}
									else
									{
										parsedTexture = tex;
									}
								}
								else if (!string.IsNullOrEmpty(data.url))
								{
									downloadUrl = data.url;
								}
								else
								{
									parseError = "Invalid image response (no data)";
								}
							}
						}
						catch (Exception ex)
						{
							parseError = "Parse error: " + ex.Message + "; raw: " + responseText;
						}

						if (parseError != null)
						{
							onError?.Invoke(parseError);
							yield break;
						}

						if (parsedTexture != null)
						{
							onSuccess?.Invoke(parsedTexture);
							yield break;
						}

						if (!string.IsNullOrEmpty(downloadUrl))
						{
							yield return DownloadImageFromUrl(downloadUrl, onSuccess, onError);
							yield break;
						}

						// 不應該到這裡
						onError?.Invoke("Invalid image response");
						yield break;
					}

					if ((status == 429 || status == 503 || status == 504) && attempt < maxRetries)
					{
						attempt++;
						float delay = Mathf.Min(maxDelay, baseDelay * Mathf.Pow(2f, attempt - 1));
						if (req.GetResponseHeader("Retry-After") is string ra && float.TryParse(ra, out var raSec))
						{
							delay = Mathf.Max(delay, raSec);
						}
						yield return new WaitForSecondsRealtime(delay);
						continue;
					}

					onError?.Invoke($"HTTP {(status > 0 ? status : -1)}: {req.error}\n{responseText}");
					yield break;
				}
			}
		}

		private IEnumerator DownloadImageFromUrl(string url, Action<Texture2D> onSuccess, Action<string> onError)
		{
			using (var req = UnityWebRequestTexture.GetTexture(url))
			{
				req.timeout = _options.requestTimeoutSeconds;
				yield return req.SendWebRequest();
				if (req.result != UnityWebRequest.Result.Success)
				{
					onError?.Invoke("Image download failed: " + req.error);
					yield break;
				}
				var tex = DownloadHandlerTexture.GetContent(req);
				onSuccess?.Invoke(tex);
			}
		}

		private IEnumerator SendWithRetry(string url, string jsonBody, Action<string> onOk, Action<string> onError)
		{
			string apiKey = _options.ResolveApiKey();
			int maxRetries = _options.maxRetries;
			float baseDelay = _options.retryBaseDelaySeconds;
			float maxDelay = _options.maxRetryDelaySeconds;
			float minBetween = _options.minDelayBetweenRequestsSeconds;
			int attempt = 0;

			while (true)
			{
				if (minBetween > 0f) yield return new WaitForSecondsRealtime(minBetween);

				using (var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
				{
					byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
					req.uploadHandler = new UploadHandlerRaw(bodyRaw);
					req.downloadHandler = new DownloadHandlerBuffer();
					req.SetRequestHeader("Content-Type", "application/json");
					req.SetRequestHeader("Authorization", "Bearer " + apiKey);
					req.timeout = _options.requestTimeoutSeconds;

					yield return req.SendWebRequest();

					bool success = req.result == UnityWebRequest.Result.Success;
					int status = (int)req.responseCode;
					string text = req.downloadHandler != null ? req.downloadHandler.text : string.Empty;

					if (success && status >= 200 && status < 300)
					{
						onOk?.Invoke(text);
						yield break;
					}

					// Retry on 429/503/504
					if ((status == 429 || status == 503 || status == 504) && attempt < maxRetries)
					{
						attempt++;
						float delay = Mathf.Min(maxDelay, baseDelay * Mathf.Pow(2f, attempt - 1));
						if (req.GetResponseHeader("Retry-After") is string ra && float.TryParse(ra, out var raSec))
						{
							delay = Mathf.Max(delay, raSec);
						}
						yield return new WaitForSecondsRealtime(delay);
						continue;
					}

					onError?.Invoke($"HTTP {(status > 0 ? status : -1)}: {req.error}\n{text}");
					yield break;
				}
			}
		}
	}
}


