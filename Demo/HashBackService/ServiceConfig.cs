using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using billpg.HashBackCore;

namespace billpg.HashBackService
{
    internal static class ServiceConfig
    {
        private const string configFilename = "ServiceConfig.json";

        private static readonly JObject config = 
            (JObject)JToken.Parse(File.ReadAllText(configFilename));

        internal static int? LoadOptionalInt(string key)
            => config[key]?.Value<int>();

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

    }
}
