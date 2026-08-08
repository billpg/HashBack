namespace DemoService.Services;

public record HeaderBase(IDictionary<string, string> Headers)
{
    private static readonly IDictionary<string, string> EmptyHeaders
        = new Dictionary<string, string>().AsReadOnly();

    public HeaderBase()
        : this(EmptyHeaders) { }

    public HeaderBase WithHeader(string name, string value)
    {
        /* Clone the old headers into a new collection. */
        var newHeaders = new Dictionary<string, string>(Headers ?? EmptyHeaders);

        /* If the header already exists, add to it with a comma.
         * Otherwise add it as a new header. */
        var oldHeaderValue = newHeaders.GetValueOrDefault(name);
        if (oldHeaderValue == null)
            newHeaders.Add(name, value);
        else
            newHeaders[name] = oldHeaderValue + "," + value;

        /* Return a new object with the new header added. */
        return this with { Headers = newHeaders.AsReadOnly() };
    }
}

public record SimpleHttpResponse(int StatusCode, string Body, string? State) : HeaderBase
{
    public SimpleHttpResponse()
        : this(200, "", null) { }

    public SimpleHttpResponse(int statusCode)
        : this(statusCode, "", null) { }

    public SimpleHttpResponse WithStatusCode(int statusCode)
        => this with { StatusCode = statusCode };

    public new SimpleHttpResponse WithHeader(string name, string value)
        => this with { Headers = base.WithHeader(name, value).Headers };

    public SimpleHttpResponse WithBody(string body)
        => this with { Body = body };
}

public record SimpleHttpRequest(Uri Url) : HeaderBase
{
    public SimpleHttpRequest(string url)
        : this(new Uri(url)) { }

    public new SimpleHttpRequest WithHeader(string name, string value)
        => this with { Headers = base.WithHeader(name, value).Headers };  
}

public static class SpartanExtensions
{
    const string bannerState = "Banner";
    const string headerState = "Header";
    const string bodyState = "Body";

    public static SimpleHttpResponse WithResponseLine(this SimpleHttpResponse resp, string line)
    {
        /* If either params are null, substiute defaults. */
        resp ??= new SimpleHttpResponse();
        line ??= "";

        /* Load the current state from the object's State value. */
        (string parseState, string? lastHeader) 
            = ParseStateString(resp.State);

        /* Handle banner state. "HTTP/1.1 200 OK" */
        if (parseState == bannerState)
        {
            /* Split string into up to 3 parts. Check the HTTP/1.1 but we only
             * care about the status code. If anything is off, assume we've jumped
             * straight to the body. */
            var parts = line.Split(new[] { ' ' }, 3, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2 ||
                !parts[0].StartsWith("HTTP/") ||
                !int.TryParse(parts[1], out int statusCode))
                return resp with { Body = line, State = bodyState };

            /* Otherwise, return a new object with this status code
             * and expecting a header next time. */
            return resp with { StatusCode = statusCode, State = headerState };
        }

        /* Handle headers state. "Xyz: Abc" */
        if (parseState == headerState)
        {
            /* Blank line signals end of headers and start of body. */
            if (string.IsNullOrWhiteSpace(line))
                return resp with { State = bodyState };

            /* Folded header (continuation) starts with whitespace. */
            if (char.IsWhiteSpace(line[0]))
            {
                /* If we don't have a last-header recored, or we do
                 * and that header is missing, assume this is the first line of body text. */
                if (string.IsNullOrEmpty(lastHeader) ||
                    resp.Headers == null ||
                    !resp.Headers.ContainsKey(lastHeader))
                    return resp with { Body = line, State = bodyState };

                /* Add this continuation header onto the previous header. Return the state as
                 * the same last-header in case the next line is another continuation. */
                var newHeaders = new Dictionary<string, string>(resp.Headers);
                newHeaders[lastHeader] = newHeaders[lastHeader] + " " + line.Trim();
                return resp with { Headers = newHeaders.AsReadOnly(), State = $"{headerState}/{lastHeader}" };
            }

            /* Normal header: "Name: value". If no colon, treat as body text. */
            int colonIndex = line.IndexOf(':');
            if (colonIndex <= 0)
                return resp with { Body = line, State = bodyState };
            var name = line.Substring(0, colonIndex).Trim();
            var value = line.Substring(colonIndex + 1).Trim();

            /* Record the newly-parsed header as the lastHeader so continuations can append to it. */
            return resp.WithHeader(name, value) with { State = $"{headerState}/{name}" };
        }

        /* Handle the body state by saving lines into the string with CRLFs. */
        if (parseState == bodyState)
        {
            /* Append lines to body preserving line breaks. */
            return resp.WithBody(
                (resp.Body ?? "") 
                + (string.IsNullOrEmpty(resp.Body) ? "" : "\r\n")
                + line);
        }

        /* If we get here, the parse stat wasn;t known. */
        throw new ApplicationException("Unknown response state.");
    }

    private static (string parseState, string? lastHeader) ParseStateString(string? state)
    {
        /* If null, start at the banner. */
        if (state == null)
            return new(bannerState, null);

        /* Handle the cases without extra parameters. */
        if (state == bannerState || state == bodyState || state == headerState)
            return (state, null);

        /* Parse the string into state and last-headr parts. */
        var stateItems = state.Split(new[] { '/' }, 2);
        if (stateItems[0] == headerState && stateItems[1].Length > 0)
            return (headerState, stateItems[1]);
        
        /* Any other combination is an error. */
        throw new ApplicationException("Bad State.");
    }
}