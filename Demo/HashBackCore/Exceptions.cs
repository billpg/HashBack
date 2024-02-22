/* Copyright William Godfrey, 2024. All rights reserved.
 * billpg.com
 */
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace billpg.HashBackCore
{
    public class BadRequestException : Exception
    {
        private IList<string>? AcceptVersions { get; }
        private int? AcceptRounds { get; }

        internal static BadRequestException General(string message)
            => new BadRequestException(message: message);

        internal static BadRequestException BadVersion()
            => new BadRequestException(
                message: 
                    $"Unknown HashBack version. " +
                    $"We only know \"{CallerRequest.VERSION_3_0}\" " +
                    $"and \"{CallerRequest.VERSION_3_1}\".",
                acceptVersions: new List<string> 
                {
                    CallerRequest.VERSION_3_0,
                    CallerRequest.VERSION_3_1 
                }.AsReadOnly());


        internal static BadRequestException BadRounds(int acceptRounds)
            => new BadRequestException(
                message: $"Rounds must be between {IssuerSession.minRounds} and {IssuerSession.maxRounds}.",
                acceptRounds: acceptRounds);

        internal static BadRequestException Required(string missingPropertyName)
            => new BadRequestException(message: $"Request is missing required {missingPropertyName} property.");

        private BadRequestException(string message, IList<string>? acceptVersions = null, int? acceptRounds = null)
            :base(message)
        {
            this.AcceptVersions = acceptVersions;
            this.AcceptRounds = acceptRounds;
        }


        public JObject AsJson(Guid incidentID)
        {
            /* Build 400 response body object. */
            var j = new JObject
            {
                [nameof(this.Message)] = this.Message,
                ["IncidentID"] = incidentID.ToHexWithHyphens()
            };
            if (this.AcceptVersions != null)
                j[nameof(this.AcceptVersions)] = new JArray(this.AcceptVersions);
            if (this.AcceptRounds.HasValue)
                j[nameof(this.AcceptRounds)] = this.AcceptRounds.Value;

            /* Return completed object. */
            return j;
        }
    }
}
