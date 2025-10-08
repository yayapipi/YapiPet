using System;
using UnityEngine;
using PolarAI.Scripts.AICore.ElevenLabs;

[RequireComponent(typeof(AudioSource))]
public class BubbleTTSSpeaker : MonoBehaviour
{
    [Header("Refs")] public DialogueBubbleUI bubbleUI;
    public ElevenLabsCore elevenLabs;

    [Header("ElevenLabs Config")] public string apiKey;
    public string voiceId;

    [Header("Playback")] public bool playOnShow = true;
    public bool stopPreviousBeforePlay = true;

    private AudioSource _audio;

    private void Awake()
    {
        _audio = GetComponent<AudioSource>();
        if (_audio)
        {
            _audio.playOnAwake = false;
        }

        // 若未綁定，建立一個純程式 ElevenLabsCore 實例
        if (elevenLabs == null)
        {
            elevenLabs = new ElevenLabsCore();
        }

        // 以 Inspector 設定覆寫初始化
        if (!string.IsNullOrWhiteSpace(apiKey) && !string.IsNullOrWhiteSpace(voiceId))
        {
            elevenLabs.Initialize(apiKey, voiceId);
        }
    }

    private void OnEnable()
    {
        if (bubbleUI && bubbleUI.onShowText != null)
        {
            bubbleUI.onShowText.AddListener(OnBubbleShowText);
        }
    }

    private void OnDisable()
    {
        if (bubbleUI && bubbleUI.onShowText != null)
        {
            bubbleUI.onShowText.RemoveListener(OnBubbleShowText);
        }
    }

    private void OnBubbleShowText(string text)
    {
        if (!playOnShow) return;
        if (elevenLabs == null)
        {
            Debug.LogWarning("ElevenLabsCore 未綁定");
            return;
        }

        if (stopPreviousBeforePlay && _audio && _audio.isPlaying)
        {
            _audio.Stop();
        }

        elevenLabs.TextToSound(text, OnTTSDone);
    }

    private void OnTTSDone(AudioClip clip, bool ok)
    {
        if (!ok || clip == null)
        {
            Debug.LogWarning("TTS 失敗或回傳空音軌");
            return;
        }

        if (_audio)
        {
            _audio.clip = clip;
            _audio.Play();
        }
    }
}


