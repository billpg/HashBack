using billpg.WebAppTools;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static billpg.HashBackCore.InternalTools;
using static billpg.HashBackCore.ThreadSafeHashStore;

namespace billpg.HashBackCore
{
    /// <summary>
    /// Supplies the hash service end-points.
    /// </summary>
    public class HashService
    {
        /// <summary>
        /// The deserialized request body for the AddHash end-point.
        /// </summary>
        public class AddHashRequestBody
        {
            public string ID { get; set; } = "";
            public string Hash { get; set; } = "";
        }

        /// <summary>
        /// Function to call when the current time in seconds-since-1970 is needed.
        /// Defaults to wrapped DateTime.UtcNow, but may be replaced by unit tests.
        /// </summary>
        public OnNowFn NowService { get; set; }
            = () => InternalTools.NowUnixTime;

        /// <summary>
        /// Delegate for the BadRequestException handler. Converts a string response body into
        /// an exception to throw that the shared error handler will know to convert into
        /// a 400-Bad-Request response.
        /// </summary>
        /// <param name="responseText">Test to include as message body.</param>
        /// <returns>Exception to throw.</returns>
        public delegate Exception OnBadRequestFn(string responseText);

        /// <summary>
        /// Function to call when an exception is needed that will be caught by the shared
        /// error handler that will become a 400 response with the supplied text as the 
        /// response body.
        /// </summary>
        public OnBadRequestFn OnBadRequestException { get; set; }
            = msg => throw new NotImplementedException();

        /// <summary>
        /// URL to redirect users to who "GET /hashes".
        /// </summary>
        public string DocumentationUrl { get; set; } = "https://invalid/";

        /// <summary>
        /// The hash store for this instance.
        /// </summary>
        private readonly ThreadSafeHashStore hashStorage 
            = new ThreadSafeHashStore();

        /// <summary>
        /// Configure the web service with the hash service end-points.
        /// </summary>
        /// <param name="app">Web Service object.</param>
        /// <param name="path">URL path to configure these end-points.</param>
        public void ConfigureHttpService(WebApplication app, string path)
        {
            /* Map these two private member functions as handlers. */
            app.MapGet(path, this.GetHash);
            app.MapPost(path, this.AddHash);
        }

        private IResult GetHash(
            [FromQuery(Name = "id")] string? idAsString, HttpContext context)
        {
            /* Load the ID query string parameter. If not used, redirect to the documentation. */
            if (idAsString == null)
                return Results.Redirect(DocumentationUrl);

            /* Convert ID to UUID, reporting bad-request if not valid. */
            if (Guid.TryParse(idAsString, out Guid id) == false)
                throw OnBadRequestException("Value of ID is not a valid UUID.");

            /* Load hash from store. */
            var hashRecord = hashStorage.Load(id);
            if (hashRecord == null)
                throw OnBadRequestException($"No hash stored with ID \"{id}\".");

            /* Respond to caller. (Web server will treat strings a text/plain responses.) */
            context.Response.Headers.Append("X-Sender-IP", hashRecord.Value.SenderIP.ToString());
            context.Response.Headers.Append("X-Sent-At", hashRecord.Value.SentAt.ToString());
            return Results.Text(
                Convert.ToBase64String(hashRecord.Value.Hash.ToArray()) + "\r\n", 
                "text/plain", 
                Encoding.ASCII, 
                200);
        }

        private string AddHash(AddHashRequestBody body, HttpContext context)
        {
            /* Pull out the ID property and validate. */
            if (Guid.TryParse(body.ID, out Guid id) == false)
                throw OnBadRequestException("ID property is not a valid UUID.");

            /* Pull out the Hash property and validate. */
            var hashAsBytes = ConvertFromBase64OrNull(body.Hash, 256 / 8);
            if (hashAsBytes == null)
                throw new BadRequestException("Hash must be 256 bits of BASE64.");

            /* Save hash.*/
            var hashRecord = new StoredHash(
                hashAsBytes, 
                context.Connection.RemoteIpAddress ?? System.Net.IPAddress.None, 
                NowService());
            hashStorage.Store(id, hashRecord, AlreadyInUseException(id));           

            /* Return a success response, admonishing the user for 
             * even using this hash service in the first place. */
            return
                "This is an open hash service for testing HashBack implementations\r\n" +
                "only. It is not suitable for production use, indeed not for any\r\n" +
                "purpose that requires security. You should instead be using your\r\n" +
                "own website where only you have control over what files are published.\r\n" +
                "\r\n" +
                "Regards, Bill, billpg.com. \uD83E\uDD89\r\n";
        }

        /// <summary>
        /// Return an callable function (suitable for hashStorage.Store) to call if an
        /// ID is already in use.
        /// </summary>
        /// <param name="id">ID to complain about.</param>
        /// <returns>Callable function to call if exception is needed.</returns>
        private Func<Exception> AlreadyInUseException(Guid id)
            => () => OnBadRequestException($"The ID {id} is already in use.");
        
        /// <summary>
        /// Convert a supplied base-64 encoded hash into 256 bits as bytes,
        /// or NULL if the string is not a valid encoding or not the expected
        /// number of bytes.
        /// </summary>
        /// <param name="hash">Supplied hash to convert.</param>
        /// <param name="expectedByteCount">Exactly how many bytes to expect.</param>
        /// <returns>Byte collection, or null if string is not valid.</returns>
        private static IList<byte>? ConvertFromBase64OrNull(string hash, int expectedByteCount)
        {
            /* Attempt to convert string into bytes. */
            byte[] hashAsBytes;
            try
            {
                hashAsBytes = Convert.FromBase64String(hash);
            }
            catch (FormatException)
            {
                /* Not valid base-64, so return null. */
                return null;
            }

            /* If not expected byte count, return null. */
            if (hashAsBytes.Length != expectedByteCount)
                return null;

            /* Passed tests, return bytes in a read-only package. */
            return hashAsBytes.AsReadOnly();
        }

    }
}
