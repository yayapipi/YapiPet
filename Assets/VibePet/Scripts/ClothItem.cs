using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using PolarAI.Scripts.Core.Gemini;

// 可掛在 Prefab 上，支援場景拖拽與放到寵物身上以換衣
public class ClothItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Refs")] public GeminiCore geminiCore;
    public Transform petRoot; // 寵物根節點（用於禁止移動/抓 Animator）
    public Animator petAnimator;
    public SpriteRenderer petSpriteRenderer; // 寵物外觀（將被替換）
		public GameObject loadingRoot; // 換衣期間顯示的 Loading 物件

		[Header("Animator")] public string idleStateName = "Idle"; // 換衣前先切到的狀態

    [Header("Assets URL")] public string bearImageUrl; // 北極熊圖片 URL
    public string clothImageUrl; // 衣服圖片 URL

    [Header("Assets Sprite (可選)")]
    public Sprite bearSprite; // 若提供則優先於 URL
    public Sprite clothSprite;

    [Header("Behavior")] public float wearDurationSeconds = 10f; // 換完衣服後多久消失/恢復

    private Vector3 _startPos;
    private bool _dragging;

    private void Awake()
    {
        _startPos = transform.position;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        _dragging = true;
    }

    public void OnDrag(PointerEventData eventData)
    {
        // 以世界空間拖拽（簡單示例，可依 UI/3D 調整）
        var world = Camera.main.ScreenToWorldPoint(new Vector3(eventData.position.x, eventData.position.y, Mathf.Abs(Camera.main.transform.position.z)));
        world.z = _startPos.z;
        transform.position = world;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        _dragging = false;
        // 簡單距離判斷：若靠近寵物即觸發換衣
        if (petRoot && Vector3.Distance(transform.position, petRoot.position) < 1.0f)
        {
            StartCoroutine(BeginWearFlow());
        }
        else
        {
            // 回到原位
            transform.position = _startPos;
        }
    }

    private IEnumerator BeginWearFlow()
    {
        if (geminiCore == null || petSpriteRenderer == null)
        {
            Debug.LogWarning("GeminiCore 或 SpriteRenderer 未設定");
            yield break;
        }


		// 關閉移動與 Animator：先切到 Idle 再停用
		bool animatorWasEnabled = petAnimator && petAnimator.enabled;
		if (petAnimator)
		{
			try
			{
				if (!string.IsNullOrEmpty(idleStateName))
				{
					petAnimator.Play(idleStateName, 0, 0f);
					petAnimator.Update(0f); // 立即更新到該姿勢
				}
			}
			catch { /* 忽略未知狀態名 */ }
			petAnimator.enabled = false;
		}

		// 顯示 Loading
		if (loadingRoot) loadingRoot.SetActive(true);

        // 準備兩張圖片並轉 Base64（優先使用 Sprite，否則用 URL 下載）
        var base64List = new List<string>();
        // Bear
        if (bearSprite)
        {
            var b64 = SpriteToBase64(bearSprite);
            if (!string.IsNullOrEmpty(b64)) base64List.Add(b64);
        }
        else
        {
            yield return StartCoroutine(DownloadImageToBase64(bearImageUrl, base64 =>
            {
                if (!string.IsNullOrEmpty(base64)) base64List.Add(base64);
            }));
        }
        // Cloth
        if (clothSprite)
        {
            var b64 = SpriteToBase64(clothSprite);
            if (!string.IsNullOrEmpty(b64)) base64List.Add(b64);
        }
        else
        {
            yield return StartCoroutine(DownloadImageToBase64(clothImageUrl, base64 =>
            {
                if (!string.IsNullOrEmpty(base64)) base64List.Add(base64);
            }));
        }

		if (base64List.Count == 0)
        {
            Debug.LogWarning("下載圖片失敗，無法換衣");
			if (loadingRoot) loadingRoot.SetActive(false);
            if (petAnimator) petAnimator.enabled = animatorWasEnabled;
            yield break;
        }

        Texture2D editedTex = null;
        yield return geminiCore.EditImagesAsync(base64List, "Dress the bear with the provided cloth, produce a clean full-body sprite.", tex =>
        {
            editedTex = tex;
        });

		if (editedTex != null)
        {
            // 替換 Sprite
            var newSprite = TextureToSprite(editedTex);
            if (newSprite != null)
            {
                petSpriteRenderer.sprite = newSprite;
            }
        }

		// 關閉 Loading
		if (loadingRoot) loadingRoot.SetActive(false);

        // 道具消失，延遲後恢復 Animator
        gameObject.SetActive(false);
        yield return new WaitForSeconds(Mathf.Max(0f, wearDurationSeconds));
        if (petAnimator) petAnimator.enabled = true;
    }

    private IEnumerator DownloadImageToBase64(string url, Action<string> onDone)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            onDone?.Invoke(null);
            yield break;
        }

        using var req = UnityWebRequest.Get(url);
        yield return req.SendWebRequest();
        if (req.result != UnityWebRequest.Result.Success)
        {
            onDone?.Invoke(null);
            yield break;
        }
        var bytes = req.downloadHandler.data;
        onDone?.Invoke(Convert.ToBase64String(bytes));
    }

    private static Sprite TextureToSprite(Texture2D tex)
    {
        if (tex == null) return null;
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
    }

    private static string SpriteToBase64(Sprite sprite)
    {
        if (sprite == null || sprite.texture == null) return null;
        Texture2D cropped = null;
        try
        {
            cropped = ExtractSpriteTexture(sprite);
            if (cropped == null) return null;
            var bytes = cropped.EncodeToPNG();
            return Convert.ToBase64String(bytes);
        }
        catch
        {
            return null;
        }
        finally
        {
            if (cropped != null)
            {
                UnityEngine.Object.Destroy(cropped);
            }
        }
    }

    private static Texture2D ExtractSpriteTexture(Sprite sprite)
    {
        var src = sprite.texture;
        int fullW = src.width;
        int fullH = src.height;

        var rt = RenderTexture.GetTemporary(fullW, fullH, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        var prev = RenderTexture.active;
        try
        {
            Graphics.Blit(src, rt);
            RenderTexture.active = rt;
            var fullTex = new Texture2D(fullW, fullH, TextureFormat.RGBA32, false, true);
            fullTex.ReadPixels(new Rect(0, 0, fullW, fullH), 0, 0);
            fullTex.Apply();

            var r = sprite.textureRect;
            int x = Mathf.RoundToInt(r.x);
            int y = Mathf.RoundToInt(r.y);
            int w = Mathf.RoundToInt(r.width);
            int h = Mathf.RoundToInt(r.height);

            var pixels = fullTex.GetPixels(x, y, w, h);
            var cropped = new Texture2D(w, h, TextureFormat.RGBA32, false);
            cropped.SetPixels(pixels);
            cropped.Apply();

            UnityEngine.Object.Destroy(fullTex);
            return cropped;
        }
        catch
        {
            return null;
        }
        finally
        {
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
        }
    }
}


