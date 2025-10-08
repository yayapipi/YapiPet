using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Unity + OpenAI 官方 File Search（RAG）完整示例
/// 流程：
///  1) CreateVectorStore →
///  2) UploadToFiles(逐檔 /multipart) → AttachFilesToVectorStoreBatch(JSON) → PollFileBatchUntilDone →
///  3) AskWithRag(Responses + tools:file_search + tool_resources.vector_store_ids)
///
/// ★ 生產注意：請將 API 呼叫放在你的後端 Relay，不要把 API Key 打包進遊戲。
/// </summary>
public class OpenAIRagClient : MonoBehaviour
{
    [Header("OpenAI")] [Tooltip("僅供本機測試；正式請改由你的後端轉發")] [SerializeField]
    private string apiKey = "sk-...";

    [SerializeField] private string baseUrl = "https://api.openai.com/v1";

    [Tooltip("建議 gpt-4o-mini（成本/效果均衡），也可換 gpt-4.1-mini 等")] [SerializeField]
    private string model = "gpt-4o-mini";

    [Header("Demo Settings")] [Tooltip("勾選就會在 Start() 自動跑完整流程 Demo")] [SerializeField]
    private bool runOnStart = false;

    [SerializeField] private string vectorStoreName = "UnityRAGStore";

    [Tooltip("相對於 StreamingAssets 的路徑或絕對路徑")] [SerializeField]
    private string[] localFilePaths = new string[]
    {
        "docs/Lore_Codex_Akashic_Fake_v1.txt",
        "docs/Item_Compendium_Fake_v1.txt",
        "docs/Quest_FAQ_Fake_v1.txt"
    };

    [TextArea(3, 6)] [SerializeField] private string question = "請列出『三環防護』包含哪三層，並附來源頁碼。";

    [SerializeField] private float pollIntervalSeconds = 1.2f;

    [Header("Quick Ask (reuse existing Assistant)")]
    [Tooltip("勾選後，Start() 會直接用已保存的 assistantId 問問題（不重建，不上傳）")]
    [SerializeField]
    private bool askOnly = false;

    [Tooltip("已建立並保存的 Assistant Id（綁定你的向量庫）")] [SerializeField]
    private string persistedAssistantId = "";

    [Tooltip("重複使用的向量庫 Id（僅在需要自動建 Assistant 時使用）")] [SerializeField]
    private string persistedVectorStoreId = "";

    [TextArea(3, 6)] [SerializeField] private string quickAskText = "只問問題：這裡填你的問題";

    [Tooltip("當沒有 assistantId 時，自動建立一個並保存到本欄位")] [SerializeField]
    private bool autoCreateAssistantIfMissing = true;

    private void Start()
    {
        if (askOnly)
        {
            StartCoroutine(QuickAskFlow());
            return;
        }

        if (runOnStart)
        {
            StartCoroutine(DemoPipeline());
        }
    }

    /// <summary>
    /// 一鍵示範：建向量庫 → 上傳多檔 → 批次掛入 → 輪詢 → 提問（RAG）
    /// </summary>
    public IEnumerator DemoPipeline()
    {
        // 1) 建立 Vector Store
        string vsId = null;
        string vsErr = null;
        yield return CreateVectorStore(vectorStoreName, id => vsId = id, e => vsErr = e);
        if (!string.IsNullOrEmpty(vsErr) || string.IsNullOrEmpty(vsId))
        {
            Debug.LogError("CreateVectorStore failed: " + vsErr);
            yield break;
        }

        Debug.Log("[VectorStore] created: " + vsId);

        // 2) 將多個檔案先上傳到 /v1/files 拿到 file_id
        var fileIds = new List<string>();
        foreach (var p in localFilePaths)
        {
            var abs = ResolvePath(p);
            string fid = null;
            string upErr = null;
            yield return UploadToFiles(abs, id => fid = id, e => upErr = e);
            if (!string.IsNullOrEmpty(upErr) || string.IsNullOrEmpty(fid))
            {
                Debug.LogError("UploadToFiles failed: " + upErr + " (" + abs + ")");
                yield break;
            }

            fileIds.Add(fid);
            Debug.Log($"[Files] uploaded: {Path.GetFileName(abs)} -> {fid}");
        }

        // 3) 批次掛入 Vector Store
        string batchId = null;
        string batchErr = null;
        yield return AttachFilesToVectorStoreBatch(vsId, fileIds.ToArray(), id => batchId = id, e => batchErr = e);
        if (!string.IsNullOrEmpty(batchErr) || string.IsNullOrEmpty(batchId))
        {
            Debug.LogError("AttachFilesToVectorStoreBatch failed: " + batchErr);
            yield break;
        }

        Debug.Log("[FileBatch] created: " + batchId);

        // 4) 輪詢直到 completed（表示嵌入完成、可檢索）
        string doneStatus = null;
        string pollErr = null;
        yield return PollFileBatchUntilDone(vsId, batchId, pollIntervalSeconds, s => doneStatus = s, e => pollErr = e);
        if (!string.IsNullOrEmpty(pollErr) || doneStatus != "completed")
        {
            Debug.LogError("Batch status: " + (doneStatus ?? "null") + " / " + pollErr);
            yield break;
        }

        Debug.Log("[FileBatch] status: completed");

        // 5) 問一題（RAG）
        string answer = null;
        string askErr = null;
        yield return AskWithRagViaAssistantsRun(vsId, question, a => answer = a, e => askErr = e);
        if (!string.IsNullOrEmpty(askErr))
        {
            Debug.LogError("AskWithRag failed: " + askErr);
            yield break;
        }

        Debug.Log("\n========== RAG Answer ==========" +
                  "\n" + answer +
                  "\n================================\n");
    }

    // -----------------------------------
    // API Methods
    // -----------------------------------

    /// <summary>
    /// POST /v1/vector_stores  （需要 Header: OpenAI-Beta: assistants=v2）
    /// </summary>
    public IEnumerator CreateVectorStore(string name, Action<string> onSuccess, Action<string> onError)
    {
        var url = $"{baseUrl}/vector_stores";
        var payload = "{\"name\":\"" + EscapeJson(name) + "\"}";

        using (var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
        {
            var bodyRaw = Encoding.UTF8.GetBytes(payload);
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Authorization", "Bearer " + apiKey);
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("OpenAI-Beta", "assistants=v2");

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke(
                    $"CreateVectorStore failed: {req.responseCode} {req.error}\n{req.downloadHandler.text}");
            }
            else
            {
                var json = req.downloadHandler.text;
                var id = ExtractJsonValue(json, "id");
                if (string.IsNullOrEmpty(id)) onError?.Invoke("CreateVectorStore: couldn't parse id.\n" + json);
                else onSuccess?.Invoke(id);
            }
        }
    }

    /// <summary>
    /// POST /v1/files  （multipart/form-data, purpose=assistants）
    /// 先把檔案丟進 Files，取得 file_id
    /// </summary>
    public IEnumerator UploadToFiles(string filePath, Action<string> onSuccess, Action<string> onError)
    {
        if (!File.Exists(filePath))
        {
            onError?.Invoke("File not found: " + filePath);
            yield break;
        }

        var url = $"{baseUrl}/files";
        var form = new WWWForm();
        form.AddField("purpose", "assistants");
        form.AddBinaryData("file", File.ReadAllBytes(filePath), Path.GetFileName(filePath), "application/octet-stream");

        using (var req = UnityWebRequest.Post(url, form))
        {
            req.SetRequestHeader("Authorization", "Bearer " + apiKey);
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke($"UploadToFiles failed: {req.responseCode} {req.error}\n{req.downloadHandler.text}");
            }
            else
            {
                var json = req.downloadHandler.text;
                var fileId = ExtractJsonValue(json, "id");
                if (string.IsNullOrEmpty(fileId)) onError?.Invoke("UploadToFiles: cannot parse file id.\n" + json);
                else onSuccess?.Invoke(fileId);
            }
        }
    }

    /// <summary>
    /// POST /v1/vector_stores/{id}/file_batches  （application/json）
    /// 一次把多個 file_id 掛入 Vector Store
    /// </summary>
    public IEnumerator AttachFilesToVectorStoreBatch(string vectorStoreId, string[] fileIds, Action<string> onBatchId,
        Action<string> onError)
    {
        var url = $"{baseUrl}/vector_stores/{vectorStoreId}/file_batches";
        var sb = new StringBuilder();
        sb.Append("{\"file_ids\":[");
        for (int i = 0; i < fileIds.Length; i++)
        {
            if (i > 0) sb.Append(",");
            sb.Append("\"").Append(EscapeJson(fileIds[i])).Append("\"");
        }

        sb.Append("]}");
        var payload = sb.ToString();

        using (var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(payload));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Authorization", "Bearer " + apiKey);
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("OpenAI-Beta", "assistants=v2");

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke($"Attach batch failed: {req.responseCode} {req.error}\n{req.downloadHandler.text}");
            }
            else
            {
                var json = req.downloadHandler.text;
                var batchId = ExtractJsonValue(json, "id");
                if (string.IsNullOrEmpty(batchId)) onError?.Invoke("File batch created but no id parsed.\n" + json);
                else onBatchId?.Invoke(batchId);
            }
        }
    }

    /// <summary>
    /// GET /v1/vector_stores/{id}/file_batches/{batch_id}
    /// 輪詢直到 status ∈ {completed, failed, canceled}
    /// </summary>
    public IEnumerator PollFileBatchUntilDone(string vectorStoreId, string batchId, float intervalSec,
        Action<string> onDoneStatus, Action<string> onError)
    {
        var url = $"{baseUrl}/vector_stores/{vectorStoreId}/file_batches/{batchId}";
        while (true)
        {
            using (var req = UnityWebRequest.Get(url))
            {
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Authorization", "Bearer " + apiKey);
                req.SetRequestHeader("OpenAI-Beta", "assistants=v2");
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke($"Poll batch failed: {req.responseCode} {req.error}\n{req.downloadHandler.text}");
                    yield break;
                }

                var json = req.downloadHandler.text;
                var status = ExtractJsonValue(json, "status"); // in_progress | completed | failed | canceled
                if (status == "completed" || status == "failed" || status == "canceled")
                {
                    onDoneStatus?.Invoke(status);
                    yield break;
                }
            }

            yield return new WaitForSeconds(intervalSec);
        }
    }

    /// <summary>
    /// POST /v1/responses with tools: file_search + tool_resources.vector_store_ids
    /// </summary>
    public IEnumerator AskWithRag(string vectorStoreId, string userQuestion, Action<string> onSuccess,
        Action<string> onError)
    {
        var url = $"{baseUrl}/responses";
        var body = new StringBuilder();
        body.Append("{");
        body.Append("\"model\":\"").Append(model).Append("\",");
        body.Append("\"input\":[{\"role\":\"user\",\"content\":\"").Append(EscapeJson(userQuestion)).Append("\"}],");
        body.Append("\"tools\":[{\"type\":\"file_search\"}],");
        body.Append("\"tool_resources\":{\"file_search\":{\"vector_store_ids\":[\"").Append(vectorStoreId)
            .Append("\"]}},");
        body.Append("\"max_output_tokens\":800");
        body.Append("}");

        using (var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
        {
            var data = Encoding.UTF8.GetBytes(body.ToString());
            req.uploadHandler = new UploadHandlerRaw(data);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Authorization", "Bearer " + apiKey);
            req.SetRequestHeader("Content-Type", "application/json");

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke($"AskWithRag failed: {req.responseCode} {req.error}\n{req.downloadHandler.text}");
            }
            else
            {
                var json = req.downloadHandler.text;
                var outputText = ExtractJsonValue(json, "output_text");
                onSuccess?.Invoke(!string.IsNullOrEmpty(outputText) ? UnescapeJson(outputText) : json);
            }
        }
    }

    // -----------------------------------
    // Helpers
    // -----------------------------------

    private string ResolvePath(string path)
    {
        if (Path.IsPathRooted(path)) return path;
        return Path.Combine(Application.streamingAssetsPath, path);
    }

    private static string EscapeJson(string s)
        => s?.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");

    private static string UnescapeJson(string s)
        => s?.Replace("\\n", "\n").Replace("\\r", "\r").Replace("\\\"", "\"").Replace("\\\\", "\\");

    /// <summary>
    /// 超輕量的 JSON value 抽取（僅支援最外層字串鍵值；足以取 id/status/output_text）
    /// </summary>
    private static string ExtractJsonValue(string json, string key)
    {
        var pattern = $"\"{key}\"\\s*:\\s*\"";
        var m = Regex.Match(json, pattern);
        if (!m.Success) return null;
        int i = m.Index + m.Length;
        var sb = new StringBuilder();
        bool esc = false;
        while (i < json.Length)
        {
            char c = json[i++];
            if (esc)
            {
                sb.Append(c);
                esc = false;
                continue;
            }

            if (c == '\\')
            {
                esc = true;
                continue;
            }

            if (c == '"') break;
            sb.Append(c);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Responses API：把 file_ids 以 "attachments" 放進同一則 user message
    /// 注意：attachments 必須在 input 的 message 物件上，而不是頂層；
    /// content 也要用 [{type:"input_text", text:"..."}] 的結構化格式。
    /// </summary>
    public IEnumerator AskWithRagViaMessageAttachments(string[] fileIds, string userQuestion,
        Action<string> onSuccess, Action<string> onError)
    {
        var url = $"{baseUrl}/responses";
        var sb = new StringBuilder();
        sb.Append("{");
        sb.Append("\"model\":\"").Append(model).Append("\",");
        sb.Append("\"tools\":[{\"type\":\"file_search\"}],");
        sb.Append("\"input\":[{");
        sb.Append("\"role\":\"user\",");
        sb.Append("\"content\":[{\"type\":\"input_text\",\"text\":\"").Append(EscapeJson(userQuestion)).Append("\"}],");
        sb.Append("\"attachments\":[");
        for (int i = 0; i < fileIds.Length; i++)
        {
            if (i > 0) sb.Append(",");
            sb.Append("{\"file_id\":\"").Append(EscapeJson(fileIds[i]))
                .Append("\",\"tools\":[{\"type\":\"file_search\"}]}");
        }

        sb.Append("]");
        sb.Append("}],");
        sb.Append("\"max_output_tokens\":800");
        sb.Append("}");

        using (var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(sb.ToString()));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Authorization", "Bearer " + apiKey);
            req.SetRequestHeader("Content-Type", "application/json");
            // 若你的環境依然 400，可嘗試打開下一行 Beta header：
            // req.SetRequestHeader("OpenAI-Beta", "assistants=v2");

            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke(
                    $"AskWithRagViaMessageAttachments failed: {req.responseCode} {req.error}\n{req.downloadHandler.text}");
            }
            else
            {
                var json = req.downloadHandler.text;
                var outputText = ExtractJsonValue(json, "output_text");
                onSuccess?.Invoke(!string.IsNullOrEmpty(outputText) ? UnescapeJson(outputText) : json);
            }
        }
    }

    /// <summary>
    /// 以 Assistants v2 的 Threads/Runs 路徑做 RAG（最穩定）：
    /// 1) 建 Assistant（掛上 file_search + vector_store_ids）
    /// 2) 建 Thread 並加入 user 訊息
    /// 3) 建 Run，輪詢直到 completed
    /// 4) 讀取最新訊息文字並回傳
    /// </summary>
    public IEnumerator AskWithRagViaAssistantsRun(string vectorStoreId, string userQuestion,
        Action<string> onSuccess, Action<string> onError)
    {
        // 1) Create Assistant
        string assistantId = null;
        {
            var url = $"{baseUrl}/assistants";
            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append("\"model\":\"").Append(model).Append("\",");
            sb.Append("\"tools\":[{\"type\":\"file_search\"}],");
            sb.Append("\"tool_resources\":{\"file_search\":{\"vector_store_ids\":[\"").Append(vectorStoreId)
                .Append("\"]}}");
            sb.Append("}");

            using (var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(sb.ToString()));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Authorization", "Bearer " + apiKey);
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("OpenAI-Beta", "assistants=v2");
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke(
                        $"Create assistant failed: {req.responseCode} {req.error}\n{req.downloadHandler.text}");
                    yield break;
                }

                assistantId = ExtractJsonValue(req.downloadHandler.text, "id");
                if (string.IsNullOrEmpty(assistantId))
                {
                    onError?.Invoke("Assistant id parse failed");
                    yield break;
                }
            }
        }

        // 2) Create Thread
        string threadId = null;
        {
            var url = $"{baseUrl}/threads";
            using (var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes("{}"));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Authorization", "Bearer " + apiKey);
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("OpenAI-Beta", "assistants=v2");
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke(
                        $"Create thread failed: {req.responseCode} {req.error}\n{req.downloadHandler.text}");
                    yield break;
                }

                threadId = ExtractJsonValue(req.downloadHandler.text, "id");
                if (string.IsNullOrEmpty(threadId))
                {
                    onError?.Invoke("Thread id parse failed");
                    yield break;
                }
            }
        }

        // 3) Add Message
        {
            var url = $"{baseUrl}/threads/{threadId}/messages";
            var payload = "{\"role\":\"user\",\"content\":[{\"type\":\"text\",\"text\":\"" + EscapeJson(userQuestion) +
                          "\"}]}";
            using (var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(payload));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Authorization", "Bearer " + apiKey);
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("OpenAI-Beta", "assistants=v2");
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke($"Add message failed: {req.responseCode} {req.error}\n{req.downloadHandler.text}");
                    yield break;
                }
            }
        }

        // 4) Create Run
        string runId = null;
        {
            var url = $"{baseUrl}/threads/{threadId}/runs";
            var payload = "{\"assistant_id\":\"" + EscapeJson(assistantId) + "\"}";
            using (var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(payload));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Authorization", "Bearer " + apiKey);
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("OpenAI-Beta", "assistants=v2");
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke($"Create run failed: {req.responseCode} {req.error}\n{req.downloadHandler.text}");
                    yield break;
                }

                runId = ExtractJsonValue(req.downloadHandler.text, "id");
                if (string.IsNullOrEmpty(runId))
                {
                    onError?.Invoke("Run id parse failed");
                    yield break;
                }
            }
        }

        // 5) Poll Run until completed
        string runStatus = null;
        while (true)
        {
            var url = $"{baseUrl}/threads/{threadId}/runs/{runId}";
            using (var req = UnityWebRequest.Get(url))
            {
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Authorization", "Bearer " + apiKey);
                req.SetRequestHeader("OpenAI-Beta", "assistants=v2");
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke($"Poll run failed: {req.responseCode} {req.error}\n{req.downloadHandler.text}");
                    yield break;
                }

                runStatus = ExtractJsonValue(req.downloadHandler.text, "status");
                if (runStatus == "completed" || runStatus == "failed" || runStatus == "cancelled") break;
            }

            yield return new WaitForSeconds(1.0f);
        }

        if (runStatus != "completed")
        {
            onError?.Invoke("Run status: " + runStatus);
            yield break;
        }

        // 6) Read latest message text
        {
            var url = $"{baseUrl}/threads/{threadId}/messages?order=desc&limit=1";
            using (var req = UnityWebRequest.Get(url))
            {
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Authorization", "Bearer " + apiKey);
                req.SetRequestHeader("OpenAI-Beta", "assistants=v2");
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke(
                        $"List messages failed: {req.responseCode} {req.error}\n{req.downloadHandler.text}");
                    yield break;
                }

                var json = req.downloadHandler.text;
                var value = ExtractJsonValue(json, "value");
                onSuccess?.Invoke(!string.IsNullOrEmpty(value) ? UnescapeJson(value) : json);
            }
        }
    }

    // -----------------------------
    // Quick Ask pipeline (reuse assistant)
    // -----------------------------
    private IEnumerator QuickAskFlow()
    {
        // 1) ensure assistant id
        if (string.IsNullOrEmpty(persistedAssistantId))
        {
            if (autoCreateAssistantIfMissing && !string.IsNullOrEmpty(persistedVectorStoreId))
            {
                string aid = null;
                string err = null;
                yield return CreateAssistantBoundToVectorStore(persistedVectorStoreId,
                    id => aid = id,
                    e => err = e);
                if (!string.IsNullOrEmpty(err) || string.IsNullOrEmpty(aid))
                {
                    Debug.LogError("CreateAssistantBoundToVectorStore failed: " + err);
                    yield break;
                }

                persistedAssistantId = aid; // 保存起來（你也可同步存 PlayerPrefs）
                Debug.Log("[Assistant] created & persisted: " + persistedAssistantId);
            }
            else
            {
                Debug.LogError(
                    "No persistedAssistantId. 請先填入，或開啟 autoCreateAssistantIfMissing 並提供 persistedVectorStoreId。");
                yield break;
            }
        }

        // 2) quick ask
        string ans = null;
        string askErr = null;
        yield return QuickAsk(persistedAssistantId, string.IsNullOrEmpty(quickAskText) ? question : quickAskText,
            a => ans = a,
            e => askErr = e);
        if (!string.IsNullOrEmpty(askErr)) Debug.LogError(askErr);
        else Debug.Log("\n[QuickAsk]\n" + ans + "\n");
    }

    /// <summary>
    /// 建立一個 Assistant，直接綁定既有的 vectorStoreId
    /// </summary>
    public IEnumerator CreateAssistantBoundToVectorStore(string vectorStoreId, Action<string> onAssistantId,
        Action<string> onError)
    {
        var url = $"{baseUrl}/assistants";
        var payload = "{\"model\":\"" + EscapeJson(model) +
                      "\",\"tools\":[{\"type\":\"file_search\"}],\"tool_resources\":{\"file_search\":{\"vector_store_ids\":[\"" +
                      EscapeJson(vectorStoreId) + "\"]}}}";
        using (var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(payload));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Authorization", "Bearer " + apiKey);
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("OpenAI-Beta", "assistants=v2");
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke(
                    $"CreateAssistantBoundToVectorStore failed: {req.responseCode} {req.error}\n{req.downloadHandler.text}");
            }
            else
            {
                var json = req.downloadHandler.text;
                var id = ExtractJsonValue(json, "id");
                if (string.IsNullOrEmpty(id)) onError?.Invoke("assistant id parse failed\n" + json);
                else onAssistantId?.Invoke(id);
            }
        }
    }

    /// <summary>
    /// 僅用既有 assistantId 來問問題（不重建，不上傳）
    /// </summary>
    public IEnumerator QuickAsk(string assistantId, string userQuestion, Action<string> onSuccess,
        Action<string> onError)
    {
        if (string.IsNullOrEmpty(assistantId))
        {
            onError?.Invoke("assistantId is empty");
            yield break;
        }

        // 1) create thread
        string threadId = null;
        {
            var url = $"{baseUrl}/threads";
            using (var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes("{}"));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Authorization", "Bearer " + apiKey);
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("OpenAI-Beta", "assistants=v2");
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke(req.downloadHandler.text);
                    yield break;
                }

                threadId = ExtractJsonValue(req.downloadHandler.text, "id");
            }
        }

        // 2) add user message
        {
            var url = $"{baseUrl}/threads/{threadId}/messages";
            var payload = "{\"role\":\"user\",\"content\":[{\"type\":\"text\",\"text\":\"" + EscapeJson(userQuestion) +
                          "\"}]}";
            using (var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(payload));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Authorization", "Bearer " + apiKey);
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("OpenAI-Beta", "assistants=v2");
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke(req.downloadHandler.text);
                    yield break;
                }
            }
        }

        // 3) run
        string runId = null;
        {
            var url = $"{baseUrl}/threads/{threadId}/runs";
            var payload = "{\"assistant_id\":\"" + EscapeJson(assistantId) + "\"}";
            using (var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(payload));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Authorization", "Bearer " + apiKey);
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("OpenAI-Beta", "assistants=v2");
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke(req.downloadHandler.text);
                    yield break;
                }

                runId = ExtractJsonValue(req.downloadHandler.text, "id");
            }
        }

        // 4) poll & read latest answer
        string runStatus = null;
        while (true)
        {
            var url = $"{baseUrl}/threads/{threadId}/runs/{runId}";
            using (var req = UnityWebRequest.Get(url))
            {
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Authorization", "Bearer " + apiKey);
                req.SetRequestHeader("OpenAI-Beta", "assistants=v2");
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke(req.downloadHandler.text);
                    yield break;
                }

                runStatus = ExtractJsonValue(req.downloadHandler.text, "status");
                if (runStatus == "completed" || runStatus == "failed" || runStatus == "cancelled") break;
            }

            yield return new WaitForSeconds(1.0f);
        }

        if (runStatus != "completed")
        {
            onError?.Invoke("Run status: " + runStatus);
            yield break;
        }

        // 5) latest message
        {
            var url = $"{baseUrl}/threads/{threadId}/messages?order=desc&limit=1";
            using (var req = UnityWebRequest.Get(url))
            {
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Authorization", "Bearer " + apiKey);
                req.SetRequestHeader("OpenAI-Beta", "assistants=v2");
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke(req.downloadHandler.text);
                    yield break;
                }

                var json = req.downloadHandler.text;
                var value = ExtractJsonValue(json, "value");
                onSuccess?.Invoke(!string.IsNullOrEmpty(value) ? UnescapeJson(value) : json);
            }
        }
    }
}