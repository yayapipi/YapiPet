// FalAIDemo_Newtonsoft.cs
// 單一腳本：Inspector 拖入 Buttons / InputFields / RawImage，即可測各模型
// 需安裝 com.unity.nuget.newtonsoft-json
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TMPro;

public class FalAIDemo_Newtonsoft : MonoBehaviour
{
    [Header("Fal API")]
    [Tooltip("在伺服器/Editor 設定，勿打包到行動端/前端")]
    public string falApiKey = "PASTE_YOUR_FAL_KEY_HERE";
    [Tooltip("Fal 同步端點前綴，不用改")]
    public string falBaseUrl = "https://fal.run/";

    [Header("UI - Common Inputs")]
    public TMP_InputField promptInput;       // 文字提示
    public InputField imageUrlInput;     // 影像/貼圖 URL
    public Image previewImage;        // 顯示回傳影像（若有）
    public Text statusText;              // 顯示狀態/錯誤訊息

    [Header("UI - Buttons (拖進來)")]
    public Button btnFastLightningSDXL;
    public Button btnTripo3D;
    public Button btnFlux;
    public Button btnGeminiEdit;
    public Button btnAuraSR;
    public Button btnKlingImageToVideo;
    public Button btnWanEffects;
    public Button btnAnyLLM;
    public Button btnElevenlabsSFX;
    public Button btnCassetteMusic;
    public Button btnSonautoMusic;
    public Button btnImagen4Fast;
    public Button btnGeminiGenerate;
    public Button btnAnyLLMVision;

    // === 推薦端點常數（可依需要調整） ===
    const string END_FAST_LIGHTNING_SDXL = "fal-ai/fast-lightning-sdxl";
    const string END_TRIPO_3D            = "tripo3d/tripo/v2.5/image-to-3d";
    const string END_FLUX_DEV            = "fal-ai/flux/dev";
    const string END_GEMINI_EDIT         = "fal-ai/gemini-25-flash-image/edit";
    const string END_AURA_SR             = "fal-ai/aura-sr";
    // * 注意：使用 v2.1/master；若你工作區仍是 v2/master，可把下行改為 v2/master
    const string END_KLING_I2V           = "fal-ai/kling-video/v2.1/master/image-to-video";
    const string END_WAN_EFFECTS         = "fal-ai/wan-effects";
    const string END_ANY_LLM             = "fal-ai/any-llm";
    const string END_ELEVENLABS_SFX      = "fal-ai/elevenlabs/sound-effects/v2";
    const string END_CASSETTE_MUSIC      = "cassetteai/music-generator";
    const string END_SONAUTO_MUSIC       = "sonauto/v2/text-to-music";
    const string END_IMAGEN4_FAST        = "fal-ai/imagen4/preview/fast";
    const string END_GEMINI_GEN          = "fal-ai/gemini-25-flash-image";
    const string END_ANY_LLM_VISION      = "fal-ai/any-llm/vision";

    void Awake()
    {
        // 綁定按鈕（Inspector 可只拖想測的）
        SafeBind(btnFastLightningSDXL, () => RunFastLightningSDXL());
        SafeBind(btnTripo3D,            () => RunTripo3D());
        SafeBind(btnFlux,               () => RunFlux());
        SafeBind(btnGeminiEdit,         () => RunGeminiEdit());
        SafeBind(btnAuraSR,             () => RunAuraSR());
        SafeBind(btnKlingImageToVideo,  () => RunKlingImageToVideo());
        SafeBind(btnWanEffects,         () => RunWanEffects());
        SafeBind(btnAnyLLM,             () => RunAnyLLM());
        SafeBind(btnElevenlabsSFX,      () => RunElevenlabsSFX());
        SafeBind(btnCassetteMusic,      () => RunCassetteMusic());
        SafeBind(btnSonautoMusic,       () => RunSonautoMusic());
        SafeBind(btnImagen4Fast,        () => RunImagen4Fast());
        SafeBind(btnGeminiGenerate,     () => RunGeminiGenerate());
        SafeBind(btnAnyLLMVision,       () => RunAnyLLMVision());
    }

    void SafeBind(Button b, Action act)
    {
        if (b != null) b.onClick.AddListener(() => act?.Invoke());
    }

    // ===================== 範例呼叫 =====================

    // 1. Fast Lightning SDXL（Text->Image）
    public void RunFastLightningSDXL()
    {
        var prompt = GetPromptOrDefault("a cinematic portrait of a hero under neon rain, ultra-detailed");
        var body = new Dictionary<string, object> {
            { "prompt", prompt }
        };
        StartCoroutine(CallFal(END_FAST_LIGHTNING_SDXL, body, HandleResultGeneric));
    }

    // 2. Tripo3D: Image->3D
    public void RunTripo3D()
    {
        var img = GetImageOrDefault("https://storage.googleapis.com/falserverless/web-examples/wan-effects/cat.jpg");
        var body = new Dictionary<string, object> {
            { "image_url", img }
        };
        StartCoroutine(CallFal(END_TRIPO_3D, body, HandleResultGeneric));
    }

    // 3. FLUX 系列（示例：flux/dev，Text->Image）
    public void RunFlux()
    {
        var prompt = GetPromptOrDefault("award-winning photo of a rhino in suit at a bar, 85mm bokeh");
        var body = new Dictionary<string, object> {
            { "prompt", prompt }
        };
        StartCoroutine(CallFal(END_FLUX_DEV, body, HandleResultGeneric));
    }

    // 4. Gemini 2.5 Flash Image - 編輯（Image Edit）
    public void RunGeminiEdit()
    {
        var img = GetImageOrDefault("https://storage.googleapis.com/falserverless/example_inputs/nano-banana-edit-input.png");
        var body = new Dictionary<string, object> {
            { "prompt", GetPromptOrDefault("replace background with cyberpunk city lights") },
            { "image_urls", new List<string>{ img } }
        };
        StartCoroutine(CallFal(END_GEMINI_EDIT, body, HandleResultGeneric));
    }

    // 5. Aura-SR（Upscale）
    public void RunAuraSR()
    {
        var img = GetImageOrDefault("https://v3.fal.media/files/rabbit/YYbm6L3DaXYHDL1_A4OaL.jpeg");
        var body = new Dictionary<string, object> {
            { "image_url", img },
            { "upscaling_factor", 4 },
            { "overlapping_tiles", true },
            { "checkpoint", "v2" }
        };
        StartCoroutine(CallFal(END_AURA_SR, body, HandleResultGeneric));
    }

    // 6. Kling - Image to Video
    public void RunKlingImageToVideo()
    {
        var img = GetImageOrDefault("https://storage.googleapis.com/falserverless/kling/kling_input.jpeg");
        var body = new Dictionary<string, object> {
            { "prompt", GetPromptOrDefault("snowflakes fall as a car moves forward along the road") },
            { "image_url", img }
        };
        StartCoroutine(CallFal(END_KLING_I2V, body, HandleResultGeneric));
    }

    // 7. Wan Effects（Image->Video with effects）
    public void RunWanEffects()
    {
        var img = GetImageOrDefault("https://storage.googleapis.com/falserverless/web-examples/wan-effects/cat.jpg");
        var body = new Dictionary<string, object> {
            { "subject", GetPromptOrDefault("a cute kitten wearing sunglasses, hip-hop vibe") },
            { "image_url", img }
        };
        StartCoroutine(CallFal(END_WAN_EFFECTS, body, HandleResultGeneric));
    }

    // 8. Any LLM（文字 LLM）
    public void RunAnyLLM()
    {
        var prompt = GetPromptOrDefault("Give me 3 eerie quest ideas for a rogue-like dungeon crawler.");
        var body = new Dictionary<string, object> {
            // model 可省略使用預設，或自行切換 openai/gpt-4o-mini、anthropic/claude-3.5-sonnet 等
            { "model", "google/gemini-2.0-flash-001" },
            { "prompt", prompt },
            { "temperature", 0.7f },
            { "max_tokens", 300 }
        };
        StartCoroutine(CallFal(END_ANY_LLM, body, HandleResultGeneric));
    }

    // 9. ElevenLabs Sound Effects V2（Text->SFX）
    public void RunElevenlabsSFX()
    {
        var body = new Dictionary<string, object> {
            // 有些端點鍵名是 text，保險起見同時帶上 prompt / text（伺服器會取用需要的）
            { "text", GetPromptOrDefault("wind chimes in a haunted temple, stereo, 6 seconds") },
            { "prompt", GetPromptOrDefault("wind chimes in a haunted temple, stereo, 6 seconds") }
        };
        StartCoroutine(CallFal(END_ELEVENLABS_SFX, body, HandleResultGeneric));
    }

    // 10. CassetteAI Music Generator（Text->Music）
    public void RunCassetteMusic()
    {
        var body = new Dictionary<string, object> {
            { "prompt", GetPromptOrDefault("dark cyberpunk synthwave, driving bassline, 120bpm, 30 seconds") }
        };
        StartCoroutine(CallFal(END_CASSETTE_MUSIC, body, HandleResultGeneric));
    }

    // 11. Sonauto V2 Text-to-Music
    public void RunSonautoMusic()
    {
        var body = new Dictionary<string, object> {
            { "prompt", GetPromptOrDefault("epic orchestral trailer with choirs and taiko drums, 20 seconds") }
            // 也支援 lyrics_prompt / tags 等，需時可自行擴充
        };
        StartCoroutine(CallFal(END_SONAUTO_MUSIC, body, HandleResultGeneric));
    }

    // 12. Imagen 4 Fast（Text->Image）
    public void RunImagen4Fast()
    {
        var body = new Dictionary<string, object> {
            { "prompt", GetPromptOrDefault("retro 1960s kitchen product shot, warm soft sunlight, shallow DOF") }
        };
        StartCoroutine(CallFal(END_IMAGEN4_FAST, body, HandleResultGeneric));
    }

    // 13. Gemini 2.5 Flash Image（Text->Image）
    public void RunGeminiGenerate()
    {
        var body = new Dictionary<string, object> {
            { "prompt", GetPromptOrDefault("an explorer robot walking through neon cherry blossoms at night") },
            { "num_images", 1 },
            { "output_format", "jpeg" }
        };
        StartCoroutine(CallFal(END_GEMINI_GEN, body, HandleResultGeneric));
    }

    // 14. Any LLM Vision（Vision LLM）
    public void RunAnyLLMVision()
    {
        var img = GetImageOrDefault("https://fal.media/files/tiger/4Ew1xYW6oZCs6STQVC7V8_86440216d0fe42e4b826d03a2121468e.jpg");
        var body = new Dictionary<string, object> {
            { "prompt", GetPromptOrDefault("Caption this image for a text-to-image model with as much detail as possible.") },
            { "model", "google/gemini-flash-1.5" },
            { "image_urls", new List<string>{ img } },
            { "temperature", 0.2f },
            { "max_tokens", 200 }
        };
        StartCoroutine(CallFal(END_ANY_LLM_VISION, body, HandleResultGeneric));
    }

    // ===================== Fal 呼叫核心 =====================

    IEnumerator CallFal(string endpoint, Dictionary<string, object> body, Action<string> onSuccess)
    {
        if (string.IsNullOrEmpty(falApiKey))
        {
            SetStatus("❌ 請先設定 falApiKey");
            yield break;
        }

        var url = CombineUrl(falBaseUrl, endpoint);
        string json = JObject.FromObject(body).ToString(Formatting.None);
        byte[] payload = Encoding.UTF8.GetBytes(json);

        using (var req = new UnityWebRequest(url, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(payload);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Accept", "application/json");
            req.SetRequestHeader("Authorization", $"Key {falApiKey}");

            SetStatus($"➡️ POST {endpoint} ...");
            yield return req.SendWebRequest();

#if UNITY_2020_2_OR_NEWER
            if (req.result != UnityWebRequest.Result.Success)
#else
            if (req.isNetworkError || req.isHttpError)
#endif
            {
                SetStatus($"❌ {endpoint} 錯誤：{req.responseCode} {req.error}\n{req.downloadHandler.text}");
                yield break;
            }

            var text = req.downloadHandler.text;
            onSuccess?.Invoke(text);
        }
    }

    void HandleResultGeneric(string json)
    {
        try
        {
            // 使用 Newtonsoft 解析
            var token = JToken.Parse(json);

            // 把可能的檔案 URL 都撈出來（image / video / audio / mesh…）
            var urls = ExtractAllUrls(token).ToList();

            // 顯示第一個可用影像
            var imgUrl = urls.FirstOrDefault(IsImageUrl);
            if (!string.IsNullOrEmpty(imgUrl))
            {
                StartCoroutine(LoadTextureToRawImage(imgUrl));
            }

            // 顯示摘要
            string summary = SummarizeFields(token, urls);
            SetStatus($"✅ 完成\n{summary}");
        }
        catch (Exception e)
        {
            SetStatus("⚠️ 解析回應失敗（JSON）：\n" + e);
        }
    }

    // ===================== 工具方法 =====================

    string GetPromptOrDefault(string def) =>
        (promptInput != null && !string.IsNullOrWhiteSpace(promptInput.text)) ? promptInput.text : def;

    string GetImageOrDefault(string def) =>
        (imageUrlInput != null && !string.IsNullOrWhiteSpace(imageUrlInput.text)) ? imageUrlInput.text : def;

    string CombineUrl(string baseUrl, string path)
    {
        if (baseUrl.EndsWith("/")) baseUrl = baseUrl.TrimEnd('/');
        return $"{baseUrl}/{path}";
    }

    IEnumerable<string> ExtractAllUrls(JToken token)
    {
        // 1) 任何名為 "url" 的字串欄位
        foreach (var jt in token.DescendantsAndSelf())
        {
            if (jt.Type == JTokenType.Property)
            {
                var prop = (JProperty)jt;
                if (prop.Name.Equals("url", StringComparison.OrdinalIgnoreCase)
                    && prop.Value.Type == JTokenType.String)
                {
                    var u = prop.Value.ToString();
                    if (IsLikelyFileUrl(u)) yield return u;
                }
            }
            else if (jt.Type == JTokenType.String)
            {
                var s = jt.ToString();
                if (IsLikelyFileUrl(s)) yield return s;
            }
        }
    }

    bool IsLikelyFileUrl(string u)
    {
        if (string.IsNullOrEmpty(u)) return false;
        // 常見回傳副檔名
        string[] exts = { ".png", ".jpg", ".jpeg", ".webp", ".gif", ".bmp",
                          ".mp4", ".mov", ".webm",
                          ".mp3", ".wav", ".flac", ".m4a",
                          ".obj", ".glb", ".gltf", ".fbx" };
        string lower = u.ToLowerInvariant();
        return lower.StartsWith("http") && exts.Any(e => lower.Contains(e));
    }

    bool IsImageUrl(string u)
    {
        string[] exts = { ".png", ".jpg", ".jpeg", ".webp", ".gif", ".bmp" };
        string lower = u.ToLowerInvariant();
        return exts.Any(e => lower.Contains(e));
    }

    IEnumerator LoadTextureToRawImage(string url)
    {
        using (var req = UnityWebRequestTexture.GetTexture(url))
        {
            yield return req.SendWebRequest();
#if UNITY_2020_2_OR_NEWER
            if (req.result != UnityWebRequest.Result.Success)
#else
            if (req.isNetworkError || req.isHttpError)
#endif
            {
                SetStatus($"⚠️ 載入圖片失敗：{req.error}\n{url}");
                yield break;
            }

            var tex = DownloadHandlerTexture.GetContent(req);
            if (previewImage != null) previewImage.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.zero);
            SetStatus($"🖼️ 已載入影像：{tex.width}x{tex.height}");
        }
    }

    string SummarizeFields(JToken token, List<string> urls)
    {
        var sb = new StringBuilder();

        // 嘗試抓常見欄位
        JToken images = token.SelectToken("images");
        JToken video = token.SelectToken("video");
        JToken audio = token.SelectToken("audio");
        JToken mesh  = token.SelectToken("mesh") ?? token.SelectToken("model");

        if (images != null) sb.AppendLine($"images: {images.Count()} 個");
        if (video  != null) sb.AppendLine("video: 有");
        if (audio  != null) sb.AppendLine("audio: 有");
        if (mesh   != null) sb.AppendLine("3D: 有");

        if (token.SelectToken("output") != null)
        {
            string txt = token.SelectToken("output").ToString();
            if (!string.IsNullOrEmpty(txt))
            {
                sb.AppendLine("--- output ---");
                sb.AppendLine(Truncate(txt, 500));
            }
        }

        if (urls != null && urls.Count > 0)
        {
            sb.AppendLine("--- urls ---");
            foreach (var u in urls.Take(6)) sb.AppendLine(u);
            if (urls.Count > 6) sb.AppendLine($"(其餘 {urls.Count - 6} 個省略)");
        }

        // 有些模型會給 description（如 Gemini Edit）
        var desc = token.SelectToken("description")?.ToString();
        if (!string.IsNullOrEmpty(desc))
        {
            sb.AppendLine("--- description ---");
            sb.AppendLine(Truncate(desc, 500));
        }

        return sb.ToString().TrimEnd();
    }

    string Truncate(string s, int max)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return s.Length <= max ? s : s.Substring(0, max) + "...";
    }

    void SetStatus(string msg)
    {
        Debug.Log(msg);
        if (statusText != null) statusText.text = msg;
    }
}

// ========== 小工具：JToken 遞迴 ==========
public static class JTokenExtensions
{
    public static IEnumerable<JToken> DescendantsAndSelf(this JToken node)
    {
        yield return node;
        foreach (var child in node.Children())
        {
            foreach (var desc in child.DescendantsAndSelf())
                yield return desc;
        }
    }
}
