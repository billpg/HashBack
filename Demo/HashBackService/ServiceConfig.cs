using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using billpg.HashBackCore;
using Microsoft.AspNetCore.DataProtection.KeyManagement;

namespace billpg.HashBackService
{
    internal static class ServiceConfig
    {
        private const string configFilename = "ServiceConfig.json";

        private static readonly JObject config = 
            (JObject)JToken.Parse(File.ReadAllText(configFilename));

        internal static int? LoadOptionalInt(string key)
            => config[key]?.Value<int>();

        internal static string? LoadOptionalString(string key)
            => config[key]?.Value<string>();

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

        internal static string LoadStringOrSet(string key, Func<string> initialValue)
        {
            /* Query JSON for key. */
            string? value = config[key]?.Value<string>();
            if (value != null)
                return value;

            /* Key is missing. Load the value from the supplied function. */
            value = initialValue();

            /* Write back into JSON. */
            config[key] = value;
            File.WriteAllText(configFilename, config.ToStringIndented());

            /* Return new value. */
            return value;
        }

        internal static IList<string> LoadStrings(string key)
        {
            JArray? alwaysAllowAsJArray = config[key]?.Value<JArray>();
            if (alwaysAllowAsJArray == null)
                return new List<string>().AsReadOnly();

            var result = new List<string>();
            foreach (var jItem in alwaysAllowAsJArray)
            {
                string? value = jItem?.Value<string>();
                if (value != null)
                    result.Add(value);
            }

            return result;
        }
    }
}
