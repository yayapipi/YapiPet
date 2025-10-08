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

namespace PolarAI.Scripts.Core.Gemini
{
    public class GeminiCore : MonoBehaviour
    {
        public string apiKey = "YOUR_GEMINI_API_KEY";
        public string chatModel = "gemini-2.5-flash";
        public string imageModel = "gemini-2.5-flash-image-preview";
        public bool enableDebugLog = true;

        private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models";

        private static readonly JsonSerializerSettings JsonCfg = new() { NullValueHandling = NullValueHandling.Ignore };

        public void SetGeminiConfig(string apiKey, string chatModel, string imageModel, bool enableDebugLog)
        {
            this.apiKey = apiKey;
            this.chatModel = chatModel;
            this.imageModel = imageModel;
            this.enableDebugLog = enableDebugLog;
        }
        
        public IEnumerator Chat(string prompt, Action<string> onVisionDone, 
            List<string> base64ImgList = null, ContentReq[] contentReqs = null)
        {
            Debug.Log($"[Gemini Chat] {prompt}");

            string selectModel = chatModel;
            if (base64ImgList != null && base64ImgList.Count != 0)
            {
                selectModel = imageModel;
            }
            var url = $"{BaseUrl}/{selectModel}:generateContent";
            var parts = new List<PartReq>();
            
            parts.Add(PartReq.FromText(prompt));
            
            if (base64ImgList != null && base64ImgList.Count != 0)
            {
                foreach (var b64 in base64ImgList)
                {
                    parts.Add(PartReq.FromInlineData(new InlineDataReq { mime_type = "image/png", data = b64 }));
                }
            }

            var allContent = new List<ContentReq>();
            if (contentReqs != null && contentReqs.Length != 0)
            {
                allContent.AddRange(contentReqs);
            }
            
            allContent.Add(new ContentReq { role = "user", parts = parts.ToArray() });
            var body = new GenerateContentRequest
            {
                contents = allContent.ToArray()
            };
            

            var json = JsonConvert.SerializeObject(body, JsonCfg);
            if (enableDebugLog) Debug.Log($"[Gemini Describe Request] {json}");

            using var req = BuildRequest(url, json);
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[Chat] Error: {req.error}\n{req.downloadHandler.text}");
                yield break;
            }

            if (enableDebugLog) Debug.Log($"[Gemini Describe] {req.downloadHandler.text}");
            var resp = JsonConvert.DeserializeObject<GenContentResponse>(req.downloadHandler.text, JsonCfg);
            var text = ExtractFirstText(resp);
            onVisionDone?.Invoke(string.IsNullOrEmpty(text) ? "[Describe] (empty)" : text.Trim());
        }
        
        
        public IEnumerator GenerateImage(string prompt, Action<string> onImageDone, Action<Texture2D> onImagePreview)
        {
            Debug.Log($"[Gemini Generate Image] {prompt}");
            var url = $"{BaseUrl}/{imageModel}:generateContent";

            var reqBody = new GenerateContentRequest
            {
                contents = new[]
                {
                    new ContentReq
                    {
                        role = "user",
                        parts = new[] { PartReq.FromText(prompt) }
                    }
                }
            };

            var json = JsonConvert.SerializeObject(reqBody, JsonCfg);
            using var req = BuildRequest(url, json);
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                onImageDone?.Invoke($"[Image] Error: {req.error}\n{req.downloadHandler.text}");
                yield break;
            }

            if (enableDebugLog) Debug.Log($"[Gemini RAW Image] {req.downloadHandler.text}");
            var resp = JsonConvert.DeserializeObject<GenContentResponse>(req.downloadHandler.text, JsonCfg);
            var imgB64 = ExtractFirstImageBase64(resp, out var mime);

            if (string.IsNullOrEmpty(imgB64))
            {
                var txt = ExtractFirstText(resp);
                onImageDone?.Invoke(string.IsNullOrEmpty(txt)
                    ? "[Image] No image returned."
                    : $"[Image/Text-only] {txt}");
                yield break;
            }

            var tex = DecodeBase64ToTexture(imgB64);
            if (tex == null)
            {
                onImageDone?.Invoke("[Image] Failed to decode image.");
                yield break;
            }

            onImagePreview?.Invoke(tex);
            onImageDone?.Invoke($"Done ({mime}), {tex.width}x{tex.height}");
        }

       

        private IEnumerator EditImages(List<string> base64ImgList, string instruction, Action<Texture2D> onImageReturn)
        {
            Debug.Log($"[Gemini Edit Images] {instruction}");

            if (base64ImgList == null || base64ImgList.Count == 0)
            {
                Debug.LogWarning("[Edit Images] No input images.");
                yield break;
            }

            var url = $"{BaseUrl}/{imageModel}:generateContent";
            var parts = new List<PartReq>();
            foreach (var b64 in base64ImgList)
            {
                parts.Add(PartReq.FromInlineData(new InlineDataReq { mime_type = "image/png", data = b64 }));
            }

            parts.Add(
                PartReq.FromText(string.IsNullOrEmpty(instruction) ? "Blend these images harmoniously." : instruction));

            var body = new GenerateContentRequest
            {
                contents = new[] { new ContentReq { role = "user", parts = parts.ToArray() } }
            };

            var json = JsonConvert.SerializeObject(body, JsonCfg);
            if (enableDebugLog) Debug.Log($"[Gemini Multi-Edit Request] {json}");

            using var req = BuildRequest(url, json);
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[Edit Images] Error: {req.error}\n{req.downloadHandler.text}");
                yield break;
            }

            if (enableDebugLog) Debug.Log($"[Gemini RAW Multi-Edit] {req.downloadHandler.text}");
            var resp = JsonConvert.DeserializeObject<GenContentResponse>(req.downloadHandler.text, JsonCfg);
            var imgB64 = ExtractFirstImageBase64(resp, out var mime);

            if (string.IsNullOrEmpty(imgB64))
            {
                var txt = ExtractFirstText(resp);
                Debug.LogWarning($"[Edit Images] No image returned.\n{txt}");
                yield break;
            }

            var tex = DecodeBase64ToTexture(imgB64);
            if (tex == null)
            {
                Debug.LogWarning("[Edit Images] Failed to decode image.");
                yield break;
            }

            onImageReturn?.Invoke(tex);
            Debug.Log($"[Edit Images] Done ({mime}), {tex.width}x{tex.height}");
        }

        public List<string> ConvertRawImagesToBase64(List<RawImage> rawImages)
        {
            var list = new List<string>();

            foreach (var raw in rawImages)
            {
                if (raw && raw.texture)
                {
                    var b64 = TextureToBase64PNG(ConvertToReadableTexture(raw.texture));
                    if (!string.IsNullOrEmpty(b64)) list.Add(b64);
                }
            }

            return list;
        }

        public List<string> ConvertImagePathsToBase64(List<string> imagePaths)
        {
            var list = new List<string>();
            foreach (var p in imagePaths)
            {
                var b64 = PathImageToBase64(p);
                if (!string.IsNullOrEmpty(b64)) list.Add(b64);
            }

            return list;
        }


        private UnityWebRequest BuildRequest(string url, string jsonBody)
        {
            var req = new UnityWebRequest(url, "POST");
            var bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("x-goog-api-key", apiKey);
            if (enableDebugLog) Debug.Log($"[Gemini Request] {url}\n{jsonBody}");
            return req;
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