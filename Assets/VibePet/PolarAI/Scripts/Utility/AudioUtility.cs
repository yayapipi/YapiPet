using System;
using System.Collections;
using System.IO;
using Core;
using UnityEngine;
using UnityEngine.Networking;

namespace YFrame.Runtime.Utility
{
    public static class AudioUtility
    {
        public static void DownloadAudioClip(string url, Action<AudioClip> onDownloaded,
            AudioType audioType = AudioType.WAV)
        {
            CoroutineManager.Instance.StartCoroutine(DownloadAudioClipCoroutine(url, onDownloaded, audioType));
        }

        public static IEnumerator DownloadAudioClipCoroutine(string url, Action<AudioClip> onDownloaded,
            AudioType audioType = AudioType.WAV)
        {
            using UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(url, audioType);
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.ConnectionError ||
                www.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError(www.error);
                onDownloaded?.Invoke(null);
            }
            else
            {
                AudioClip audioClip = DownloadHandlerAudioClip.GetContent(www);
                if (audioClip == null)
                {
                    Debug.LogError($"Failed to load audio clip from URL: {url}");
                }
                else
                {
                    Debug.Log($"Audio clip loaded successfully from URL: {url}");
                }

                onDownloaded?.Invoke(audioClip);
            }
        }
        
        public static IEnumerator LoadLocalAudioClipCoroutine(string filePath, Action<AudioClip> callback)
        {
            using UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip("file://" + filePath, AudioType.WAV);
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError(www.error);
            }
            else
            {
                AudioClip audioClip = DownloadHandlerAudioClip.GetContent(www);
                callback(audioClip);
            }
        }
    }
}