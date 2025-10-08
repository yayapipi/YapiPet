using System;
using System.Collections;
using System.Collections.Generic;
using PolarAI.Scripts.Core.Gemini.Model;
using UnityEngine;
using UnityEngine.UI;

namespace PolarAI.Scripts.Core.Gemini.Example
{
    public class GeminiExample : MonoBehaviour
    {
        [Header("Gemini Core Reference")] 
        public GeminiCore geminiCore;

        [Header("Chat Section")] 
        [SerializeField] private InputField promptInput;
        [SerializeField] private Text chatOutputText;
        [SerializeField] private ScrollRect chatScrollRect;

        [Header("Image Generation Section")] 
        [SerializeField] private InputField imagePromptInput;
        [SerializeField] private RawImage generatedImage;
        [SerializeField] private Text imageStatusText;

        [Header("Vision Section")] 
        [SerializeField] private List<RawImage> visionInputImages;
        [SerializeField] private InputField visionPromptInput;
        [SerializeField] private Text visionResultText;

        [Header("Image Edit Section")] 
        [SerializeField] private List<RawImage> editInputImages;
        [SerializeField] private InputField editInstructionInput;
        [SerializeField] private RawImage editResultImage;
        [SerializeField] private Text editStatusText;

        [Header("Buttons")] 
        [SerializeField] private Button chatButton;
        [SerializeField] private Button chatWithVisionButton;
        [SerializeField] private Button generateImageButton;
        [SerializeField] private Button editImagesButton;
        [SerializeField] private Button clearChatButton;

        // 聊天記錄
        private List<ContentReq> chatHistory = new List<ContentReq>();

        private void Start()
        {
            InitializeUI();
            BindButtonEvents();
        }

        private void InitializeUI()
        {
            if (promptInput) promptInput.text = "你好，我是Unity開發者，請簡單介紹一下Gemini AI。";
            if (imagePromptInput) imagePromptInput.text = "一隻可愛的卡通熊貓在竹林中玩耍";
            if (visionPromptInput) visionPromptInput.text = "請描述這張圖片的內容";
            if (editInstructionInput) editInstructionInput.text = "讓圖片更加鮮豔並添加夢幻效果";
        }

        private void BindButtonEvents()
        {
            if (chatButton) chatButton.onClick.AddListener(OnChat);
            if (chatWithVisionButton) chatWithVisionButton.onClick.AddListener(OnChatWithVision);
            if (generateImageButton) generateImageButton.onClick.AddListener(OnGenerateImage);
            if (editImagesButton) editImagesButton.onClick.AddListener(OnEditImages);
            if (clearChatButton) clearChatButton.onClick.AddListener(OnClearChat);
        }

        public void OnChat()
        {
            string prompt = promptInput ? promptInput.text : "";
            if (string.IsNullOrEmpty(prompt))
            {
                AppendChatMessage("系統", "請輸入聊天內容");
                return;
            }

            AppendChatMessage("用戶", prompt);
            StartCoroutine(ChatCoroutine(prompt));
        }

        private IEnumerator ChatCoroutine(string prompt)
        {
            if (!geminiCore)
            {
                AppendChatMessage("系統", "GeminiCore 未設置");
                yield break;
            }

            yield return StartCoroutine(geminiCore.Chat(prompt, OnChatResponse, null, chatHistory.ToArray()));
        }

        private void OnChatResponse(string response)
        {
            AppendChatMessage("Gemini", response);
            
            // 修正：使用 "model" 而不是 "assistant"
            chatHistory.Add(new ContentReq 
            { 
                role = "model",  // ✅ Gemini API 使用 "model"
                parts = new[] { PartReq.FromText(response) }
            });
        }

        // 帶視覺的聊天
        public void OnChatWithVision()
        {
            string prompt = visionPromptInput ? visionPromptInput.text : "";
            if (string.IsNullOrEmpty(prompt))
            {
                AppendChatMessage("系統", "請輸入視覺分析提示");
                return;
            }

            if (!HasVisionImages())
            {
                AppendChatMessage("系統", "請先在 Vision Input Images 中添加圖片");
                return;
            }

            AppendChatMessage("用戶", $"[帶圖片] {prompt}");
            StartCoroutine(ChatWithVisionCoroutine(prompt));
        }

        private IEnumerator ChatWithVisionCoroutine(string prompt)
        {
            if (!geminiCore)
            {
                AppendChatMessage("系統", "GeminiCore 未設置");
                yield break;
            }

            List<string> base64Images = geminiCore.ConvertRawImagesToBase64(visionInputImages);
            yield return StartCoroutine(geminiCore.Chat(prompt, OnVisionResponse, base64Images, chatHistory.ToArray()));
        }

        private void OnVisionResponse(string response)
        {
            AppendChatMessage("Gemini", response);
            if (visionResultText) visionResultText.text = response;
        }

        // 圖片生成
        public void OnGenerateImage()
        {
            string prompt = imagePromptInput ? imagePromptInput.text : "";
            if (string.IsNullOrEmpty(prompt))
            {
                SetImageStatus("請輸入圖片生成提示");
                return;
            }

            SetImageStatus("正在生成圖片...");
            StartCoroutine(GenerateImageCoroutine(prompt));
        }

        private IEnumerator GenerateImageCoroutine(string prompt)
        {
            if (!geminiCore)
            {
                SetImageStatus("GeminiCore 未設置");
                yield break;
            }

            yield return StartCoroutine(geminiCore.GenerateImage(prompt, OnImageGenerated, OnImagePreview));
        }

        private void OnImageGenerated(string status)
        {
            SetImageStatus(status);
        }

        private void OnImagePreview(Texture2D texture)
        {
            if (generatedImage && texture)
            {
                generatedImage.texture = texture;
                SetImageStatus($"圖片生成完成：{texture.width}x{texture.height}");
            }
        }

        // 圖片編輯
        public void OnEditImages()
        {
            string instruction = editInstructionInput ? editInstructionInput.text : "";
            if (string.IsNullOrEmpty(instruction))
            {
                SetEditStatus("請輸入編輯指令");
                return;
            }

            if (!HasEditImages())
            {
                SetEditStatus("請先在 Edit Input Images 中添加圖片");
                return;
            }

            SetEditStatus("正在編輯圖片...");
            StartCoroutine(EditImagesCoroutine(instruction));
        }

        private IEnumerator EditImagesCoroutine(string instruction)
        {
            if (!geminiCore)
            {
                SetEditStatus("GeminiCore 未設置");
                yield break;
            }

            List<string> base64Images = geminiCore.ConvertRawImagesToBase64(editInputImages);

            // 使用反射調用私有方法 EditImages
            var method = typeof(GeminiCore).GetMethod("EditImages",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (method != null)
            {
                yield return StartCoroutine((IEnumerator)method.Invoke(geminiCore,
                    new object[] { base64Images, instruction, (Action<Texture2D>)OnImageEdited }));
            }
            else
            {
                SetEditStatus("EditImages 方法不可用");
            }
        }

        private void OnImageEdited(Texture2D texture)
        {
            if (editResultImage && texture)
            {
                editResultImage.texture = texture;
                SetEditStatus($"圖片編輯完成：{texture.width}x{texture.height}");
            }
        }

        // 清除聊天記錄
        public void OnClearChat()
        {
            chatHistory.Clear();
            if (chatOutputText) chatOutputText.text = "";
            AppendChatMessage("系統", "聊天記錄已清除");
        }

        // 輔助方法
        private void AppendChatMessage(string sender, string message)
        {
            if (!chatOutputText) return;

            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            string formattedMessage = $"[{timestamp}] {sender}: {message}\n\n";

            chatOutputText.text += formattedMessage;

            // 自動滾動到底部
            if (chatScrollRect)
            {
                Canvas.ForceUpdateCanvases();
                chatScrollRect.verticalNormalizedPosition = 0f;
            }
        }

        private void SetImageStatus(string status)
        {
            if (imageStatusText) imageStatusText.text = status;
            Debug.Log($"[GeminiExample] Image: {status}");
        }

        private void SetEditStatus(string status)
        {
            if (editStatusText) editStatusText.text = status;
            Debug.Log($"[GeminiExample] Edit: {status}");
        }

        private bool HasVisionImages()
        {
            if (visionInputImages == null) return false;
            foreach (var img in visionInputImages)
            {
                if (img && img.texture) return true;
            }

            return false;
        }

        private bool HasEditImages()
        {
            if (editInputImages == null) return false;
            foreach (var img in editInputImages)
            {
                if (img && img.texture) return true;
            }

            return false;
        }

        // 公開方法供外部調用
        public void ChatWithMemory(string prompt)
        {
            StartCoroutine(ChatCoroutine(prompt));
        }

        public void EditImagesFromPaths(List<string> imagePaths, string instruction)
        {
            if (!geminiCore) return;

            List<string> base64Images = geminiCore.ConvertImagePathsToBase64(imagePaths);
            StartCoroutine(EditImagesFromBase64(base64Images, instruction));
        }

        private IEnumerator EditImagesFromBase64(List<string> base64Images, string instruction)
        {
            var method = typeof(GeminiCore).GetMethod("EditImages",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (method != null)
            {
                yield return StartCoroutine((IEnumerator)method.Invoke(geminiCore,
                    new object[] { base64Images, instruction, (Action<Texture2D>)OnImageEdited }));
            }
        }
    }
}