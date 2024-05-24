/* Copyright William Godfrey, 2024. All rights reserved.
 * billpg.com
 */
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using billpg.HashBackService;
using Newtonsoft.Json.Linq;
using billpg.HashBackCore;
using System.Text;
using System.Security.Cryptography;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Mvc;
using System.Net;

if (false)
{
    var hls = new HostLookupService();
    var xreq =
        Spartan.Request
        .GET(new Uri("https://xn--8s9h.billpg.com/hashback/123.txt"))
        .WithHeader("Accept", "text/plain");
    var resp = Spartan.Run(xreq, msg => new ApplicationException(msg), hls.Lookup);
}  

/* Announce the service starting. */
Console.WriteLine("Starting HashBackService.");

/* Load port number from config. */
int port = ServiceConfig.LoadRequiredInt("ListenPort");

/* Open a web service. */
var app = WebApplication.Create();
app.Urls.Add($"http://localhost:{port}");
string rootUrlAsString = ServiceConfig.LoadOptionalString("RootUrl") ?? app.Urls.Single();
Uri rootUrl = new Uri(rootUrlAsString);

/* Redirect home page visits to the documentation. */
string redirectHome = ServiceConfig.LoadRequiredString("RedirectHomeTo");
app.MapGet("/", () => Results.Redirect(redirectHome));

/* Configure the hash store. */
var hashSvc = new HashService();
hashSvc.OnBadRequestException = ErrorHandler.BadRequestExceptionWithText;
hashSvc.DocumentationUrl = ServiceConfig.LoadRequiredString("RedirectHashServiceTo");
hashSvc.ConfigureHttpService(app, "/hashes");

/* Configure the host lookup service. */
var hostLookupSvc = new HostLookupService();

/* Configure the download service. */
var downloadSvc = new DownloadService
{
    RootUrl = rootUrl,
    OnDownloadError = ErrorHandler.BadRequestExceptionWithText,
    OnHostLookup = hostLookupSvc.Lookup,
    AlwaysAllow = ServiceConfig.LoadStrings("AlwaysAllowDownload")
};
//downloadSvc.ConfigureHttpService(app, "/allowDownload");

/* Configure the issuer (3.0/3.1) demo. */
var issuerSvc = new IssuerService();
string redirectIssuerDemoDocs = ServiceConfig.LoadRequiredString("RedirectIssuerDemoTo");
issuerSvc.RootUrl = rootUrlAsString;
issuerSvc.DocumentationUrl = redirectIssuerDemoDocs;
issuerSvc.OnBadRequest = ErrorHandler.BadRequestExceptionWithJson;
issuerSvc.OnRetrieveVerificationHash = downloadSvc.HashDownload;
issuerSvc.ConfigureHttpService(app, "/issuer");

/* Configure the Authorization header (4.0) validation service. */
var authHeaderSvc = new AuthorizationHeaderService
{
    RootUrl = rootUrl,
    OnBadRequest = ErrorHandler.BadRequestExceptionWithText,   
    OnRetrieveVerifyHash = downloadSvc.HashDownload,
    OnReadClock = InternalTools.NowService,
    ClockMarginSeconds = 999
};

/* Configure the Bearer Token (4.0) Service. */
var bearerTokenSvc = new BearerTokenService
{
    RootUrl = rootUrl,
    TokenExpirySeconds = 999,
    OnAuthorizationHeader = authHeaderSvc.Handle
};
bearerTokenSvc.ConfigureHttpService(app, "/tokens");

/* Configure the "Hello" Service, that also takes Hashback 4.0. */
var helloSvc = new HelloService();
helloSvc.RootUrl = rootUrl;
helloSvc.OnAuthorizationHeader = authHeaderSvc.Handle;
helloSvc.ConfigureHttpService(app, "/hello");


#if false
/* Configure the token request service used by the caller demo. */
var tokenRequesterSvc = new TokenRequesterService();

/* Configure the caller demo. */
var callerSvc = new CallerService();
callerSvc.RootUrl = app.Urls.Single();
callerSvc.DocumentationUrl = ServiceConfig.LoadRequiredString("RedirectCallerDemoTo");
callerSvc.OnBadRequest = ErrorHandler.BadRequestExceptionWithText;
callerSvc.HashService = hashSvc;
callerSvc.TokenRequestService = tokenRequesterSvc;
callerSvc.ConfigureHttpService(app, "/caller");
#endif 

/* Set up the custom error handler. */
app.Use(async (context, next) =>
{
    try { await next(context); }
    catch (Exception ex)
    { ErrorHandler.Handle(ex, context); }
});

/* Start running and log. */
Console.WriteLine("Running.");
app.Run();


