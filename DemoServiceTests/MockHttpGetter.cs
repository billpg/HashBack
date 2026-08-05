using DemoService.Services;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace DemoServiceTests
{
    public sealed class MockHttpGetter : IHttpGetter
    {
        private readonly ConcurrentDictionary<string, string> _registered;
        public MockHttpGetter(ConcurrentDictionary<string, string>? registered = null)
        {
            _registered = registered ?? new ConcurrentDictionary<string, string>();
        }
        
        public HttpResponseMessage? Response { get; set; }
        public IDictionary<string, string>? LastHeaders { get; private set; }
        public Uri? LastUri { get; private set; }

        public Task<HttpResponseMessage> GetAsync(Uri uri, IDictionary<string, string>? headers = null)
        {
            if (Response == null && _registered.TryGetValue(uri.ToString(), out string? registeredHash))
            {
                Response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(registeredHash) };
            }
                   
            LastUri = uri;
            LastHeaders = headers;
            return Task.FromResult(Response ?? new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
