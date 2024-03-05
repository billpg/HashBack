using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace billpg.HashBackService
{
    internal static class ServiceConfig
    {
        private static readonly JObject config = 
            (JObject)JToken.Parse(File.ReadAllText("ServiceConfig.json"));

        internal static int LoadRequiredInt(string key)
        {
            int? value = config[key]?.Value<int>();
            if (value.HasValue)
                return value.Value;
            throw new ApplicationException("Missing Config: " + key);
        }

        internal static string LoadRequiredString(string key)
        {
            string? value = config[key]?.Value<string>();
            if (value != null)
                return value;
            throw new ApplicationException("Missing Config: " + key);
        }

    }
}
