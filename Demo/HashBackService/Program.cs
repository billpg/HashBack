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
using HashBackService;
using System.Net;

/* Announce the service starting. */
Console.WriteLine("Starting HashBackService.");

/* Load port number from config. */
int port = ServiceConfig.LoadRequiredInt("ListenPort");

/* Open a web service. */
var app = WebApplication.Create();
app.Urls.Add($"http://localhost:{port}");

/* Redirect home page visits to the documentation. */
string redirectHome = ServiceConfig.LoadRequiredString("RedirectHomeTo");
app.MapGet("/", () => Results.Redirect(redirectHome));

/* Configure the hash store. */
var hashSvc = new HashService();
hashSvc.OnBadRequestException = ErrorHandler.BadRequestExceptionWithText;
hashSvc.DocumentationUrl = ServiceConfig.LoadRequiredString("RedirectHashServiceTo");
hashSvc.ConfigureHttpService(app, "/hashes");

/* Configure the issuer (3.0/3.1) demo. */
var issuerSvc = new IssuerService();
string redirectIssuerDemoDocs = ServiceConfig.LoadRequiredString("RedirectIssuerDemoTo");
issuerSvc.RootUrl = app.Urls.Single();
issuerSvc.DocumentationUrl = redirectIssuerDemoDocs;
issuerSvc.OnBadRequest = ErrorHandler.BadRequestExceptionWithJson;
issuerSvc.OnRetrieveVerificationHash = VerificationHashDownload.Retrieve;
issuerSvc.ConfigureHttpService(app, "/issuer");

/* Configure the Bearer Token (4.0) Service. */
var bearerTokenSvc = new BearerTokenService
{
    RootUrl = app.Urls.Single(),
    NowValidationMarginSeconds 
        = ServiceConfig.LoadOptionalInt("NowValidationMarginSeconds") ?? 10,
    OnBadRequest = ErrorHandler.BadRequestExceptionWithText,
    OnReadClock = InternalTools.NowService,
    OnRetrieveVerifyHash = VerificationHashDownload.Retrieve
};
bearerTokenSvc.ConfigureHttpService(app, "/bearerToken");

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


