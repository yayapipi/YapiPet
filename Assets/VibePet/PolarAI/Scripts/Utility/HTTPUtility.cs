using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace Utility.WebRequest
{
    public static class HttpUtility
    {
        
        public static IEnumerator Get<T>(string url, Action<T> callback)
        {
            var request = UnityWebRequest.Get(url);
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Failed: {request.error}");
                yield break;
            }

            var responseJson = request.downloadHandler.text;
            Debug.Log(responseJson);
            var apiResponseModel = JsonConvert.DeserializeObject<T>(responseJson);
            callback?.Invoke(apiResponseModel);
            request.Dispose();
        }
        
        public static IEnumerator Get<T>(string url, Dictionary<string, string> parameters, Action<T> callback)
        {
            /*
            构建 URL 及其参数 Example
            Dictionary<string, string> parameters = new Dictionary<string, string>
            {
                { "param1", "value1" },
                { "param2", "value2" }
            };
            */
            
            if (parameters != null && parameters.Count > 0)
            {
                var queryString = string.Join("&", 
                    parameters.Select(param => 
                        $"{UnityWebRequest.EscapeURL(param.Key)}={UnityWebRequest.EscapeURL(param.Value)}"));
                url = $"{url}?{queryString}";
            }

            var request = UnityWebRequest.Get(url);
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Failed: {request.error}");
                yield break;
            }

            var responseJson = request.downloadHandler.text;
            Debug.Log(responseJson);
            var apiResponseModel = JsonConvert.DeserializeObject<T>(responseJson);
            callback?.Invoke(apiResponseModel);
            request.Dispose();
        }
        
        public static IEnumerator PostByForm<T>(string url, WWWForm form, Action<T> callback)
        {
            var request = UnityWebRequest.Post(url, form);
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Failed: {request.error}");
                yield break;
            }

            var responseJson = request.downloadHandler.text;
            Debug.Log(responseJson);
            var apiResponseModel = JsonConvert.DeserializeObject<T>(responseJson);
            callback?.Invoke(apiResponseModel);
            request.Dispose();
        }

        public static IEnumerator PostByJson<T>(string url, object jsonData, Action<T> callback)
        {
            var json = JsonConvert.SerializeObject(jsonData);
            var request = new UnityWebRequest(url, "POST");
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Failed: {request.error}");
                yield break;
            }

            var responseJson = request.downloadHandler.text;
            Debug.Log(responseJson);
            var apiResponseModel = JsonConvert.DeserializeObject<T>(responseJson);
            callback?.Invoke(apiResponseModel);
            request.Dispose();
        }

    }
}