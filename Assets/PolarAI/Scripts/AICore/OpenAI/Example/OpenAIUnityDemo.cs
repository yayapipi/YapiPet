// OpenAIUnityDemo.cs
// Single-file all-in-one OpenAI demo for Unity
// Unity 2021.3+  (.NET 4.x)  |  UGUI or TextMeshPro OK (此範例用 UGUI)
// 將本檔放入專案後，新增空GameObject並掛上 OpenAIUnityDemo；把 UI 欄位拖進 Inspector。
// Buttons 綁定：ChatBasic / ChatJsonMode / ChatStream / GenerateImage / VisionDescribe
// StartMicRecord / StopMicAndTranscribe / RealtimeConnect / RealtimeSendText / RealtimeClose / ShowUsage

using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

// ======================= Config（可在 Inspector 改） =======================
[Serializable]
public class OpenAIConfigLite
{
    [Header("Credentials")]
    public string apiKey = "YOUR_OPENAI_API_KEY";
    [Tooltip("可選：OpenAI 組織 ID")]
    public string organizationId = "";
    [Tooltip("可選：OpenAI 專案 ID")]
    public string projectId = "";

    [Header("Models")]
    public string chatModel = "gpt-4o-mini";
    public string jsonModeModel = "gpt-4o-mini";
    public string visionModel = "gpt-4o-mini";
    public string imageModel = "gpt-image-1";
    public string whisperModel = "whisper-1";
    public string realtimeModel = "gpt-4o-realtime-preview";

    [Header("Endpoints")]
    public string baseUrl = "https://api.openai.com/v1";
}

// ======================= MonoBehaviour 主腳本 =======================
public class OpenAIUnityDemo : MonoBehaviour
{
    [Header("OpenAI Config")]
    public OpenAIConfigLite config = new OpenAIConfigLite();

    [Header("UI Refs")]
    public InputField promptInput;    // 可改成 TMP_InputField
    public Text logText;              // 可改成 TMP_Text
    public RawImage imagePreview;     // 顯示生成影像
    public AudioSource micPreview;    // 錄音監聽（可選）

    [Header("UI Buttons (Optional Auto Wiring)")]
    public Button btnChatBasic;
    public Button btnChatJsonMode;
    public Button btnChatStream;
    public Button btnGenerateImage;
    public Button btnVisionDescribe;
    public Button btnStartMicRecord;
    public Button btnStopMicAndTranscribe;
    public Button btnRealtimeConnect;
    public Button btnRealtimeSendText;
    public Button btnRealtimeClose;
    public Button btnShowUsage;

    // Usage 累積
    private int totalInputTokens = 0;
    private int totalOutputTokens = 0;

    // Realtime
    private ClientWebSocket ws;
    private CancellationTokenSource cts;

    // Whisper
    private AudioClip recordedClip;
    private const int MicMaxSeconds = 10;
    private string micDevice;

    private void OnEnable()
    {
        if (btnChatBasic) btnChatBasic.onClick.AddListener(ChatBasic);
        if (btnChatJsonMode) btnChatJsonMode.onClick.AddListener(ChatJsonMode);
        if (btnChatStream) btnChatStream.onClick.AddListener(ChatStream);
        if (btnGenerateImage) btnGenerateImage.onClick.AddListener(GenerateImage);
        if (btnVisionDescribe) btnVisionDescribe.onClick.AddListener(VisionDescribe);
        if (btnStartMicRecord) btnStartMicRecord.onClick.AddListener(StartMicRecord);
        if (btnStopMicAndTranscribe) btnStopMicAndTranscribe.onClick.AddListener(StopMicAndTranscribe);
        if (btnRealtimeConnect) btnRealtimeConnect.onClick.AddListener(RealtimeConnect);
        if (btnRealtimeSendText) btnRealtimeSendText.onClick.AddListener(RealtimeSendText);
        if (btnRealtimeClose) btnRealtimeClose.onClick.AddListener(RealtimeClose);
        if (btnShowUsage) btnShowUsage.onClick.AddListener(ShowUsage);
    }

    private void OnDisable()
    {
        if (btnChatBasic) btnChatBasic.onClick.RemoveListener(ChatBasic);
        if (btnChatJsonMode) btnChatJsonMode.onClick.RemoveListener(ChatJsonMode);
        if (btnChatStream) btnChatStream.onClick.RemoveListener(ChatStream);
        if (btnGenerateImage) btnGenerateImage.onClick.RemoveListener(GenerateImage);
        if (btnVisionDescribe) btnVisionDescribe.onClick.RemoveListener(VisionDescribe);
        if (btnStartMicRecord) btnStartMicRecord.onClick.RemoveListener(StartMicRecord);
        if (btnStopMicAndTranscribe) btnStopMicAndTranscribe.onClick.RemoveListener(StopMicAndTranscribe);
        if (btnRealtimeConnect) btnRealtimeConnect.onClick.RemoveListener(RealtimeConnect);
        if (btnRealtimeSendText) btnRealtimeSendText.onClick.RemoveListener(RealtimeSendText);
        if (btnRealtimeClose) btnRealtimeClose.onClick.RemoveListener(RealtimeClose);
        if (btnShowUsage) btnShowUsage.onClick.RemoveListener(ShowUsage);
    }

    // ------------------ Buttons: Chat 基本 ------------------
    [Serializable] class ResponsesReq
    {
        public string model;
        public string input;                 // 單字串輸入
        public bool stream = false;
        public object response_format = null; // JSON Mode: { "type": "json_object" }
    }
    [Serializable] class JsonType { public string type; }

    public void ChatBasic()
    {
        string user = string.IsNullOrEmpty(promptInput ? promptInput.text : null)
            ? "Hello from Unity!"
            : promptInput.text;

        var req = new ResponsesReq {
            model = config.chatModel, input = user, stream = false
        };
        var json = JsonUtility.ToJson(req);
        StartCoroutine(PostJson("responses", json,
            res => { TryAccumulateUsage(res); Log("ChatBasic:\n" + res); },
            err => LogError(err)));
    }

    public void ChatJsonMode()
    {
        string user = string.IsNullOrEmpty(promptInput ? promptInput.text : null)
            ? "請輸出 JSON：{\"title\":\"...\"}"
            : promptInput.text;

        var req = new ResponsesReq {
            model = config.jsonModeModel,
            input = user,
            stream = false,
            response_format = new JsonType { type = "json_object" }
        };
        var json = ToJson(req);
        StartCoroutine(PostJson("responses", json,
            res => { TryAccumulateUsage(res); Log("ChatJson:\n" + res); },
            err => LogError(err)));
    }

    public void ChatStream()
    {
        string user = string.IsNullOrEmpty(promptInput ? promptInput.text : null)
            ? "Stream a poem about polar bears."
            : promptInput.text;

        var req = new ResponsesReq { model = config.chatModel, input = user, stream = true };
        var json = ToJson(req);

        Log("Streaming…");
        StartCoroutine(PostSseStream("responses", json,
            onDelta: chunk => { LogAppend(ParseDeltaToText(chunk)); },
            onComplete: finalJson => {
                if (!string.IsNullOrEmpty(finalJson)) TryAccumulateUsage(finalJson);
                LogAppend("\n[Stream Complete]");
            },
            onError: err => LogError(err)));
    }

    // 這裡為簡化示意，真實應按 SSE 片段 JSON 結構擷取文字
    private string ParseDeltaToText(string jsonDelta) => jsonDelta + "\n";

    // ------------------ Buttons: 影像生成 ------------------
    [Serializable] class ImageGenReq
    {
        public string model;
        public string prompt;
        public string size = "1024x1024";
        public string response_format = "b64_json";
    }
    [Serializable] class ImageData { public string b64_json; }
    [Serializable] class ImageGenResp { public ImageData[] data; }

    public void GenerateImage()
    {
        string p = string.IsNullOrEmpty(promptInput ? promptInput.text : null)
            ? "a cute low-poly polar bear"
            : promptInput.text;

        var req = new ImageGenReq { model = config.imageModel, prompt = p };
        var json = JsonUtility.ToJson(req);
        StartCoroutine(PostJson("images/generations", json,
            res => {
                try
                {
                    var obj = JsonUtility.FromJson<ImageGenResp>(res);
                    if (obj?.data != null && obj.data.Length > 0)
                    {
                        var bytes = Convert.FromBase64String(obj.data[0].b64_json);
                        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                        tex.LoadImage(bytes);
                        if (imagePreview) imagePreview.texture = tex;
                        Log("Image generated.");
                    }
                    else Log("No image data.");
                }
                catch (Exception e) { LogError(e.Message); }
            },
            err => LogError(err)));
    }

    // ------------------ Buttons: Vision ------------------
    [Serializable] class VisionInput
    {
        public string model;
        public bool stream = false;
        public InputContent[] input;
    }
    [Serializable] class InputContent
    {
        public string role = "user";
        public Part[] content;
    }
    [Serializable] class Part
    {
        public string type;         // "input_text" | "input_image"
        public string text;         // for input_text
        public ImageUrl image_url;  // for input_image
    }
    [Serializable] class ImageUrl { public string url; }

    public void VisionDescribe()
    {
        string imgUrl = "https://upload.wikimedia.org/wikipedia/commons/e/e7/Polar_Bear_-_Alaska.jpg";
        string question = string.IsNullOrEmpty(promptInput ? promptInput.text : null)
            ? "用中文簡要描述這張圖。"
            : promptInput.text;

        var req = new VisionInput
        {
            model = config.visionModel,
            stream = false,
            input = new[] {
                new InputContent {
                    content = new [] {
                        new Part { type = "input_text", text = question },
                        new Part { type = "input_image", image_url = new ImageUrl{ url = imgUrl } }
                    }
                }
            }
        };
        var json = ToJson(req);
        StartCoroutine(PostJson("responses", json,
            res => { TryAccumulateUsage(res); Log("Vision:\n" + res); },
            err => LogError(err)));
    }

    // ------------------ Buttons: Whisper（錄音→文字） ------------------
    public void StartMicRecord()
    {
        if (Microphone.devices.Length == 0) { Log("No microphone found."); return; }
        micDevice = Microphone.devices[0];
        recordedClip = Microphone.Start(micDevice, false, MicMaxSeconds, 44100);
        if (micPreview) { micPreview.clip = recordedClip; micPreview.loop = true; micPreview.Play(); }
        Log("Recording…");
    }

    public void StopMicAndTranscribe()
    {
        if (string.IsNullOrEmpty(micDevice) || recordedClip == null) { Log("No recording."); return; }
        Microphone.End(micDevice);
        if (micPreview) micPreview.Stop();
        Log("Encoding WAV…");
        var wav = WavUtility.FromClipToWavBytes(recordedClip);

        StartCoroutine(PostMultipart("audio/transcriptions", wav, "unity_mic.wav", config.whisperModel,
            res => { Log("Transcription:\n" + res); },
            err => LogError(err)));
    }

    // ------------------ Buttons: Realtime（WebSocket） ------------------
    public async void RealtimeConnect()
    {
        try
        {
            cts = new CancellationTokenSource();
            ws = new ClientWebSocket();
            ws.Options.SetRequestHeader("Authorization", $"Bearer {config.apiKey}");
            ws.Options.SetRequestHeader("OpenAI-Beta", "realtime=v1");
            if (!string.IsNullOrEmpty(config.organizationId))
                ws.Options.SetRequestHeader("OpenAI-Organization", config.organizationId);
            if (!string.IsNullOrEmpty(config.projectId))
                ws.Options.SetRequestHeader("OpenAI-Project", config.projectId);

            var uri = new Uri($"wss://api.openai.com/v1/realtime?model={config.realtimeModel}");
            await ws.ConnectAsync(uri, cts.Token);
            Log("[Realtime] Connected.");
            _ = ReceiveLoop();
        }
        catch (Exception e) { LogError("[Realtime] " + e.Message); }
    }

    public async void RealtimeSendText()
    {
        if (ws == null || ws.State != WebSocketState.Open) { Log("[Realtime] Not connected."); return; }
        var userText = string.IsNullOrEmpty(promptInput ? promptInput.text : null)
            ? "用一句話鼓勵我。"
            : promptInput.text;

        var payload = "{\"type\":\"response.create\",\"response\":{\"instructions\":\"" + Escape(userText) + "\"}}";
        var bytes = Encoding.UTF8.GetBytes(payload);
        try
        {
            await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cts.Token);
            Log("[Realtime->] " + payload);
        }
        catch (Exception e) { LogError("[Realtime Tx] " + e.Message); }
    }

    public async void RealtimeClose()
    {
        try
        {
            if (ws != null && ws.State == WebSocketState.Open)
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
            ws?.Dispose();
            cts?.Cancel();
            Log("[Realtime] Closed.");
        }
        catch (Exception e) { LogError(e.Message); }
    }

    private async System.Threading.Tasks.Task ReceiveLoop()
    {
        var buffer = new ArraySegment<byte>(new byte[8192]);
        try
        {
            while (ws.State == WebSocketState.Open)
            {
                var ms = new System.IO.MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await ws.ReceiveAsync(buffer, cts.Token);
                    ms.Write(buffer.Array, buffer.Offset, result.Count);
                } while (!result.EndOfMessage);

                var msg = Encoding.UTF8.GetString(ms.ToArray());
                Log("[Realtime<-] " + msg);
            }
        }
        catch (Exception e) { LogError("[Realtime Rx] " + e.Message); }
    }

    // ------------------ Buttons: Usage 顯示 ------------------
    public void ShowUsage()
    {
        Log($"[Usage] Input: {totalInputTokens}, Output: {totalOutputTokens}");
    }

    private void TryAccumulateUsage(string json)
    {
        try
        {
            var u = JsonUtility.FromJson<RespRoot>(json);
            if (u != null && u.usage != null)
            {
                totalInputTokens += Mathf.Max(0, u.usage.input_tokens);
                totalOutputTokens += Mathf.Max(0, u.usage.output_tokens);
            }
        }
        catch { /* ignore */ }
    }

    [Serializable] class Usage { public int input_tokens; public int output_tokens; }
    [Serializable] class RespRoot { public Usage usage; }

    // ======================= HTTP：JSON / SSE / Multipart =======================
    private IEnumerator PostJson(string path, string json, Action<string> onSuccess, Action<string> onError, Dictionary<string, string> extraHeaders = null)
    {
        var url = $"{config.baseUrl}/{path}";
        var req = new UnityWebRequest(url, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
        req.uploadHandler = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Authorization", $"Bearer {config.apiKey}");
        req.SetRequestHeader("Content-Type", "application/json");
        if (!string.IsNullOrEmpty(config.organizationId))
            req.SetRequestHeader("OpenAI-Organization", config.organizationId);
        if (!string.IsNullOrEmpty(config.projectId))
            req.SetRequestHeader("OpenAI-Project", config.projectId);
        if (extraHeaders != null)
            foreach (var kv in extraHeaders) req.SetRequestHeader(kv.Key, kv.Value);

        yield return req.SendWebRequest();
        if (req.result != UnityWebRequest.Result.Success) onError?.Invoke(req.error);
        else onSuccess?.Invoke(req.downloadHandler.text);
    }

    private IEnumerator PostSseStream(string path, string json,
        Action<string> onDelta, Action<string> onComplete, Action<string> onError)
    {
        var url = $"{config.baseUrl}/{path}";
        var req = new UnityWebRequest(url, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
        req.uploadHandler = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Authorization", $"Bearer {config.apiKey}");
        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("Accept", "text/event-stream");

        var prev = 0;
        var sb = new StringBuilder();
        var done = false;

        var op = req.SendWebRequest();
        while (!op.isDone)
        {
            var txt = req.downloadHandler.text;
            if (txt != null && txt.Length > prev)
            {
                sb.Append(txt.Substring(prev));
                prev = txt.Length;

                var chunks = sb.ToString().Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < Mathf.Max(0, chunks.Length - 1); i++)
                {
                    var line = chunks[i];
                    if (line.StartsWith("data: "))
                    {
                        var payload = line.Substring(6).Trim();
                        if (payload == "[DONE]") { done = true; break; }
                        onDelta?.Invoke(payload);
                    }
                }
                sb.Clear();
                if (chunks.Length > 0) sb.Append(chunks[chunks.Length - 1]);
            }
            yield return null;
        }

        if (req.result != UnityWebRequest.Result.Success) onError?.Invoke(req.error);
        else onComplete?.Invoke(req.downloadHandler.text);

        if (!done) onComplete?.Invoke(string.Empty);
    }

    private IEnumerator PostMultipart(string path, byte[] fileBytes, string fileName, string model,
        Action<string> onSuccess, Action<string> onError)
    {
        var url = $"{config.baseUrl}/{path}";
        var form = new WWWForm();
        form.AddField("model", model);
        form.AddBinaryData("file", fileBytes, fileName, "audio/wav");

        var req = UnityWebRequest.Post(url, form);
        req.SetRequestHeader("Authorization", $"Bearer {config.apiKey}");
        if (!string.IsNullOrEmpty(config.organizationId))
            req.SetRequestHeader("OpenAI-Organization", config.organizationId);
        if (!string.IsNullOrEmpty(config.projectId))
            req.SetRequestHeader("OpenAI-Project", config.projectId);

        yield return req.SendWebRequest();
        if (req.result != UnityWebRequest.Result.Success) onError?.Invoke(req.error);
        else onSuccess?.Invoke(req.downloadHandler.text);
    }

    // ======================= Utils =======================
    private string ToJson<T>(T obj) => JsonUtility.ToJson(obj);

    private string Escape(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");

    private void Log(string s)
    {
        if (logText) logText.text = s;
        Debug.Log(s);
    }
    private void LogAppend(string s)
    {
        if (logText) logText.text += s;
        Debug.Log(s);
    }
    private void LogError(string s)
    {
        if (logText) logText.text = "[Error] " + s;
        Debug.LogError(s);
    }
}

// ======================= WAV 工具（Mic 轉 WAV bytes） =======================
public static class WavUtility
{
    public static byte[] FromClipToWavBytes(AudioClip clip)
    {
        var samples = new float[clip.samples * clip.channels];
        clip.GetData(samples, 0);
        byte[] pcm16 = FloatToPCM16(samples);

        using (var ms = new System.IO.MemoryStream())
        using (var bw = new System.IO.BinaryWriter(ms))
        {
            int byteRate = clip.frequency * 2 * clip.channels; // 16-bit
            int subChunk2Size = pcm16.Length;
            int chunkSize = 36 + subChunk2Size;

            // RIFF
            bw.Write(Encoding.UTF8.GetBytes("RIFF"));
            bw.Write(chunkSize);
            bw.Write(Encoding.UTF8.GetBytes("WAVE"));

            // fmt 
            bw.Write(Encoding.UTF8.GetBytes("fmt "));
            bw.Write(16); // Subchunk1Size for PCM
            bw.Write((short)1); // AudioFormat = PCM
            bw.Write((short)clip.channels);
            bw.Write(clip.frequency);
            bw.Write(byteRate);
            bw.Write((short)(clip.channels * 2)); // BlockAlign
            bw.Write((short)16); // BitsPerSample

            // data
            bw.Write(Encoding.UTF8.GetBytes("data"));
            bw.Write(subChunk2Size);
            bw.Write(pcm16);

            return ms.ToArray();
        }
    }

    private static byte[] FloatToPCM16(float[] samples)
    {
        byte[] bytes = new byte[samples.Length * 2];
        const int rescale = 32767;
        for (int i = 0; i < samples.Length; i++)
        {
            short val = (short)Mathf.Clamp(samples[i] * rescale, short.MinValue, short.MaxValue);
            bytes[i * 2] = (byte)(val & 0xFF);
            bytes[i * 2 + 1] = (byte)((val >> 8) & 0xFF);
        }
        return bytes;
    }
}
