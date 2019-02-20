using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MessagePack;
using Newtonsoft.Json;

namespace RedisMessagePackFormatter
{
    class Program
    {
        private static Dictionary<string, string> _propertyMappings;
        private static string[] _guidFields;

        static void Main(string[] args)
        {
            GetConfig();
            
            if (args[0] == "info")
            {
                OutputFormatterInfo();
                return;
            }

            if (args[0] == "decode")
            {
                DecodeMessagePack();
            }
        }

        private static void DecodeMessagePack()
        {
            string input;
            while ((input = Console.ReadLine()) != null)
            {
                var bytes = Convert.FromBase64CharArray(input.ToCharArray(), 0, input.Length);
                dynamic messagePackObject = MessagePackSerializer.Typeless.Deserialize(bytes);
                
                SetFullGuids(messagePackObject);
                
                string prettyJson = JsonConvert.SerializeObject(messagePackObject, Formatting.Indented);

                foreach (var prop in _propertyMappings)
                {
                    prettyJson = prettyJson?.Replace($"\"{prop.Key}\"", $"\"{prop.Value}\"");
                }
                
                // Convert to ASCII because that's all RDM supports
                var asciiBytes = Encoding.ASCII.GetBytes(prettyJson);
                var asciiPrettyJson = Encoding.ASCII.GetString(asciiBytes);
                Console.Write(JsonConvert.SerializeObject(new DecodeResponse(asciiPrettyJson)));
            }
        }

        private static void SetFullGuids(dynamic messagePackObject)
        {
            if (messagePackObject is string || !messagePackObject.ContainsKey("i"))
            {
                return;
            }

            foreach (var guidField in _guidFields)
            {
                SetFullGuid(messagePackObject, guidField);
            }
          
            if (messagePackObject.ContainsKey("c"))
            {
                // Set child IDs
                foreach (var child in messagePackObject["c"])
                {
                    SetFullGuids(child);
                }
            }
        }

        private static void SetFullGuid(dynamic value, string key)
        {
            if (value.ContainsKey(key))
            {
                value[key] = $"{value[key]} ({ToFullGuid(value[key])})";
            }
        }

        private static Guid ToFullGuid(string value)
        {
            return new Guid(Convert.FromBase64String($"{value}=="));
        }

        private static void OutputFormatterInfo()
        {
            var info = new {version = "1.1.0", description = "MessagePack Formatter"};
            Console.Write(JsonConvert.SerializeObject(info));
        }
        
        static void GetConfig()
        {
            var json = File.ReadAllText("property-mappings.json");
            var config = JsonConvert.DeserializeObject<Config>(json);
            _propertyMappings = config.PropertyMappings;
            _guidFields = config.GuidFields;
        }
    }

    class DecodeResponse
    {
        public DecodeResponse(string output)
        {
            Output = output;
        }

        [JsonProperty("output")]
        public string Output { get; private set; }
        [JsonProperty("read-only")] 
        public string ReadOnly { get; private set; } = "true";
        [JsonProperty("format")]
        public string Format { get; private set; } = "plain_text";
    }

    class Config
    {
        public Dictionary<string, string> PropertyMappings { get; set; }
        public string[] GuidFields { get; set; }
    }
}
