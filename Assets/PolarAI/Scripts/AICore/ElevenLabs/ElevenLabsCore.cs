using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Core;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace PolarAI.Scripts.AICore.ElevenLabs
{
    public class ElevenLabsCore 
    {
        public string ApiKey = "ELEVENLABS_API_KEY";
        public string VoiceId = "YOUR_VOICE_ID";
        public string ModelId = "eleven_multilingual_v2";
        private string OutputFormat = "mp3_44100_128";

        private AudioClip _recording;

        public void Initialize(string apiKey, string voiceId)
        {
            ApiKey = apiKey;
            VoiceId = voiceId;
        }
        
        
        public void SetDefaultVoiceId(string voiceId)
        {
            VoiceId = voiceId;
        }


        public void TextToSound(string text,  Action<AudioClip, bool> onComplete, string voiceId=null)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                Debug.Log("TTS Text is empty.");
                return;
            }

            CoroutineManager.Instance.StartCoroutine(DoTTS(text,onComplete, voiceId));
        }

        private IEnumerator DoTTS(string text,Action<AudioClip, bool> onComplete,  string voiceId =null)
        {
            Debug.Log("Requesting ElevenLabs TTS...");
            voiceId ??= VoiceId;
            var url = $"https://api.elevenlabs.io/v1/text-to-speech/{voiceId}?output_format={OutputFormat}";
            var payload = new ElevenTTS { text = text, model_id = ModelId };
            var json = JsonConvert.SerializeObject(payload);
            
            using var req = new UnityWebRequest(url, "POST");
            byte[] body = Encoding.UTF8.GetBytes(json);
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("xi-api-key", ApiKey);
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                string err = req.error + " | " + req.downloadHandler.text;
                onComplete?.Invoke(null, false);
                Debug.LogError(err);
                yield break;
            }

            byte[] audioData = req.downloadHandler.data;

            string ext = OutputFormat.StartsWith("mp3") ? "mp3" : "wav";
            string tempPath = Path.Combine(Application.persistentDataPath, "tts." + ext);
            File.WriteAllBytes(tempPath, audioData);

            yield return CoroutineManager.Instance.StartCoroutine(
                LoadCLip(tempPath, ext == "mp3" ? AudioType.MPEG : AudioType.WAV, onComplete));
        }

        public IEnumerator LoadCLip(string path, AudioType type, Action<AudioClip,bool> onLoad)
        {
            using var www = UnityWebRequestMultimedia.GetAudioClip("file://" + path, type);
            yield return www.SendWebRequest();
            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Audio load failed: " + www.error);
                yield break;
            }

            AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
            onLoad?.Invoke(clip, true);
        }

        [Serializable]
        private class ElevenTTS
        {
            public string text;
            public string model_id;
        }
    }

}