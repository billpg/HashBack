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
        
        public SimpleHttpResponse? Response { get; set; }
        public IDictionary<string, string>? LastHeaders { get; private set; }
        public Uri? LastUri { get; private set; }

        public Task<SimpleHttpResponse> GetAsync(SimpleHttpRequest req)
        {
            if (Response == null && _registered.TryGetValue(req.Url.ToString(), out string? registeredHash))
                Response = new SimpleHttpResponse(200)
                    .WithHeader("Content-Type", "text/plain")
                    .WithHeader("Server", "unit test")
                    .WithBody(registeredHash);
                   
            LastUri = req.Url;
            LastHeaders = req.Headers;
            return Task.FromResult(Response ?? new SimpleHttpResponse(404));
        }
    }
}
