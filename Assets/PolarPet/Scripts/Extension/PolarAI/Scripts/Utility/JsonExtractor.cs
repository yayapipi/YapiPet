using System.Text.RegularExpressions;
using Newtonsoft.Json;
using UnityEngine;

namespace YFrame.Runtime.Utility
{
    public static class JsonExtractor
    {
        public static object Extract<T>(string text)
        {
            Debug.Log(text);
            text = CleanJsonString(text);
            return JsonConvert.DeserializeObject<T>(text);
        }
        
        public static string CleanJsonString(string input)
        {
            string pattern = @"\{(?:[^{}]|(?<open>\{)|(?<-open>\}))+(?(open)(?!))\}";
            Match match = Regex.Match(input, pattern);
            Debug.Log("---Clean Input---");
            Debug.Log(match.Value);
            if (match.Success)
            {
                var output = match.Value.Replace("，", ",");
                return output;
            }
            return input;
            
        }
    }
}