using System;
using System.Collections.Generic;
using PolarAI.Scripts.Core.ComfyUI.Model;
using UnityEngine;
using UnityEngine.UI;

namespace PolarAI.Scripts.Core.ComfyUI.Example
{
    public class ComfyUIExample : MonoBehaviour
    {
        public ComfyUICore comfyUICore;

        public RawImage previewImage;

        [Header("UI Button")] 
        public Button generateButton;
        public Button cancelButton;
        public InputField promptInput;

        [Header("ComfyUI Parameters")] 
        [Tooltip("ComfyUI 中可用的模型檔名，例如：realisticVisionV60.safetensors")]
        public string checkpoint = "your_model.safetensors";

        [TextArea(2, 6)] public string positivePrompt = "a cat, cinematic lighting";
        [TextArea(2, 6)] public string negativePrompt = "blurry, low quality";

        public int width = 512;
        public int height = 512;
        public int steps = 20;
        public float cfg = 7f;
        public string samplerName = "euler";
        public string scheduler = "normal";
        [Range(0f, 1f)] public float denoise = 1.0f;
        public long seed = -1;
        public string filenamePrefix = "PolarComfyUI";

        [Serializable]
        public class LoraConfig
        {
            public string loraName = "example_lora.safetensors";
            [Range(0f, 2f)] public float modelStrength = 1.0f;
            [Range(0f, 2f)] public float clipStrength = 1.0f;
        }

        public LoraConfig[] loras = Array.Empty<LoraConfig>();

        private Coroutine _running;

        private void Reset()
        {
            loras = new[]
            {
                new LoraConfig
                {
                    loraName = "example_lora.safetensors",
                    modelStrength = 1.0f,
                    clipStrength = 1.0f
                }
            };
        }

        private void OnEnable()
        {
            if (generateButton != null) generateButton.onClick.AddListener(Generate);
            if (cancelButton != null) cancelButton.onClick.AddListener(CancelIfRunning);
            if (promptInput != null) promptInput.onValueChanged.AddListener(v => positivePrompt = v);
        }

        private void OnDisable()
        {
            if (generateButton != null) generateButton.onClick.RemoveListener(Generate);
            if (cancelButton != null) cancelButton.onClick.RemoveListener(CancelIfRunning);
            if (promptInput != null) promptInput.onValueChanged.RemoveListener(v => positivePrompt = v);
        }

        private void OnValidate()
        {
            width = Mathf.Clamp(width, 64, 4096);
            height = Mathf.Clamp(height, 64, 4096);
            steps = Mathf.Clamp(steps, 1, 200);
            cfg = Mathf.Clamp(cfg, 0f, 50f);
            denoise = Mathf.Clamp01(denoise);
            if (string.IsNullOrWhiteSpace(filenamePrefix)) filenamePrefix = "PolarComfyUI";
            if (loras != null)
            {
                foreach (var l in loras)
                {
                    if (l == null) continue;
                    l.modelStrength = Mathf.Clamp(l.modelStrength, 0f, 2f);
                    l.clipStrength = Mathf.Clamp(l.clipStrength, 0f, 2f);
                }
            }
        }

        public void Generate()
        {
            if (comfyUICore == null)
            {
                SetStatus("請先指定 ComfyUIClient。");
                return;
            }

            if (_running != null)
            {
                SetStatus("已有任務進行中，請稍候...");
                return;
            }

            var req = new ComfyUIRequest
            {
                checkpoint = checkpoint?.Trim() ?? "",
                positivePrompt = positivePrompt ?? "",
                negativePrompt = negativePrompt ?? "",
                width = width,
                height = height,
                steps = steps,
                cfg = cfg,
                samplerName = string.IsNullOrWhiteSpace(samplerName) ? "euler" : samplerName.Trim(),
                scheduler = string.IsNullOrWhiteSpace(scheduler) ? "normal" : scheduler.Trim(),
                denoise = denoise,
                seed = seed,
                filenamePrefix = string.IsNullOrWhiteSpace(filenamePrefix) ? "UnityComfyUI" : filenamePrefix.Trim(),
                loras = BuildLoraEntries()
            };

            SetStatus("提交生成中...");
            _running = StartCoroutine(comfyUICore.GenerateImageCoroutine(
                req,
                onSuccess: tex =>
                {
                    _running = null;
                    if (previewImage != null) previewImage.texture = tex;
                    SetStatus("完成！");
                },
                onError: err =>
                {
                    _running = null;
                    SetStatus("失敗：" + err);
                },
                onProgress: (p, msg) => { SetStatus($"進度：{Mathf.RoundToInt(p * 100f)}% {msg}"); }
            ));
        }

        private ComfyUIRequest.LoraEntry[] BuildLoraEntries()
        {
            var list = new List<ComfyUIRequest.LoraEntry>();
            if (loras != null)
            {
                foreach (var l in loras)
                {
                    if (l == null) continue;
                    if (string.IsNullOrWhiteSpace(l.loraName)) continue;
                    list.Add(new ComfyUIRequest.LoraEntry
                    {
                        loraName = l.loraName.Trim(),
                        modelStrength = Mathf.Clamp(l.modelStrength, 0f, 2f),
                        clipStrength = Mathf.Clamp(l.clipStrength, 0f, 2f)
                    });
                }
            }

            return list.ToArray();
        }

        public void CancelIfRunning()
        {
            if (_running != null)
            {
                comfyUICore?.CancelCurrent();
                StopCoroutine(_running);
                _running = null;
                SetStatus("已取消當前任務。");
            }
        }

        private void SetStatus(string msg)
        {
            Debug.Log(msg);
        }
    }
}