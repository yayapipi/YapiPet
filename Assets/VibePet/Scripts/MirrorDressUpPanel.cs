using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using PolarAI.Scripts.Core.Gemini;

// 換裝面板：顯示北極熊、三件衣服按鈕、預覽與 Loading，並可關閉
public class MirrorDressUpPanel : MonoBehaviour
{
    [Header("Core")]
    public GeminiCore geminiCore;

    [Header("Sprites - 來源素材")]
    public Sprite bearSprite; // 北極熊原圖
    public Sprite clothSpriteA;
    public Sprite clothSpriteB;
    public Sprite clothSpriteC;

    [Header("UI 綁定")]
    public Button btnClothA;
    public Button btnClothB;
    public Button btnClothC;
    public Button btnClose;
    public Image previewImage; // 僅在 UI Image 上顯示生成結果
    public GameObject loadingRoot; // UI Loading 物件

    [Header("指令設定")]
    [TextArea]
    public string dressInstruction = "Dress the bear with the provided cloth, produce a clean full-body sprite.";

    private void Awake()
    {
        if (btnClothA) btnClothA.onClick.AddListener(() => OnClickCloth(clothSpriteA));
        if (btnClothB) btnClothB.onClick.AddListener(() => OnClickCloth(clothSpriteB));
        if (btnClothC) btnClothC.onClick.AddListener(() => OnClickCloth(clothSpriteC));
        if (btnClose) btnClose.onClick.AddListener(ClosePanel);
    }

    private void OnEnable()
    {
        if (loadingRoot) loadingRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        if (btnClothA) btnClothA.onClick.RemoveAllListeners();
        if (btnClothB) btnClothB.onClick.RemoveAllListeners();
        if (btnClothC) btnClothC.onClick.RemoveAllListeners();
        if (btnClose) btnClose.onClick.RemoveAllListeners();
    }

    public void ClosePanel()
    {
        if (loadingRoot) loadingRoot.SetActive(false);
        gameObject.SetActive(false);
        if (Time.timeScale == 0f) Time.timeScale = 1f;
    }

    private void OnClickCloth(Sprite clothSprite)
    {
        if (geminiCore == null || bearSprite == null || clothSprite == null) return;
        if (loadingRoot) loadingRoot.SetActive(true);

        var base64List = new List<string>();
        var b64Bear = SpriteToBase64(bearSprite);
        var b64Cloth = SpriteToBase64(clothSprite);
        if (!string.IsNullOrEmpty(b64Bear)) base64List.Add(b64Bear);
        if (!string.IsNullOrEmpty(b64Cloth)) base64List.Add(b64Cloth);

        geminiCore.EditImagesAsync(base64List, dressInstruction, OnImageReturn);
    }

    private void OnImageReturn(Texture2D tex)
    {
        if (tex != null && previewImage)
        {
            var sprite = TextureToSprite(tex);
            if (sprite != null) previewImage.sprite = sprite;
        }
        if (loadingRoot) loadingRoot.SetActive(false);
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

    private static Sprite TextureToSprite(Texture2D tex)
    {
        if (tex == null) return null;
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
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


