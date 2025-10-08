using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using PolarAI.Scripts.Core.Ollama.Model;
using UnityEngine;
using UnityEngine.Networking;

namespace PolarAI.Scripts.Core.Ollama
{
    public class OllamaAICore
    {
        private readonly OllamaClientOptions _options;

        public OllamaAICore(OllamaClientOptions options)
        {
            _options = options ?? OllamaClientOptions.DefaultLocal();
            if (string.IsNullOrEmpty(_options.Host))
            {
                _options.Host = _options.Mode == OllamaMode.Turbo ? "https://ollama.com" : "http://localhost:11434";
            }
        }

        private string BuildUrl(string path)
        {
            if (string.IsNullOrEmpty(_options.Host)) return path;
            if (_options.Host.EndsWith("/"))
                return _options.Host.TrimEnd('/') + path;
            return _options.Host + path;
        }

        private UnityWebRequest BuildJsonPost(string url, string json)
        {
            var req = new UnityWebRequest(url, "POST");
            var bodyRaw = Encoding.UTF8.GetBytes(json);
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            if (_options.Mode == OllamaMode.Turbo && !string.IsNullOrEmpty(_options.ApiKey))
            {
                var value = string.IsNullOrEmpty(_options.ApiKeyHeaderPrefix)
                    ? _options.ApiKey
                    : (_options.ApiKeyHeaderPrefix + _options.ApiKey);
                req.SetRequestHeader(_options.ApiKeyHeaderName, value);
            }

            req.timeout = _options.Timeout;
            return req;
        }

        public IEnumerator ChatOnce(string model, List<OllamaMessage> messages,
            Action<string> onCompleted,
            Action<string> onError = null)
        {
            if (string.IsNullOrEmpty(model))
            {
                onError?.Invoke("Model Cannot Be Empty");
                yield break;
            }

            var url = BuildUrl("/api/chat");
            var payload = new OllamaChatRequest
            {
                model = model,
                messages = messages ?? new List<OllamaMessage>(),
                stream = false
            };
            var json = JsonUtility.ToJson(payload);

            using var req = BuildJsonPost(url, json);
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke($"HTTP Error: {req.responseCode} - {req.error}");
                yield break;
            }

            var text = req.downloadHandler.text;
            if (string.IsNullOrEmpty(text))
            {
                onError?.Invoke("空回應");
                yield break;
            }

            OllamaChatStreamChunk doc = null;
            try
            {
                doc = JsonConvert.DeserializeObject<OllamaChatStreamChunk>(text);
            }
            catch (Exception e)
            {
                onError?.Invoke($"JSON 解析失敗: {e.Message}\n{text}");
                yield break;
            }

            var content = (doc != null && doc.message != null) ? doc.message.content : null;
            if (string.IsNullOrEmpty(content))
            {
                onError?.Invoke("回傳內容為空");
                yield break;
            }

            onCompleted?.Invoke(content);
        }

        public IEnumerator ChatStream(string model, List<OllamaMessage> messages,
            Action<string> onToken,
            Action<string> onCompleted,
            Action<string> onError = null)
        {
            if (string.IsNullOrEmpty(model))
            {
                onError?.Invoke("Model Cannot Be Empty");
                yield break;
            }

            var url = BuildUrl("/api/chat");
            var payload = new OllamaChatRequest
            {
                model = model,
                messages = messages ?? new List<OllamaMessage>(),
                stream = true
            };
            var json = JsonUtility.ToJson(payload);

            var downloadHandler = new StreamingDownloadHandler(
                onLineJson: (line) =>
                {
                    if (string.IsNullOrEmpty(line)) return;

                    OllamaChatStreamChunk chunk = null;
                    try
                    {
                        chunk = JsonUtility.FromJson<OllamaChatStreamChunk>(line);
                    }
                    catch
                    {
                        return;
                    }

                    if (chunk == null) return;

                    if (chunk.message != null && !string.IsNullOrEmpty(chunk.message.content))
                    {
                        onToken?.Invoke(chunk.message.content);
                    }

                    if (chunk.done)
                    {
                        onCompleted?.Invoke(string.Empty);
                    }
                },
                onError: (err) => { onError?.Invoke(err); }
            );

            var req = new UnityWebRequest(url, "POST");
            var bodyRaw = Encoding.UTF8.GetBytes(json);
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = downloadHandler;
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = _options.Timeout;

            if (_options.Mode == OllamaMode.Turbo && !string.IsNullOrEmpty(_options.ApiKey))
            {
                var value = string.IsNullOrEmpty(_options.ApiKeyHeaderPrefix)
                    ? _options.ApiKey
                    : (_options.ApiKeyHeaderPrefix + _options.ApiKey);
                req.SetRequestHeader(_options.ApiKeyHeaderName, value);
            }

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke($"HTTP Error: {req.responseCode} - {req.error}");
            }

            req.Dispose();
        }

        // 下載時即時處理逐行 JSON（以 '\n' 分隔）
        private class StreamingDownloadHandler : DownloadHandlerScript
        {
            private readonly Action<string> _onLineJson;
            private readonly Action<string> _onError;
            private readonly StringBuilder _buffer = new StringBuilder();

            public StreamingDownloadHandler(Action<string> onLineJson, Action<string> onError)
                : base()
            {
                _onLineJson = onLineJson;
                _onError = onError;
            }

            protected override bool ReceiveData(byte[] data, int dataLength)
            {
                if (data == null || dataLength <= 0)
                    return true;

                var text = Encoding.UTF8.GetString(data, 0, dataLength);
                _buffer.Append(text);

                // 依照換行切割
                int newlineIndex;
                while ((newlineIndex = _buffer.ToString().IndexOf('\n')) >= 0)
                {
                    var line = _buffer.ToString(0, newlineIndex).Trim();
                    _buffer.Remove(0, newlineIndex + 1);

                    if (!string.IsNullOrEmpty(line))
                    {
                        try
                        {
                            _onLineJson?.Invoke(line);
                        }
                        catch (Exception ex)
                        {
                            _onError?.Invoke($"處理串流行時發生錯誤: {ex.Message}");
                        }
                    }
                }

                return true;
            }

            protected override void CompleteContent()
            {
                // 尾端若尚有未換行的資料，視為一行處理
                var remaining = _buffer.ToString().Trim();
                if (!string.IsNullOrEmpty(remaining))
                {
                    try
                    {
                        _onLineJson?.Invoke(remaining);
                    }
                    catch (Exception ex)
                    {
                        _onError?.Invoke($"完成時處理剩餘資料錯誤: {ex.Message}");
                    }
                }

                _buffer.Length = 0;
            }

            protected override float GetProgress()
            {
                return base.GetProgress();
            }

            protected override byte[] GetData()
            {
                return null;
            }
        }
    }
}