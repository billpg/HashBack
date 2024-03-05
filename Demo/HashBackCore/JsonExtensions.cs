using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace billpg.HashBackCore
{
    public static class JsonExtensions
    {
        public static string ToStringIndented(this JObject obj)
            => obj.ToString(Newtonsoft.Json.Formatting.Indented);

        public static string ToStringOneLine(this JObject obj)
            => obj.ToString(Newtonsoft.Json.Formatting.None);
    }
}
