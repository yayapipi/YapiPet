using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

public class DialogueBubbleUI : MonoBehaviour
{
    [Header("Follow Target")] public GameObject targetObject;
    public float verticalOffset = 0.2f; 

    [Header("Canvas & Transforms")] public Canvas canvas; 
    public RectTransform bubbleRoot; // 氣泡根節點（通常就是掛腳本的 RectTransform）
    public RectTransform backgroundRect; // 背景圖 RectTransform（Image）
    public Text text; // Legacy UnityEngine.UI.Text

    [Header("Sizing")] public Vector2 padding = new Vector2(24f, 16f); // 背景相對文本的內邊距（左右、上下）
    public float maxWidth = 420f; // 最大寬度（超過會換行）
    public float minWidth = 80f; // 最小寬度（背景不會更窄）
    public bool clampToCanvas = true; // 是否把氣泡限制在畫布可見範圍內

    public string contentDemo;
    private Camera worldCam;

    // 佈局狀態守衛，避免重入
    private bool isRefreshingLayout = false;
    private bool deferredScheduled = false;

    // 由佈局引發、用於抵銷高度變化的 Y 偏移（Canvas 本地座標）
    private float layoutYOffset = 0f;

    [Header("Visibility")] public float autoHideSeconds = 4f; // 自動隱藏秒數（<=0 表示不自動隱藏）
    private Coroutine autoHideCoroutine;

    [Header("Events")] public UnityEvent<string> onShowText; // 當顯示文字時觸發（傳遞文字）

    private void Reset()
    {
        bubbleRoot = transform as RectTransform;
        if (!canvas) canvas = GetComponentInParent<Canvas>();
        layoutYOffset = 0f;
    }

    private void Awake()
    {
        if (!canvas) canvas = GetComponentInParent<Canvas>();
        worldCam = ResolveCamera();
    }

    private void OnEnable()
    {
        layoutYOffset = 0f;
        RefreshLayout();
    }

    private void OnDisable()
    {
        deferredScheduled = false;
        isRefreshingLayout = false;
    }

    private Camera ResolveCamera()
    {
        if (!canvas) return Camera.main;
        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay) return null; // Overlay 用 null
        return canvas.worldCamera ? canvas.worldCamera : Camera.main;
    }

    private void LateUpdate()
    {
        UpdateFollow();
    }

    // 移除 TMP 事件回調

    public void SetText()
    {
        if (!text) return;
        SetText(contentDemo);
    }

    public void SetText(string newText)
    {
        if (!text) return;

        text.text = newText ?? string.Empty;
        Canvas.ForceUpdateCanvases();
        RefreshLayout();

        if (!deferredScheduled && isActiveAndEnabled)
        {
            deferredScheduled = true;
            StartCoroutine(DeferredRefreshLayout());
        }

        Show();
        ScheduleAutoHide();

        if (onShowText != null)
        {
            onShowText.Invoke(text.text);
        }
    }

    public void Show()
    {
        if (!gameObject.activeSelf) gameObject.SetActive(true);
    }

    public void Hide()
    {
        if (gameObject.activeSelf) gameObject.SetActive(false);
    }

    public void CancelAutoHide()
    {
        if (autoHideCoroutine != null)
        {
            StopCoroutine(autoHideCoroutine);
            autoHideCoroutine = null;
        }
    }

    public void ScheduleAutoHide()
    {
        CancelAutoHide();
        if (autoHideSeconds > 0f && isActiveAndEnabled)
        {
            autoHideCoroutine = StartCoroutine(AutoHideAfterDelay(autoHideSeconds));
        }
    }

    private System.Collections.IEnumerator AutoHideAfterDelay(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        Hide();
        autoHideCoroutine = null;
    }

    private System.Collections.IEnumerator DeferredRefreshLayout()
    {
        yield return new WaitForEndOfFrame();
        deferredScheduled = false;

        if (!isActiveAndEnabled || !text) yield break;

        Canvas.ForceUpdateCanvases();
        RefreshLayout();
    }

    private void UpdateFollow()
    {
        if (!targetObject || !canvas || !bubbleRoot) return;

        // 以目標的世界座標（含向上偏移）轉成螢幕座標
        Vector3 worldPos = targetObject.transform.position + Vector3.up * verticalOffset;
        Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);

        // 佈局造成的 Y 補償
        screenPos.y += layoutYOffset;

        // 設置 UI 位置（直接用螢幕座標）
        bubbleRoot.position = screenPos;
    }

    public void RefreshLayout()
    {
        if (isRefreshingLayout) return;
        if (!text || !backgroundRect || !bubbleRoot) return;

        isRefreshingLayout = true;
        try
        {
            float prevBgH = backgroundRect.rect.height;

            // 確保啟用換行（Legacy Text）
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            // 計算首選尺寸（Legacy Text）
            float availableMaxTextWidth = Mathf.Max(0f, maxWidth - padding.x * 2f);
            float minTextWidth = Mathf.Max(0f, minWidth - padding.x * 2f);

            // 先計算不受限寬度下的首選寬度
            var settingsNoWrap = text.GetGenerationSettings(Vector2.zero);
            float preferredUnconstrainedWidth = text.cachedTextGeneratorForLayout
                .GetPreferredWidth(text.text, settingsNoWrap) / text.pixelsPerUnit;

            float targetTextWidth = Mathf.Clamp(
                preferredUnconstrainedWidth,
                minTextWidth,
                availableMaxTextWidth > 0 ? availableMaxTextWidth : preferredUnconstrainedWidth
            );

            // 再用限制寬度計算對應高度
            var settingsWithWidth = text.GetGenerationSettings(new Vector2(targetTextWidth, 0f));
            float targetTextHeight = text.cachedTextGeneratorForLayout
                .GetPreferredHeight(text.text, settingsWithWidth) / text.pixelsPerUnit;
            targetTextHeight = Mathf.Max(1f, targetTextHeight);

            // 套用尺寸
            RectTransform textRect = text.rectTransform;
            textRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, targetTextWidth);
            textRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetTextHeight);

            float bgW = targetTextWidth + padding.x * 2f;
            float bgH = targetTextHeight + padding.y * 2f;

            backgroundRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, bgW);
            backgroundRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, bgH);

            // 高度變化補償位置
            float deltaH = bgH - prevBgH;
            if (Mathf.Abs(deltaH) > Mathf.Epsilon)
            {
                float pivotY = bubbleRoot.pivot.y;
                layoutYOffset += deltaH * pivotY;
            }

            // 保守推進一次（避免頻繁重建）
            Canvas.ForceUpdateCanvases();
        }
        finally
        {
            isRefreshingLayout = false;
        }
    }
}