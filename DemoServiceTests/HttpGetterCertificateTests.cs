using System;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DemoService;
using DemoService.Services;

namespace DemoServiceTests;

[TestClass]
public class HttpGetterCertificateTests
{
    private static X509Certificate2 CreateSelfSignedCertificate(string subjectName)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            $"CN={subjectName}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        var sanBuilder = new SubjectAlternativeNameBuilder();
        sanBuilder.AddDnsName(subjectName);
        request.CertificateExtensions.Add(sanBuilder.Build());

        var cert = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddMinutes(5));

        /* Re-import from a PFX so the result doesn't depend on the "rsa" object's lifetime,
         * matching what a server loading a certificate from disk would end up with. */
        return X509CertificateLoader.LoadPkcs12(cert.Export(X509ContentType.Pfx), password: null);
    }

    /// <summary>Starts a bare TLS server on loopback that accepts one connection, handshakes with the given certificate, and stops.</summary>
    private static (TcpListener listener, int port, Task serverTask) StartTlsServer(X509Certificate2 serverCert)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var serverTask = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync();
            using var sslStream = new SslStream(client.GetStream(), leaveInnerStreamOpen: false);
            try
            {
                await sslStream.AuthenticateAsServerAsync(
                    new SslServerAuthenticationOptions { ServerCertificate = serverCert });

                /* Only reached when the client accepts the handshake - write a minimal
                 * response so the full GET/response exchange can complete. */
                var response = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n");
                await sslStream.WriteAsync(response);
            }
            catch
            {
                /* Expected when the test client deliberately rejects this handshake. */
            }
        });

        return (listener, port, serverTask);
    }

    [TestMethod]
    public async Task FetchAsync_DefaultPolicy_RejectsSelfSignedCertificate()
    {
        using var cert = CreateSelfSignedCertificate("localhost");
        string expectedHash = Convert.ToBase64String(cert.GetCertHash(HashAlgorithmName.SHA256));
        var (listener, port, serverTask) = StartTlsServer(cert);
        ServiceData.AllowGetLocalhost = true;
        try
        {
            var getter = new HttpGetter(
                new ServiceData(), new IpFilter(), new AlwaysAllowCallPermissionChecker(),
                dnsLookup: (host, ct) => Task.FromResult(new[] { IPAddress.Loopback }));

            var ex = await Assert.ThrowsExceptionAsync<BadRequestException>(async () =>
                await getter.FetchAsync(new Uri($"https://localhost:{port}/")));

            Assert.AreEqual("External URL not available.", ex.Title);
            StringAssert.Contains(ex.Message, expectedHash,
                "Rejection detail should still report the certificate that was actually presented.");
        }
        finally
        {
            ServiceData.AllowGetLocalhost = false;
            listener.Stop();
            await Task.WhenAny(serverTask, Task.Delay(TimeSpan.FromSeconds(2)));
        }
    }

    [TestMethod]
    public async Task FetchAsync_CustomPolicyAcceptsIt_SucceedsAndReportsMatchingHash()
    {
        using var cert = CreateSelfSignedCertificate("localhost");
        string expectedHash = Convert.ToBase64String(cert.GetCertHash(HashAlgorithmName.SHA256));
        var (listener, port, serverTask) = StartTlsServer(cert);
        ServiceData.AllowGetLocalhost = true;
        try
        {
            var getter = new HttpGetter(
                new ServiceData(), new IpFilter(), new AlwaysAllowCallPermissionChecker(),
                dnsLookup: (host, ct) => Task.FromResult(new[] { IPAddress.Loopback }),
                isCertificateAcceptable: (url, presentedCert, chain, sslPolicyErrors) => true);

            var resp = await getter.FetchAsync(new Uri($"https://localhost:{port}/"));

            Assert.AreEqual(expectedHash, resp.RemoteCertificateHash);
        }
        finally
        {
            ServiceData.AllowGetLocalhost = false;
            listener.Stop();
            await Task.WhenAny(serverTask, Task.Delay(TimeSpan.FromSeconds(2)));
        }
    }
}
