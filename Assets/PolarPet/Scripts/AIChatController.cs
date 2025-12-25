using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using PolarAI.Scripts.Core.Gemini;
using PolarAI.Scripts.Core.Gemini.Model;

public class AIChatController : MonoBehaviour
{
    [Header("Refs")] public GeminiCore geminiCore;
    public DialogueBubbleUI dialogueBubble;

    [Header("Input UI")] public GameObject inputRoot; // 可整個群組的 Obj
    public InputField inputField; // Legacy UI InputField

    [Header("Settings")] public KeyCode toggleKey = KeyCode.C;

     [Header("System Prompt")]
     [TextArea(2, 6)] public string systemInstruction; // 預設系統指令，於 Inspector 設定

    private bool _showing;

    private void Awake()
    {
        if (inputRoot) inputRoot.SetActive(false);
        _showing = false;
        if (inputField)
        {
            inputField.onEndEdit.AddListener(OnEndEdit);
        }
    }

    private void OnDestroy()
    {
        if (inputField)
        {
            inputField.onEndEdit.RemoveListener(OnEndEdit);
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            ToggleInput();
        }
    }

    private void ToggleInput()
    {
        _showing = !_showing;
        if (inputRoot) inputRoot.SetActive(_showing);
        if (_showing && inputField)
        {
            inputField.text = string.Empty;
            inputField.ActivateInputField();
            inputField.Select();
        }
    }

    private void CloseInput()
    {
        _showing = false;
        if (inputRoot) inputRoot.SetActive(false);
    }

    private void OnEndEdit(string value)
    {
        // 在 Legacy InputField，按 Enter 會觸發 onEndEdit
        // 避免空字串請求
        var userText = (value ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(userText))
        {
            CloseInput();
            return;
        }

        CloseInput();
        SendPrompt(userText);
    }

    private void SendPrompt(string userText)
    {
        if (geminiCore == null)
        {
            Debug.LogWarning("GeminiCore 未綁定");
            if (dialogueBubble) dialogueBubble.SetText("[錯誤] 未設定 GeminiCore");
            return;
        }

        StartCoroutine(CallGemini(userText));
    }

    private IEnumerator CallGemini(string prompt)
    {
        string aiReply = null;
        ContentReq[] systemReqs = null;
        var sys = (systemInstruction ?? string.Empty).Trim();
        if (!string.IsNullOrEmpty(sys))
        {
            systemReqs = new[]
            {
                new ContentReq
                {
                    role = "user",
                    parts = new[] { PartReq.FromText(sys) }
                }
            };
        }

        yield return geminiCore.Chat(
            prompt,
            text => { aiReply = text; },
            null,
            systemReqs
        );

        if (dialogueBubble)
        {
            dialogueBubble.SetText(aiReply ?? "(無回覆)");
        }
    }
}


