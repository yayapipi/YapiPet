using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using PolarAI.Scripts.Core.Gemini.Model;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace PolarAI.Scripts.Core.Gemini.Example
{
    public class GeminiUnity : MonoBehaviour
    {
        // ================= Config =================
        [Header("Config")] [SerializeField] private string apiKey = "YOUR_GEMINI_API_KEY";
        [SerializeField] private string chatModel = "gemini-2.5-flash"; // 對話
        [SerializeField] private string imageModel = "gemini-2.5-flash-image-preview"; // 圖像（Nano Banana）
        [SerializeField] private bool enableDebugLog = true;

        private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models";

        private static readonly JsonSerializerSettings JsonCfg = new JsonSerializerSettings
            { NullValueHandling = NullValueHandling.Ignore };

        // ================= UI: Chat =================
        [Header("UI (Chat)")] [SerializeField] private InputField chatInput;
        [SerializeField] private Text chatOutput;

        // ================= UI: Image Generate =================
        [Header("UI (Image Generate)")] [SerializeField]
        private InputField imagePromptInput;

        [SerializeField] private RawImage imagePreview; // 用來顯示生成/編修後的結果
        [SerializeField] private Text imageStatus;

        // ================= UI: Image Edit (Single) =================
        [Header("UI (Image Edit - Single)")] [SerializeField]
        private InputField imageEditPromptInput;

        [SerializeField] private Text imageEditStatus;

        // ================= UI: Vision Describe & Multi-Image Edit =================
        public enum ImageInputMode
        {
            RawImages,
            Paths
        }

        [Header("Image Input Source (for Describe / Multi-Edit)")] [SerializeField]
        private ImageInputMode inputMode = ImageInputMode.RawImages;

        [Tooltip("當 InputMode = RawImages 時使用。可放多張 RawImage 當輸入來源")] [SerializeField]
        private RawImage[] inputRawImages;

        [Tooltip("當 InputMode = Paths 時使用。可放多個圖片檔案路徑")] [SerializeField]
        private string[] inputImagePaths;

        [Header("UI (Vision Describe)")] [SerializeField]
        private InputField visionPromptInput; // 可選：覆寫描述指令

        [SerializeField] private Text visionOutput;

        [Header("UI (Multi Image Edit)")] [SerializeField]
        private InputField multiEditPromptInput; // 多張編修的指令

        [SerializeField] private Text multiEditStatus;

        // ================= Buttons (optional wire) =================
        [Header("Buttons (optional)")] public Button chatButton;
        public Button imageButton;
        public Button imageEditButton;
        public Button visionDescribeButton;
        public Button multiImageEditButton;

        private void Start()
        {
            if (chatButton) chatButton.onClick.AddListener(OnChatButton);
            if (imageButton) imageButton.onClick.AddListener(OnImageButton);
            if (imageEditButton) imageEditButton.onClick.AddListener(OnImageEditButton);
            if (visionDescribeButton) visionDescribeButton.onClick.AddListener(OnVisionDescribeButton);
            if (multiImageEditButton) multiImageEditButton.onClick.AddListener(OnMultiImageEditButton);
        }

        // ================= Buttons =================
        public void OnChatButton()
        {
            var userText = string.IsNullOrWhiteSpace(chatInput?.text) ? "Say hi in one sentence." : chatInput.text;
            StartCoroutine(ChatOnce(userText));
        }

        public void OnImageButton()
        {
            var prompt = string.IsNullOrWhiteSpace(imagePromptInput?.text)
                ? "Create a picture of a nano banana dish in a fancy restaurant with a Gemini theme"
                : imagePromptInput.text;
            StartCoroutine(GenerateImage(prompt));
        }

        public void OnImageEditButton()
        {
            var instruction = string.IsNullOrWhiteSpace(imageEditPromptInput?.text)
                ? "Make it neon-cyberpunk poster style with soft glow."
                : imageEditPromptInput.text;

            var srcTex = imagePreview && imagePreview.texture ? imagePreview.texture as Texture2D : null;
            StartCoroutine(EditImageSingle(srcTex, instruction));
        }

        public void OnVisionDescribeButton()
        {
            // 若有自定義描述 prompt 就帶入；否則給一個穩健的預設
            var prompt = string.IsNullOrWhiteSpace(visionPromptInput?.text)
                ? "For each image, describe concisely in Traditional Chinese what you see. One bullet per image."
                : visionPromptInput.text;

            StartCoroutine(DescribeImages(prompt));
        }

        public void OnMultiImageEditButton()
        {
            var instruction = string.IsNullOrWhiteSpace(multiEditPromptInput?.text)
                ? "Blend these images into a cohesive stylized poster. Unify colors and add a soft cinematic tone."
                : multiEditPromptInput.text;

            StartCoroutine(EditImagesMulti(instruction));
        }

        // ================= Core: Chat =================
        private IEnumerator ChatOnce(string userText)
        {
            var url = $"{BaseUrl}/{chatModel}:generateContent";

            var reqBody = new GenerateContentRequest
            {
                contents = new[]
                {
                    new ContentReq
                    {
                        role = "user",
                        parts = new PartReq[] { PartReq.FromText(userText) }
                    }
                },
                generation_config = new GenerationConfig { temperature = 0.7f, top_p = 0.95f }
            };

            string json = JsonConvert.SerializeObject(reqBody, JsonCfg);
            using (var req = BuildRequest(url, json))
            {
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    SafeSetText(chatOutput, $"[Chat] Error: {req.error}\n{req.downloadHandler.text}");
                    yield break;
                }

                if (enableDebugLog) Debug.Log($"[Gemini RAW Chat] {req.downloadHandler.text}");
                var resp = JsonConvert.DeserializeObject<GenContentResponse>(req.downloadHandler.text, JsonCfg);
                var text = ExtractFirstText(resp);
                SafeSetText(chatOutput, string.IsNullOrEmpty(text) ? "[Chat] (empty)" : text.Trim());
            }
        }

        // ================= Core: Image Generate =================
        private IEnumerator GenerateImage(string prompt)
        {
            SafeSetText(imageStatus, "Generating...");
            var url = $"{BaseUrl}/{imageModel}:generateContent";

            var reqBody = new GenerateContentRequest
            {
                contents = new[]
                {
                    new ContentReq
                    {
                        role = "user",
                        parts = new PartReq[] { PartReq.FromText(prompt) }
                    }
                }
            };

            string json = JsonConvert.SerializeObject(reqBody, JsonCfg);
            using (var req = BuildRequest(url, json))
            {
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    SafeSetText(imageStatus, $"[Image] Error: {req.error}\n{req.downloadHandler.text}");
                    yield break;
                }

                if (enableDebugLog) Debug.Log($"[Gemini RAW Image] {req.downloadHandler.text}");
                var resp = JsonConvert.DeserializeObject<GenContentResponse>(req.downloadHandler.text, JsonCfg);
                var imgB64 = ExtractFirstImageBase64(resp, out var mime);

                if (string.IsNullOrEmpty(imgB64))
                {
                    var txt = ExtractFirstText(resp);
                    SafeSetText(imageStatus,
                        string.IsNullOrEmpty(txt) ? "[Image] No image returned." : $"[Image/Text-only] {txt}");
                    yield break;
                }

                var tex = DecodeBase64ToTexture(imgB64);
                if (tex == null)
                {
                    SafeSetText(imageStatus, "[Image] Failed to decode image.");
                    yield break;
                }

                if (imagePreview) imagePreview.texture = tex;
                SafeSetText(imageStatus, $"Done ({mime}), {tex.width}x{tex.height}");
            }
        }

        // ================= Core: Image Edit (Single) =================
        private IEnumerator EditImageSingle(Texture2D sourceTex, string instruction)
        {
            if (imageEditStatus) imageEditStatus.text = "Editing (single)...";
            if (sourceTex == null && imagePreview && imagePreview.texture)
                sourceTex = imagePreview.texture as Texture2D;

            if (sourceTex == null)
            {
                if (imageEditStatus) imageEditStatus.text = "[Edit] No source image.";
                yield break;
            }

            string b64 = TextureToBase64PNG(sourceTex);
            if (b64 == null)
            {
                if (imageEditStatus) imageEditStatus.text = "[Edit] Texture not readable.";
                yield break;
            }

            var url = $"{BaseUrl}/{imageModel}:generateContent";
            var body = new GenerateContentRequest
            {
                contents = new[]
                {
                    new ContentReq
                    {
                        role = "user",
                        parts = new PartReq[]
                        {
                            PartReq.FromInlineData(new InlineDataReq { mime_type = "image/png", data = b64 }),
                            PartReq.FromText(instruction ?? string.Empty)
                        }
                    }
                }
            };

            string json = JsonConvert.SerializeObject(body, JsonCfg);
            if (enableDebugLog) Debug.Log($"[Gemini Edit Single Request] {json}");

            using (var req = BuildRequest(url, json))
            {
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    if (imageEditStatus) imageEditStatus.text = $"[Edit] Error: {req.error}\n{req.downloadHandler.text}";
                    yield break;
                }

                if (enableDebugLog) Debug.Log($"[Gemini RAW Edit Single] {req.downloadHandler.text}");
                var resp = JsonConvert.DeserializeObject<GenContentResponse>(req.downloadHandler.text, JsonCfg);
                var imgB64 = ExtractFirstImageBase64(resp, out var mime);

                if (string.IsNullOrEmpty(imgB64))
                {
                    var txt = ExtractFirstText(resp);
                    if (imageEditStatus)
                        imageEditStatus.text = string.IsNullOrEmpty(txt)
                            ? "[Edit] No image returned."
                            : $"[Edit/Text-only] {txt}";
                    yield break;
                }

                var tex = DecodeBase64ToTexture(imgB64);
                if (tex == null)
                {
                    if (imageEditStatus) imageEditStatus.text = "[Edit] Failed to decode image.";
                    yield break;
                }

                if (imagePreview) imagePreview.texture = tex;
                if (imageEditStatus) imageEditStatus.text = $"Edited ({mime}), {tex.width}x{tex.height}";
            }
        }

        // ================= Core: Vision Describe (Multi-Image OK) =================
        private IEnumerator DescribeImages(string describePrompt)
        {
            SafeSetText(visionOutput, "Describing images...");

            var base64List = GatherInputImagesBase64();
            if (base64List == null || base64List.Count == 0)
            {
                SafeSetText(visionOutput, "[Describe] No input images. Check input mode & sources.");
                yield break;
            }

            var url = $"{BaseUrl}/{imageModel}:generateContent";
            var parts = new List<PartReq>();
            foreach (var b64 in base64List)
                parts.Add(PartReq.FromInlineData(new InlineDataReq { mime_type = "image/png", data = b64 }));
            parts.Add(PartReq.FromText(string.IsNullOrEmpty(describePrompt)
                ? "Describe what you see in each image, one bullet per image, short and clear, in Traditional Chinese."
                : describePrompt));

            var body = new GenerateContentRequest
            {
                contents = new[] { new ContentReq { role = "user", parts = parts.ToArray() } }
            };

            string json = JsonConvert.SerializeObject(body, JsonCfg);
            if (enableDebugLog) Debug.Log($"[Gemini Describe Request] {json}");

            using (var req = BuildRequest(url, json))
            {
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    SafeSetText(visionOutput, $"[Describe] Error: {req.error}\n{req.downloadHandler.text}");
                    yield break;
                }

                if (enableDebugLog) Debug.Log($"[Gemini RAW Describe] {req.downloadHandler.text}");
                var resp = JsonConvert.DeserializeObject<GenContentResponse>(req.downloadHandler.text, JsonCfg);
                var text = ExtractFirstText(resp);
                SafeSetText(visionOutput, string.IsNullOrEmpty(text) ? "[Describe] (empty)" : text.Trim());
            }
        }

        // ================= Core: Multi-Image Edit =================
        private IEnumerator EditImagesMulti(string instruction)
        {
            if (multiEditStatus) multiEditStatus.text = "Editing (multi)...";

            var base64List = GatherInputImagesBase64();
            if (base64List == null || base64List.Count == 0)
            {
                if (multiEditStatus) multiEditStatus.text = "[Multi-Edit] No input images.";
                yield break;
            }

            var url = $"{BaseUrl}/{imageModel}:generateContent";
            var parts = new List<PartReq>();
            foreach (var b64 in base64List)
                parts.Add(PartReq.FromInlineData(new InlineDataReq { mime_type = "image/png", data = b64 }));
            parts.Add(
                PartReq.FromText(string.IsNullOrEmpty(instruction) ? "Blend these images harmoniously." : instruction));

            var body = new GenerateContentRequest
            {
                contents = new[] { new ContentReq { role = "user", parts = parts.ToArray() } }
            };

            string json = JsonConvert.SerializeObject(body, JsonCfg);
            if (enableDebugLog) Debug.Log($"[Gemini Multi-Edit Request] {json}");

            using (var req = BuildRequest(url, json))
            {
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    if (multiEditStatus)
                        multiEditStatus.text = $"[Multi-Edit] Error: {req.error}\n{req.downloadHandler.text}";
                    yield break;
                }

                if (enableDebugLog) Debug.Log($"[Gemini RAW Multi-Edit] {req.downloadHandler.text}");
                var resp = JsonConvert.DeserializeObject<GenContentResponse>(req.downloadHandler.text, JsonCfg);
                var imgB64 = ExtractFirstImageBase64(resp, out var mime);

                if (string.IsNullOrEmpty(imgB64))
                {
                    var txt = ExtractFirstText(resp);
                    if (multiEditStatus)
                        multiEditStatus.text = string.IsNullOrEmpty(txt)
                            ? "[Multi-Edit] No image returned."
                            : $"[Multi-Edit/Text-only] {txt}";
                    yield break;
                }

                var tex = DecodeBase64ToTexture(imgB64);
                if (tex == null)
                {
                    if (multiEditStatus) multiEditStatus.text = "[Multi-Edit] Failed to decode image.";
                    yield break;
                }

                if (imagePreview) imagePreview.texture = tex;
                if (multiEditStatus) multiEditStatus.text = $"Edited ({mime}), {tex.width}x{tex.height}";
            }
        }

        // ================= Input Gathering =================
        private List<string> GatherInputImagesBase64()
        {
            var list = new List<string>();

            if (inputMode == ImageInputMode.RawImages)
            {
                if (inputRawImages != null)
                {
                    foreach (var raw in inputRawImages)
                    {
                        if (raw && raw.texture)
                        {
                            var b64 = TextureToBase64PNG(ConvertToReadableTexture(raw.texture));
                            if (!string.IsNullOrEmpty(b64)) list.Add(b64);
                        }
                    }
                }
            }
            else // Paths
            {
                if (inputImagePaths != null)
                {
                    foreach (var p in inputImagePaths)
                    {
                        var b64 = PathImageToBase64(p);
                        if (!string.IsNullOrEmpty(b64)) list.Add(b64);
                    }
                }
            }

            return list;
        }

        // ================= HTTP & Utils =================
        private UnityWebRequest BuildRequest(string url, string jsonBody)
        {
            var req = new UnityWebRequest(url, "POST");
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("x-goog-api-key", apiKey);
            if (enableDebugLog) Debug.Log($"[Gemini Request] {url}\n{jsonBody}");
            return req;
        }

        private void SafeSetText(Text t, string s)
        {
            if (t) t.text = s;
        }

        private Texture2D DecodeBase64ToTexture(string base64)
        {
            try
            {
                byte[] bytes = Convert.FromBase64String(base64);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                return tex.LoadImage(bytes) ? tex : null;
            }
            catch
            {
                return null;
            }
        }

        private string TextureToBase64PNG(Texture2D t2d)
        {
            try
            {
                var bytes = t2d.EncodeToPNG();
                return Convert.ToBase64String(bytes);
            }
            catch
            {
                // 若來源不可讀，嘗試轉可讀
                var readable = ConvertToReadableTexture(t2d);
                if (readable == null) return null;
                try
                {
                    return Convert.ToBase64String(readable.EncodeToPNG());
                }
                catch
                {
                    return null;
                }
            }
        }

        private string TextureToBase64PNG(Texture t)
        {
            var readable = ConvertToReadableTexture(t);
            if (readable == null) return null;
            try
            {
                return Convert.ToBase64String(readable.EncodeToPNG());
            }
            catch
            {
                return null;
            }
        }

        private string PathImageToBase64(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
                var bytes = File.ReadAllBytes(path);
                return Convert.ToBase64String(bytes);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 將任意 Texture 轉為可讀的 Texture2D（避免 Read/Write 限制）
        /// </summary>
        private Texture2D ConvertToReadableTexture(Texture src)
        {
            if (src == null) return null;

            int w = src.width;
            int h = src.height;

            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var prev = RenderTexture.active;

            try
            {
                Graphics.Blit(src, rt);
                RenderTexture.active = rt;

                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                return tex;
            }
            catch
            {
                return null;
            }
            finally
            {
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        private static string ExtractFirstText(GenContentResponse r)
        {
            if (r?.candidates == null) return null;
            foreach (var c in r.candidates)
            {
                if (c?.content?.parts == null) continue;
                foreach (var p in c.content.parts)
                {
                    if (!string.IsNullOrEmpty(p.text)) return p.text;
                }
            }

            return null;
        }

        private static string ExtractFirstImageBase64(GenContentResponse r, out string mime)
        {
            mime = null;
            if (r?.candidates == null) return null;
            foreach (var c in r.candidates)
            {
                var parts = c?.content?.parts;
                if (parts == null) continue;
                foreach (var p in parts)
                {
                    var id = p.inlineData;
                    if (id != null && !string.IsNullOrEmpty(id.data))
                    {
                        mime = id.mimeType;
                        return id.data;
                    }
                }
            }

            return null;
        }
    }
}