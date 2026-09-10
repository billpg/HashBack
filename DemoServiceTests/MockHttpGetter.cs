using DemoService.Services;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Threading.Tasks;
using billpg.SpartanHttpClient;

namespace DemoServiceTests
{
    public sealed class MockHttpGetter : IHttpGetter
    {
        private readonly ConcurrentDictionary<string, string> _registered;
        public MockHttpGetter(ConcurrentDictionary<string, string>? registered = null)
        {
            _registered = registered ?? new ConcurrentDictionary<string, string>();
        }

        public SpartanResponse? Response { get; set; }
        public string? LastAuthorizationHeader { get; private set; }
        public Uri? LastUri { get; private set; }

        public Task<SpartanResponse> GetAsync(Uri url, string? authorizationHeader)
        {
            if (Response == null && _registered.TryGetValue(url.ToString(), out string? registeredHash))
                Response = new SpartanResponse()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "text/plain")
                    .WithHeader("Server", "unit test")
                    .WithBody(registeredHash);

            LastUri = url;
            LastAuthorizationHeader = authorizationHeader;
            var resp = Response ?? new SpartanResponse().WithStatusCode(404);
            return Task.FromResult(resp.WithRemoteAddress(IPAddress.Loopback));
        }
    }
}
