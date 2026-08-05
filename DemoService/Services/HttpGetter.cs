using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace DemoService.Services;

public interface IHttpGetter
{
    Task<HttpResponseMessage> GetAsync(Uri uri, IDictionary<string, string>? headers = null);
}

public class HttpGetter : IHttpGetter
{
    private readonly HttpClient _client;

    public HttpGetter(HttpClient client)
    {
        _client = client;
    }

    public async Task<HttpResponseMessage> GetAsync(Uri uri, IDictionary<string, string>? headers = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);

        if (headers != null)
        {
            foreach (var kv in headers)
            {
                // Use TryAddWithoutValidation to allow arbitrary header values like "HashBack ..."
                request.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
            }
        }

        return await _client.SendAsync(request);
    }
}