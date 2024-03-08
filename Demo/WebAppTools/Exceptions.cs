using Newtonsoft.Json.Linq;

namespace billpg.WebAppTools
{
    public class HttpResponseException : Exception
    {
        public int StatusCode { get; } = 200;
        public JObject ResponseBody { get; } = new JObject();
        public IDictionary<string,string> Headers { get; } = new Dictionary<string,string>();

        public HttpResponseException(int statusCode, string message, JObject responseBody)
            : base(message)
        {
            this.StatusCode = statusCode;
            var body = (JObject)(responseBody.DeepClone());
            body["Message"] = message;
            body["StatusCode"] = statusCode;
            this.ResponseBody = body;
        }

        public JObject BodyWithIncidentId(Guid incidentId)
        {
            /* Copy the respnse body and update it. */
            JObject newBody = (JObject)this.ResponseBody.DeepClone();
            newBody["IncidentID"] = incidentId.ToString().ToUpperInvariant();
            return newBody;
        }
    }

    public class NotFoundException: HttpResponseException
    {
        public NotFoundException()
            : base(404, "Not Found", new JObject())
        { }
    }

    public class BadRequestException: HttpResponseException
    {
        public BadRequestException(string message)
            : this(message, new JObject())
        {}

        private BadRequestException(string message, JObject responseBody)
            : base(400, message, responseBody)
        {}

        public BadRequestException WithResponseProperty(string propName, JToken node)
        {
            /* Open a new response body object. */
            JObject newResponseBody = (JObject)this.ResponseBody.DeepClone();
            newResponseBody[propName] = node;

            /* Return in a new exception object. */
            return new BadRequestException(this.Message, newResponseBody);
        }
    }
}
