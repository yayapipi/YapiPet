using System;
using System.Collections;
using Core;
using UnityEngine;
using UnityEngine.Networking;

namespace YFrame.Runtime.Utility
{
    public static class SpritesUtility
    {
        public static byte[] ToBytes(this Sprite sprite)
        {
            return sprite.texture.EncodeToPNG();
        }

        public static string ToBase64DataUri(this Sprite sprite)
        {
            Texture2D texture = sprite.texture;
            Rect spriteRect = sprite.rect;
            Texture2D croppedTexture = new Texture2D((int)spriteRect.width, (int)spriteRect.height);
            Color[] pixels = texture.GetPixels((int)spriteRect.x, (int)spriteRect.y, (int)spriteRect.width,
                (int)spriteRect.height);
            croppedTexture.SetPixels(pixels);
            croppedTexture.Apply();

            byte[] imageBytes = croppedTexture.EncodeToPNG();
            string base64String = Convert.ToBase64String(imageBytes);
            string base64DataUri = $"data:image/png;base64,{base64String}";
            return base64DataUri;
        }

        public static Sprite ToSprite(this byte[] bytes)
        {
            var texture = new Texture2D(10, 10);
            texture.LoadImage(bytes);
            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.zero);
        }

        public static void DownloadImageFromURL(string imageURL, System.Action<byte[]> onImageDownloaded)
        {
            CoroutineManager.Instance.StartCoroutine(DownloadImageFromURLCoroutine(imageURL, onImageDownloaded));
        }

        private static IEnumerator DownloadImageFromURLCoroutine(string imageURL,
            System.Action<byte[]> onImageDownloaded)
        {
            string cacheBusterURL = imageURL + (imageURL.Contains("?") ? "&" : "?") + "nocache=" +
                                    System.DateTime.Now.Ticks;

            using UnityWebRequest webRequest = UnityWebRequestTexture.GetTexture(cacheBusterURL);
            webRequest.SetRequestHeader("Cache-Control", "no-cache, no-store, must-revalidate");
            webRequest.SetRequestHeader("Pragma", "no-cache");
            webRequest.SetRequestHeader("Expires", "0");

            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                byte[] imageData = ((DownloadHandlerTexture)webRequest.downloadHandler).data;
                onImageDownloaded?.Invoke(imageData);
            }
            else
            {
                Debug.LogWarning($"Error downloading image from URL: {imageURL} - {webRequest.error}");
                onImageDownloaded?.Invoke(null);
            }
        }
    }
}