using Microsoft.Extensions.Hosting;
using System;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace billpg.HashBackCore
{
    internal class SpartanHttp
    {
        internal static byte[] Run(IPAddress ip, bool useTls, int port, string host, Action<X509Certificate2> onTlsHandshake, byte[] request)
        {
            /* Connect to TCP. */
            using var tcp = new TcpClient();
            tcp.Connect(ip, port);

            /* Open the TCP stream under a "using" so it'll be closed. Also set the read
             * timeout to 5s so a hostile caller can't hold things up. Then save a copy 
             * as a base-class which may get swapped out for the TLS tsream. */
            using var tcpStream = tcp.GetStream();
            tcpStream.ReadTimeout = 5000;

            using var str = OpenTls(useTls, tcpStream, host, onTlsHandshake);

            /* Send HTTP request, either through TLS or TCP as applicable. */
            str.Write(request);

            /* Wait for response, keeping going until the strem is closed or the
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

            /* Copy the completed buffer into a new array. */
            byte[] response = new byte[bufferIndex];
            Buffer.BlockCopy(buffer, 0, response, 0, bufferIndex);
            return response;
        }

        private static Stream OpenTls(bool useTls, NetworkStream tcpStr, string host, Action<X509Certificate2> onTlsHandshake)
        {
            /* If not using TLS, return the TCP stream back. */
            if (useTls == false)
                return tcpStr;

            /* Set up TLS, wrapping the TCP stream. */
            var tls = new SslStream(tcpStr, false, InternalTlsHandshake);
            bool InternalTlsHandshake(
                object sender, X509Certificate? cert, X509Chain? _, SslPolicyErrors errors)
            {
                /* Pass the captured cert to the caller, who may throw an exception here. */
                if (cert is X509Certificate2 cert2)
                    onTlsHandshake(cert2);

                /* Didn't throw, so defer to the error for if to continue or not. */
                return errors == SslPolicyErrors.None;
            }

            /* Actually handhsake TLS. This will call the aove validator and in turn
                * call the caller's function to receive the TLS cert. */
            tls.AuthenticateAsClient(host);

            return tls;
        }
    }
}