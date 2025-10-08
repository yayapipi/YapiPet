using System;

namespace PolarAI.Scripts.Core.ComfyUI.Model
{
    [Serializable]
    public struct ComfyUIRequest
    {
        public string checkpoint; // 例如：realisticVisionV60.safetensors
        public string positivePrompt; // 例如："a cat, cinematic lighting"
        public string negativePrompt; // 例如："blurry, low quality"
        public int width; // 例如：512
        public int height; // 例如：512
        public int steps; // 例如：20
        public float cfg; // 例如：7.0f
        public string samplerName; // 例如："euler" 或 "euler_a" 等
        public string scheduler; // 例如："normal"
        public float denoise; // 例如：1.0f
        public long seed; // 例如：-1 表示隨機
        public string filenamePrefix; // 例如："UnityComfyUI"

        // 單一 LoRA（舊欄位，為了相容保留；若 loras 有值則忽略這三個）
        public string loraName;
        public float loraModelStrength;
        public float loraClipStrength;

        // 多個 LoRA（新）
        [Serializable]
        public struct LoraEntry
        {
            public string loraName;
            public float modelStrength;
            public float clipStrength;
        }

        public LoraEntry[] loras;
    }
}