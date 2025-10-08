using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Net.WebSockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using PolarAI.Scripts.Core.ComfyUI.Model;
using UnityEngine;
using UnityEngine.Networking;

namespace PolarAI.Scripts.Core.ComfyUI
{
    public class ComfyUICore : MonoBehaviour
    {
        [Header("ComfyUI Local Server IP")] 
        public string baseUrl = "http://127.0.0.1:8188";
        public float pollIntervalSeconds = 1.0f;
        public float maxWaitSeconds = 180f;

        [Header("Progress Report Setting")] public string clientId = "UnityClient";
        public bool useWebSocketProgress = true;
        private bool _cancelRequested;

        public void CancelCurrent()
        {
            _cancelRequested = true;
        }

        public IEnumerator GenerateImageCoroutine(ComfyUIRequest request,
            Action<Texture2D> onSuccess,
            Action<string> onError,
            Action<float, string> onProgress = null)
        {
            _cancelRequested = false;

            var promptJson = BuildMinimalText2ImgWorkflowJson(request);
            var body = "{\"prompt\":" + promptJson + ",\"client_id\":\"" + EscapeJson(clientId) + "\"}";
            using var req = new UnityWebRequest($"{baseUrl}/prompt", "POST");

            var raw = Encoding.UTF8.GetBytes(body);
            req.uploadHandler = new UploadHandlerRaw(raw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            onProgress?.Invoke(0f, "提交工作中...");
            yield return req.SendWebRequest();

            if (_cancelRequested)
            {
                onError?.Invoke("已取消。");
                yield break;
            }

            if (req.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke($"POST /prompt 失敗: {req.error}");
                yield break;
            }

            PromptIdResponse resp = null;
            try
            {
                resp = JsonUtility.FromJson<PromptIdResponse>(req.downloadHandler.text);
            }
            catch (Exception ex)
            {
                onError?.Invoke($"解析 prompt_id 失敗: {ex.Message} 原始回應: {req.downloadHandler.text}");
                yield break;
            }

            if (resp == null || string.IsNullOrEmpty(resp.prompt_id))
            {
                onError?.Invoke($"回應未包含 prompt_id。原始回應: {req.downloadHandler.text}");
                yield break;
            }

            bool finished = false;
            Coroutine wsCoro = null;
            if (useWebSocketProgress && onProgress != null)
            {
                wsCoro = StartCoroutine(ListenProgressWs(
                    clientId,
                    resp.prompt_id,
                    (p, msg) =>
                    {
                        if (!_cancelRequested) onProgress?.Invoke(p, msg);
                    },
                    () => finished || _cancelRequested
                ));
            }
            else
            {
                onProgress?.Invoke(0f, "已送出工作，等待排隊/執行...");
            }

            string filename = null;
            string subfolder = "";
            string type = "output";
            float waited = 0f;

            while (waited < maxWaitSeconds)
            {
                if (_cancelRequested)
                {
                    finished = true;
                    if (wsCoro != null) StopCoroutine(wsCoro);
                    onError?.Invoke("已取消。");
                    yield break;
                }

                using (var histReq = UnityWebRequest.Get($"{baseUrl}/history/{resp.prompt_id}"))
                {
                    yield return histReq.SendWebRequest();

                    if (_cancelRequested)
                    {
                        finished = true;
                        if (wsCoro != null) StopCoroutine(wsCoro);
                        onError?.Invoke("已取消。");
                        yield break;
                    }

                    if (histReq.result == UnityWebRequest.Result.Success)
                    {
                        var json = histReq.downloadHandler.text;
                        if (TryExtractFirstImageInfo(json, out filename, out subfolder, out type))
                        {
                            break;
                        }
                    }
                }

                yield return new WaitForSecondsRealtime(pollIntervalSeconds);
                waited += pollIntervalSeconds;

                // 若未啟用 WS，提供簡單的時間型進度感（最多 10%）
                if (!useWebSocketProgress && onProgress != null)
                {
                    float pseudo = Mathf.Clamp01(waited / Mathf.Max(1f, maxWaitSeconds)) * 0.1f;
                    onProgress?.Invoke(pseudo, "等待結果中...");
                }
            }

            finished = true; // 通知 WS 協程結束
            if (wsCoro != null) StopCoroutine(wsCoro);

            if (string.IsNullOrEmpty(filename))
            {
                onError?.Invoke("等待逾時或未取得輸出圖片。");
                yield break;
            }

            onProgress?.Invoke(1f, "生成完成，下載圖片中...");
            string viewUrl = $"{baseUrl}/view?filename={UnityWebRequest.EscapeURL(filename)}" +
                             $"&subfolder={UnityWebRequest.EscapeURL(subfolder ?? string.Empty)}" +
                             $"&type={UnityWebRequest.EscapeURL(type ?? "output")}";

            using (var texReq = UnityWebRequestTexture.GetTexture(viewUrl))
            {
                yield return texReq.SendWebRequest();

                if (texReq.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke($"下載圖片失敗: {texReq.error}");
                    yield break;
                }

                var tex = DownloadHandlerTexture.GetContent(texReq);
                onSuccess?.Invoke(tex);
            }
        }

        private IEnumerator ListenProgressWs(string cId, string targetPromptId,
            Action<float, string> onProgress, Func<bool> shouldStop)
        {
            Uri wsUri;
            try
            {
                var wsBase = baseUrl.StartsWith("https", StringComparison.OrdinalIgnoreCase)
                    ? baseUrl.Replace("https", "wss")
                    : baseUrl.Replace("http", "ws");
                wsUri = new Uri($"{wsBase}/ws?clientId={UnityWebRequest.EscapeURL(cId)}");
            }
            catch
            {
                yield break;
            }

            using var ws = new ClientWebSocket();
            var connectTask = ws.ConnectAsync(wsUri, CancellationToken.None);
            while (!connectTask.IsCompleted)
            {
                if (shouldStop()) yield break;
                yield return null;
            }

            if (ws.State != WebSocketState.Open) yield break;

            var buffer = new ArraySegment<byte>(new byte[8192]);
            var sb = new StringBuilder();

            while (!shouldStop())
            {
                sb.Length = 0;
                WebSocketReceiveResult result = null;

                do
                {
                    var recvTask = ws.ReceiveAsync(buffer, CancellationToken.None);
                    while (!recvTask.IsCompleted)
                    {
                        if (shouldStop()) yield break;
                        yield return null;
                    }

                    result = recvTask.Result;

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        // 嘗試關閉
                        var closeTask = ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "close",
                            CancellationToken.None);
                        while (!closeTask.IsCompleted) yield return null;
                        yield break;
                    }

                    var chunk = Encoding.UTF8.GetString(buffer.Array, 0, result.Count);
                    sb.Append(chunk);
                } while (!result.EndOfMessage);

                var msg = sb.ToString();
                WsEnvelope env = null;
                try
                {
                    env = JsonUtility.FromJson<WsEnvelope>(msg);
                }
                catch
                {
                    env = null;
                }

                if (env == null || env.data == null) continue;
                if (!string.Equals(env.data.prompt_id, targetPromptId, StringComparison.Ordinal)) continue;

                if (env.type == "progress" && env.data.max > 0)
                {
                    float p = Mathf.Clamp01(env.data.value / env.data.max);
                    string nodeInfo = string.IsNullOrEmpty(env.data.node) ? "" : $" ({env.data.node})";
                    onProgress?.Invoke(p, $"步驟 {env.data.value}/{env.data.max}{nodeInfo}");
                }
                else if (env.type == "executing")
                {
                    string nodeInfo = string.IsNullOrEmpty(env.data.node) ? "-" : env.data.node;
                    onProgress?.Invoke(0f, $"執行節點 {nodeInfo}...");
                }
                else if (env.type == "execution_end")
                {
                    onProgress?.Invoke(1f, "執行結束");
                }
            }

            if (ws.State == WebSocketState.Open)
            {
                var closeTask = ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None);
                while (!closeTask.IsCompleted) yield return null;
            }
        }

        [Serializable]
        private class WsEnvelope
        {
            public string type;
            public WsData data;
        }

        [Serializable]
        private class WsData
        {
            public string prompt_id;
            public float value;
            public float max;
            public string node;
        }

        private static string BuildMinimalText2ImgWorkflowJson(ComfyUIRequest r)
        {
            long seed = r.seed >= 0 ? r.seed : UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            var ic = CultureInfo.InvariantCulture;

            var loraList = new List<ComfyUIRequest.LoraEntry>();
            if (r.loras != null && r.loras.Length > 0)
            {
                foreach (var l in r.loras)
                {
                    if (!string.IsNullOrWhiteSpace(l.loraName))
                        loraList.Add(l);
                }
            }
            else if (!string.IsNullOrWhiteSpace(r.loraName))
            {
                loraList.Add(new ComfyUIRequest.LoraEntry
                {
                    loraName = r.loraName.Trim(),
                    modelStrength = r.loraModelStrength == 0 ? 1.0f : r.loraModelStrength,
                    clipStrength = r.loraClipStrength == 0 ? 1.0f : r.loraClipStrength
                });
            }

            var loraNodes = new StringBuilder();
            string modelSource = "2";
            string clipSource = "2";
            int nextId = 9;

            foreach (var l in loraList)
            {
                string id = nextId.ToString();
                loraNodes.Append($@",
              ""{id}"": {{
                ""inputs"": {{
                  ""lora_name"": ""{EscapeJson(l.loraName.Trim())}"",
                  ""strength_model"": {l.modelStrength.ToString(ic)},
                  ""strength_clip"": {l.clipStrength.ToString(ic)},
                  ""model"": [ ""{modelSource}"", 0 ],
                  ""clip"": [ ""{clipSource}"", 1 ]
                }},
                ""class_type"": ""LoraLoader""
              }}");
                modelSource = id;
                clipSource = id;
                nextId++;
            }

            string json = $@"
            {{
              ""2"": {{
                ""inputs"": {{
                  ""ckpt_name"": ""{EscapeJson(r.checkpoint)}""
                }},
                ""class_type"": ""CheckpointLoaderSimple""
              }}{loraNodes},
              ""3"": {{
                ""inputs"": {{
                  ""text"": ""{EscapeJson(r.positivePrompt)}"",
                  ""clip"": [ ""{clipSource}"", 1 ]
                }},
                ""class_type"": ""CLIPTextEncode""
              }},
              ""4"": {{
                ""inputs"": {{
                  ""text"": ""{EscapeJson(r.negativePrompt)}"",
                  ""clip"": [ ""{clipSource}"", 1 ]
                }},
                ""class_type"": ""CLIPTextEncode""
              }},
              ""6"": {{
                ""inputs"": {{
                  ""width"": {r.width},
                  ""height"": {r.height},
                  ""batch_size"": 1
                }},
                ""class_type"": ""EmptyLatentImage""
              }},
              ""5"": {{
                ""inputs"": {{
                  ""seed"": {seed},
                  ""steps"": {r.steps},
                  ""cfg"": {r.cfg.ToString(ic)},
                  ""sampler_name"": ""{EscapeJson(r.samplerName)}"",
                  ""scheduler"": ""{EscapeJson(r.scheduler)}"",
                  ""denoise"": {r.denoise.ToString(ic)},
                  ""model"": [ ""{modelSource}"", 0 ],
                  ""positive"": [ ""3"", 0 ],
                  ""negative"": [ ""4"", 0 ],
                  ""latent_image"": [ ""6"", 0 ]
                }},
                ""class_type"": ""KSampler""
              }},
              ""7"": {{
                ""inputs"": {{
                  ""samples"": [ ""5"", 0 ],
                  ""vae"": [ ""2"", 2 ]
                }},
                ""class_type"": ""VAEDecode""
              }},
              ""8"": {{
                ""inputs"": {{
                  ""images"": [ ""7"", 0 ],
                  ""filename_prefix"": ""{EscapeJson(string.IsNullOrWhiteSpace(r.filenamePrefix) 
                      ? "UnityComfyUI" : r.filenamePrefix)}""
                }},
                ""class_type"": ""SaveImage""
              }}
            }}
            ";
            return json;
        }

        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static bool TryExtractFirstImageInfo(string json, out string filename, out string subfolder,
            out string type)
        {
            filename = null;
            subfolder = "";
            type = "output";

            if (string.IsNullOrEmpty(json)) return false;

            var imagesMatch = Regex.Match(json, "\"images\"\\s*:\\s*\\[\\s*\\{([\\s\\S]*?)\\}\\s*\\]");
            if (!imagesMatch.Success) return false;

            var block = imagesMatch.Groups[1].Value;

            var fnameMatch = Regex.Match(block, "\"filename\"\\s*:\\s*\"([^\"]+)\"");
            if (fnameMatch.Success)
                filename = fnameMatch.Groups[1].Value;

            var subMatch = Regex.Match(block, "\"subfolder\"\\s*:\\s*\"([^\"]*)\"");
            if (subMatch.Success)
                subfolder = subMatch.Groups[1].Value;

            var typeMatch = Regex.Match(block, "\"type\"\\s*:\\s*\"([^\"]*)\"");
            if (typeMatch.Success)
                type = typeMatch.Groups[1].Value;

            return !string.IsNullOrEmpty(filename);
        }
    }
}