using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using PolarAI.AICore.XAI.Grok.Example;

namespace PolarAI.AICore.XAI.Grok.Editor
{
	public static class CreateGrokExampleScene
	{
		[MenuItem("PolarAI/Grok/Create Example Scene", priority = 10)]
		public static void CreateScene()
		{
			var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
			scene.name = "GrokExample";

			// Canvas
			var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
			var canvas = canvasGo.GetComponent<Canvas>();
			canvas.renderMode = RenderMode.ScreenSpaceOverlay;
			// Editor 腳本不可使用 DontDestroyOnLoad

			// Text (Legacy)
			var textGo = new GameObject("OutputText", typeof(Text));
			textGo.transform.SetParent(canvasGo.transform, false);
			var text = textGo.GetComponent<Text>();
			text.text = "Grok output will appear here";
			text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
			text.color = Color.white;
			var textRect = textGo.GetComponent<RectTransform>();
			textRect.anchorMin = new Vector2(0.5f, 1f);
			textRect.anchorMax = new Vector2(0.5f, 1f);
			textRect.pivot = new Vector2(0.5f, 1f);
			textRect.anchoredPosition = new Vector2(0, -40);
			textRect.sizeDelta = new Vector2(800, 100);

			// InputField (Legacy)
			var inputGo = new GameObject("PromptInput", typeof(Image), typeof(InputField));
			inputGo.transform.SetParent(canvasGo.transform, false);
			var inputRect = inputGo.GetComponent<RectTransform>();
			inputRect.anchorMin = new Vector2(0.5f, 1f);
			inputRect.anchorMax = new Vector2(0.5f, 1f);
			inputRect.pivot = new Vector2(0.5f, 1f);
			inputRect.anchoredPosition = new Vector2(0, -150);
			inputRect.sizeDelta = new Vector2(800, 40);
			var inputImage = inputGo.GetComponent<Image>();
			inputImage.color = new Color(0f, 0f, 0f, 0.4f);
			var input = inputGo.GetComponent<InputField>();

			// Placeholder/Text for InputField
			var placeholderGo = new GameObject("Placeholder", typeof(Text));
			placeholderGo.transform.SetParent(inputGo.transform, false);
			var placeholder = placeholderGo.GetComponent<Text>();
			placeholder.text = "輸入 Prompt (按下 Chat 或 Image 測試)";
			placeholder.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
			placeholder.color = new Color(1f, 1f, 1f, 0.5f);
			placeholder.alignment = TextAnchor.MiddleLeft;
			var placeholderRect = placeholderGo.GetComponent<RectTransform>();
			placeholderRect.anchorMin = Vector2.zero;
			placeholderRect.anchorMax = Vector2.one;
			placeholderRect.offsetMin = new Vector2(10, 6);
			placeholderRect.offsetMax = new Vector2(-10, -7);

			var textInputGo = new GameObject("Text", typeof(Text));
			textInputGo.transform.SetParent(inputGo.transform, false);
			var textInput = textInputGo.GetComponent<Text>();
			textInput.text = string.Empty;
			textInput.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
			textInput.color = Color.white;
			textInput.alignment = TextAnchor.MiddleLeft;
			var textInputRect = textInputGo.GetComponent<RectTransform>();
			textInputRect.anchorMin = Vector2.zero;
			textInputRect.anchorMax = Vector2.one;
			textInputRect.offsetMin = new Vector2(10, 6);
			textInputRect.offsetMax = new Vector2(-10, -7);

			input.textComponent = textInput;
			input.placeholder = placeholder;

			// RawImage
			var imageGo = new GameObject("OutputImage", typeof(RawImage));
			imageGo.transform.SetParent(canvasGo.transform, false);
			var rawImage = imageGo.GetComponent<RawImage>();
			rawImage.color = Color.white;
			var imgRect = imageGo.GetComponent<RectTransform>();
			imgRect.anchorMin = new Vector2(0.5f, 0.5f);
			imgRect.anchorMax = new Vector2(0.5f, 0.5f);
			imgRect.pivot = new Vector2(0.5f, 0.5f);
			imgRect.anchoredPosition = new Vector2(0, -80);
			imgRect.sizeDelta = new Vector2(512, 512);

			// Controller
			var ctrlGo = new GameObject("GrokExampleController");
			var controller = ctrlGo.AddComponent<GrokExampleUGUI>();
			controller.outputText = text;
			controller.outputImage = rawImage;
			controller.promptInput = input;

			// Buttons
			var chatBtn = CreateButton(canvasGo.transform, new Vector2(-120, -200), "Run Chat");
			var imgBtn = CreateButton(canvasGo.transform, new Vector2(120, -200), "Run Image");

			// 綁定按鈕事件（Persistent）
			UnityEventTools.AddPersistentListener(chatBtn.onClick, controller.RunChat);
			UnityEventTools.AddPersistentListener(imgBtn.onClick, controller.RunImage);

			// 指派到 controller 方便互斥控制
			controller.chatButton = chatBtn;
			controller.imageButton = imgBtn;

			// Loading 面板
			var loadingGo = new GameObject("Loading", typeof(Image));
			loadingGo.transform.SetParent(canvasGo.transform, false);
			var loadingRect = loadingGo.GetComponent<RectTransform>();
			loadingRect.anchorMin = Vector2.zero;
			loadingRect.anchorMax = Vector2.one;
			loadingRect.offsetMin = Vector2.zero;
			loadingRect.offsetMax = Vector2.zero;
			var loadingBg = loadingGo.GetComponent<Image>();
			loadingBg.color = new Color(0f, 0f, 0f, 0.4f);
			var loadingTextGo = new GameObject("Text", typeof(Text));
			loadingTextGo.transform.SetParent(loadingGo.transform, false);
			var ltxt = loadingTextGo.GetComponent<Text>();
			ltxt.text = "Loading...";
			ltxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
			ltxt.color = Color.white;
			ltxt.alignment = TextAnchor.MiddleCenter;
			var ltxtRect = loadingTextGo.GetComponent<RectTransform>();
			ltxtRect.anchorMin = Vector2.zero;
			ltxtRect.anchorMax = Vector2.one;
			ltxtRect.offsetMin = Vector2.zero;
			ltxtRect.offsetMax = Vector2.zero;
			loadingGo.SetActive(false);
			controller.loadingRoot = loadingGo;
			controller.loadingText = ltxt;

			// Save scene
			string scenePath = EditorUtility.SaveFilePanelInProject("Save Scene", "GrokExample", "unity", "Choose a location to save the Grok example scene.");
			if (!string.IsNullOrEmpty(scenePath))
			{
				EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), scenePath);
			}

			Selection.activeObject = ctrlGo;
		}

		private static Button CreateButton(Transform parent, Vector2 anchoredPos, string label)
		{
			var btnGo = new GameObject(label + "Button", typeof(Image), typeof(Button));
			btnGo.transform.SetParent(parent, false);
			var img = btnGo.GetComponent<Image>();
			img.color = new Color(0.2f, 0.5f, 1f, 0.9f);
			var rect = btnGo.GetComponent<RectTransform>();
			rect.anchorMin = new Vector2(0.5f, 1f);
			rect.anchorMax = new Vector2(0.5f, 1f);
			rect.pivot = new Vector2(0.5f, 1f);
			rect.anchoredPosition = new Vector2(anchoredPos.x, anchoredPos.y);
			rect.sizeDelta = new Vector2(220, 40);

			var textGo = new GameObject("Text", typeof(Text));
			textGo.transform.SetParent(btnGo.transform, false);
			var text = textGo.GetComponent<Text>();
			text.text = label;
			text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
			text.color = Color.white;
			text.alignment = TextAnchor.MiddleCenter;
			var textRect = textGo.GetComponent<RectTransform>();
			textRect.anchorMin = Vector2.zero;
			textRect.anchorMax = Vector2.one;
			textRect.offsetMin = Vector2.zero;
			textRect.offsetMax = Vector2.zero;

			return btnGo.GetComponent<Button>();
		}
	}
}


