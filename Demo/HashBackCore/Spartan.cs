using billpg.HashBackCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace billpg.HashBackCore
{
    public static class Spartan
    {
        private const byte CR = (byte)'\r';
        private const byte LF = (byte)'\n';


        /// <summary>
        /// A single collection of headers, used in both a Request and Response.
        /// </summary>
        public record HeaderCollection
        {
            /// <summary>
            /// The wrapped header collection. a Dictionary inside a
            /// .AsReadOnly() wrapper. When called as a public constructor
            /// the initial value will be an empty dictionary.
            /// </summary>
            private IDictionary<string, string> headers { get; init; }
                = new Dictionary<string, string>();

            /// <summary>
            /// Return a new object with the existing headers and a new
            /// header. The current object remains unmodified.
            /// </summary>
            /// <param name="name">Name of new header.</param>
            /// <param name="value">Value of the new header.</param>
            /// <returns>Updated HeaderCollection.</returns>
            public HeaderCollection With(string name, string value)
            {
                /* Copy the current headers into a new collection. */
                var newHeaders = new Dictionary<string, string>(this.headers);

                /* Set the newly added value into the copy. */
                newHeaders[name] = value;

                /* Construct a new HeaderCollection object using the
                 * updated headers. */
                return this with { headers = newHeaders.AsReadOnly() };
            }

            /// <summary>
            /// Return the header collection as an HTTP block.
            /// </summary>
            /// <returns>String version of header collection.</returns>
            public IList<byte> AsHttpBytes()
            {
                /* Start the byte collection that will be returned at the end. */
                var result = new List<byte>();

                /* Loop through headers, adding bytes as we go,
                 * including CRLF for all including at the end. */
                foreach (var header in this.headers)
                {
                    byte[] headerAsBytes 
                        = Encoding.UTF8.GetBytes($"{header.Key}: {header.Value}\r\n");
                    result.AddRange(headerAsBytes);
                }

                /* Completed hyte array. */
                return result.AsReadOnly();
            }
        }

        public record Request
        {
            public string Method { get; init; } = null!;
            public Uri Url { get; init; } = null!;
            public HeaderCollection Headers { get; init; } 
                = new HeaderCollection();
            public string Body { get; init; } = "";

            public static Request GET(Uri url)
                => new Request() with { Method = "GET", Url = url };

            public static Request POST(Uri url)
                => new Request() with { Method = "POST", Url = url };

            public Request WithHeader(string name, string value)
                => this with { Headers = this.Headers.With(name, value) };

            public Request WithBody(string body)
                => this with { Body = body };

            public IList<byte> AsHttpBytes()
            {
                /* Start a byte collection for the HTTP request. Wrap in a
                 * function for appening strings to this collection. */
                var requestAsBytes = new List<byte>();
                void AppendAscii(string line)
                    => requestAsBytes.AddRange(Encoding.ASCII.GetBytes(line + "\r\n"));
                
                /* HTTP connection banner. */
                AppendAscii($"{this.Method} {this.Url.PathAndQuery} HTTP/1.1");

                /* Our headers. */
                AppendAscii($"Host: {this.Url.Host}");
                AppendAscii("Connection: close" );
                AppendAscii("Accept-Encoding: identity");
                AppendAscii(
                    "User-Agent: HashBack Demo." +
                    " Please report issues to https://github.com/billpg/HashBack/issues");

                /* Caller's headers, each of which will include CRLF. */
                requestAsBytes.AddRange(this.Headers.AsHttpBytes());

                /* Blank line separator between header and body. */
                requestAsBytes.Add(CR);
                requestAsBytes.Add(LF);

                /* Request body bytes. */
                requestAsBytes.AddRange(Encoding.UTF8.GetBytes(this.Body));

                /* Completed. */
                return requestAsBytes.AsReadOnly();
            }
        }

        public record Response 
        {
            public IPAddress IP { get; init; } = IPAddress.None;
            public X509Certificate2? TlsCertificate { get; init; } = null;
            public int StatusCode { get; init; } = 0;
            public HeaderCollection Headers { get; init; } 
                = new HeaderCollection();
            public string Body { get; init; } = "";

            public Response WithIP(IPAddress newIP)
                => this with { IP = newIP };
            public Response WithTlsCertificate(X509Certificate2 newTlsCertificate)
                => this with { TlsCertificate = newTlsCertificate };            
            public Response WithStatusCode(int newStatusCode)
                => this with { StatusCode = newStatusCode };
            public Response WithHeader(string name, string value)
                => this with { Headers = this.Headers.With(name, value) };
            public Response WithBody(string newBody)
                => this with { Body = newBody };
        }

        public static Response Run(
            Request req,
            OnRetrieveErrorFn onRetrieveError,
            OnHostLookupFn onHostLookup,
            OnHostLookupCompletedFn? onHostLookupCompleted = null,
            OnTlsHandshakeCompletedFn? onTlsHandshakeCompleted = null)
        {
            /* Call the host lookup service for the IP. */
            IPAddress ip = onHostLookup(req.Url.Host);
            if (ip == IPAddress.None)
                throw onRetrieveError("No such host at " + req.Url.Host);

            /* Coll the host-lookup-complete and allow it throw an exception. */
            onHostLookupCompleted?.Invoke(req.Url.Host, ip);

            /* Connect to TCP. */
            using var tcp = new TcpClient();
            tcp.Connect(ip, req.Url.Port);

            /* Open the TCP stream under a "using" so it'll be closed. Also set the read
             * timeout to 5s so a hostile caller can't hold things up. Then save a copy 
             * as a base-class which may get swapped out for the TLS tsream. */
            using var tcpStream = tcp.GetStream();
            tcpStream.ReadTimeout = 5000;

            /* Hand-shake TLS, collecting the certificate along the way. */
            X509Certificate2? tlsCert = null;
            using var str = OpenTls(req.Url.UseTls(), tcpStream, req.Url.Host, TlsSave);
            void TlsSave(X509Certificate2 cert)
            {
                tlsCert = cert;
                onTlsHandshakeCompleted?.Invoke(req.Url.Host, cert);
            }

            /* Send HTTP request, either through TLS or TCP as applicable. */
            var requestAsBytes = req.AsHttpBytes();
            str.Write(requestAsBytes.ToArray());

            /* Wait for response, keeping going until the stream is closed or the
             * buffer is full. If a read takes more than 5s, the timeout will cause
             * an error. */
            byte[] buffer = new byte[64 * 1024];
            int bufferIndex = 0;
            while (true)
            {
                /* Attempt to populate response buffer. */
                int bytesIn = str.Read(buffer, bufferIndex, buffer.Length - bufferIndex);
                bufferIndex += bytesIn;
                if (bytesIn == 0)
                    break;
            }

            /* Set up for parsing the response buffer. The index from
             * the above is now the size of the buffer, so store that
             * and reset the index to zero.*/
            int bufferSize = bufferIndex;
            bufferIndex = 0;

            /* A function that reads the next line from the response buffer. */
            string? ResponseNextLine()
            {
                /* Null if we've reached the end. */
                if (bufferIndex >= bufferSize)
                    return null;

                /* Find the index of the next CR/LF/End. */
                int lineTermIndex = bufferIndex;
                while (lineTermIndex < bufferSize && IsCrOrLf(buffer[lineTermIndex]) == false)
                    lineTermIndex++;

                /* Pull out the line and move the index along. */
                string responseLine = Encoding.UTF8.GetString(buffer, bufferIndex, lineTermIndex - bufferIndex);
                bufferIndex = lineTermIndex;

                /* If we're not yet at the end of the buffer... */
                if (bufferIndex < bufferSize)
                {
                    /* Are we stepping over a CR? */
                    bool isCR = buffer[bufferIndex] == CR;

                    /* CR or LF, step over it. */
                    bufferIndex += 1;

                    /* If that was a CR and we're still not at the end,
                     * check for an LF too. */
                    if (isCR && bufferIndex < bufferSize && buffer[bufferIndex] == LF)
                        bufferIndex += 1;
                }

                /* Return the line string captured earlier. */
                return responseLine;
            }

            /* Open an empty response object and save the items we've collected so far. */
            var response = new Response();
            if (ip != IPAddress.None)
                response = response.WithIP(ip);
            if (tlsCert != null)
                response = response.WithTlsCertificate(tlsCert);

            /* Read the status code line. */
            string? statusCodeLine = ResponseNextLine();
            if (statusCodeLine == null)
                throw onRetrieveError("HTTP response did not include the status line.");
            string[] statusLineBySpace = statusCodeLine.Split(' ');
            if (statusLineBySpace[0] != "HTTP/1.1")
                throw onRetrieveError("Status line did not begin \"HTTP/1.1\".");
            if (statusLineBySpace.Length < 2)
                throw onRetrieveError("Status line did not include a status code.");
            if (int.TryParse(statusLineBySpace[1], out int statusCode) == false || statusCode < 100 || statusCode > 599)
                throw onRetrieveError($"Status code must be three dgitis 100-599.");
            response = response.WithStatusCode(statusCode);

            /* Churn through the response headers. */
            while (true)
            {
                /* Read the header, stop if either blank line 
                 * separating header from body or end of buffer. */
                string? headerLine = ResponseNextLine();
                if (string.IsNullOrEmpty(headerLine))
                    break;

                /* Split into name and value. */
                int indexOfColon = headerLine.IndexOf(':');
                if (indexOfColon < 0)
                    throw onRetrieveError("Response header without colon.");
                response = response.WithHeader(
                    headerLine.Substring(0, indexOfColon).Trim(),
                    headerLine.Substring(indexOfColon+1).Trim());
            }

            /* Rest of buffer is response. Read without using line reader. */
            string responseBody = Encoding.UTF8.GetString(buffer, bufferIndex, bufferSize - bufferIndex);
            response = response.WithBody(responseBody);

            /* Completed parse. */
            return response;
        }

        private static bool IsCrOrLf(byte by)
            => (by == CR) || (by == LF);

        private static Stream OpenTls(bool useTls, NetworkStream tcpStr, string host, Action<X509Certificate2> onTlsSave)
        {
            /* If not using TLS, return the TCP stream back. */
            if (useTls == false)
                return tcpStr;

            /* Set up TLS, wrapping the TCP stream. This will be the stream returned at the end. */
            var tls = new SslStream(tcpStr, false, InternalTlsHandshake);
            bool InternalTlsHandshake(
                object _, X509Certificate? cert, X509Chain? __, SslPolicyErrors errors)
            {
                /* Pass the captured cert to the caller, who may throw an exception here. */
                if (cert is X509Certificate2 cert2)
                    onTlsSave(cert2);

                /* Didn't throw, so defer to the error for if to continue or not. */
                return errors == SslPolicyErrors.None;
            }

            /* Actually handhsake TLS. This will call the aove validator and in turn
             * call the caller's function to receive the TLS cert. */
            tls.AuthenticateAsClient(host);

            /* Completed without exceptions thorwn, return the TLS stream to the caller. */
            return tls;
        }
    }
}

