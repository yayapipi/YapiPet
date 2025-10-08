using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI; // Text (Legacy) & RawImage
using PolarAI.AICore.XAI.Grok.Model;

namespace PolarAI.AICore.XAI.Grok.Example
{
	public class GrokExampleUGUI : MonoBehaviour
	{
		[Header("References")]
		public Text outputText; // Text (Legacy)
		public RawImage outputImage; // 用於顯示生成圖片
		public InputField promptInput; // 輸入 Prompt
		public Button chatButton;
		public Button imageButton;
		public GameObject loadingRoot;
		public Text loadingText;
		[Header("Options")]
		public GrokClientOptions options = new GrokClientOptions();

		private GrokClient _client;

		private void Awake()
		{
			_client = new GrokClient(options);
		}


		[ContextMenu("Run Chat (Grok 4 Fast)")]
		public void RunChat()
		{
			StartCoroutine(RunChatRoutine());
		}

		private IEnumerator RunChatRoutine()
		{
			ShowLoading("Chatting...");
			SetInteractable(false);
			var req = new GrokChatRequest
			{
				model = string.IsNullOrEmpty(options?.defaultModel) ? GrokModelCatalog.Grok4FastReasoning : options.defaultModel,
				messages = new List<GrokChatMessage>
				{
					new GrokChatMessage{ role = "system", content = "You are a helpful assistant."},
					new GrokChatMessage{ role = "user", content = ResolvePromptForChat() }
				}
			};
			GrokChatResponse resp = null; string err = null;
			yield return _client.ChatCoroutine(req, r => resp = r, e => err = e);
			HideLoading();
			SetInteractable(true);
			if (!string.IsNullOrEmpty(err)) OnError(err); else OnChatSuccess(resp);
		}


		[ContextMenu("Generate Image")]
		public void RunImage()
		{
			StartCoroutine(RunImageRoutine());
		}

		private IEnumerator RunImageRoutine()
		{
			ShowLoading("Generating image...");
			SetInteractable(false);
			var req = new GrokImageGenerationRequest
			{
				model = GrokModelCatalog.Grok2Image_1212,
				prompt = ResolvePromptForImage()
			};
			Texture2D tex = null; string err = null;
			yield return _client.GenerateImageCoroutine(req, t => tex = t, e => err = e);
			HideLoading();
			SetInteractable(true);
			if (!string.IsNullOrEmpty(err)) OnError(err); else OnImageSuccess(tex);
		}

		private string ResolvePromptForChat()
		{
			var p = promptInput != null ? promptInput.text : null;
			if (string.IsNullOrWhiteSpace(p)) return "請用一行文字說明 PolarAI 是什麼?";
			return p.Trim();
		}

		private string ResolvePromptForImage()
		{
			var p = promptInput != null ? promptInput.text : null;
			if (string.IsNullOrWhiteSpace(p)) return "a cute blue slime with big eyes, studio light, 4k";
			return p.Trim();
		}

		private void OnChatSuccess(GrokChatResponse resp)
		{
			string text = "<no content>";
			if (resp != null && resp.choices != null && resp.choices.Count > 0 && resp.choices[0].message != null)
			{
				text = resp.choices[0].message.content;
			}
			if (outputText != null) outputText.text = text;
			Debug.Log("Grok Chat: " + text);
		}

		private void OnImageSuccess(Texture2D tex)
		{
			if (outputImage != null)
			{
				outputImage.texture = tex;
			}
			Debug.Log("Grok Image generated: " + (tex != null ? tex.width + "x" + tex.height : "null"));
		}

		private void OnError(string err)
		{
			Debug.LogError("Grok Error: " + err);
			if (outputText != null) outputText.text = "Error: " + err;
		}

		private void ShowLoading(string message)
		{
			if (loadingRoot != null) loadingRoot.SetActive(true);
			if (loadingText != null) loadingText.text = message;
		}

		private void HideLoading()
		{
			if (loadingRoot != null) loadingRoot.SetActive(false);
		}

		private void SetInteractable(bool value)
		{
			if (chatButton != null) chatButton.interactable = value;
			if (imageButton != null) imageButton.interactable = value;
			if (promptInput != null) promptInput.interactable = value;
		}
	}
}


