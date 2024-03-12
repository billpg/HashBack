/* Copyright William Godfrey, 2024. All rights reserved.
 * billpg.com
 */
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using billpg.HashBackService;
using billpg.WebAppTools;

/* Announce the service starting. */
Console.WriteLine("Starting HashBackService.");

/* Load port number from config. */
int port = ServiceConfig.LoadRequiredInt("ListenPort");

/* Open a web service. */
var app = WebApplication.Create();
app.Urls.Add($"http://localhost:{port}");

/* Configure various end-points with handlers. */
app.MapGetRedirectTo("/", ServiceConfig.LoadRequiredString("RedirectHomeTo"));

/* Configure the hash store. */
app.MapGetRedirectTo("/devHashStore", ServiceConfig.LoadRequiredString("RedirectHashStoreTo"));
app.MapGetWrapped("/devHashStore/user", DevHashStoreEndpoints.GetUser);
app.MapPostWrapped("/devHashStore/store", DevHashStoreEndpoints.PostStore);
app.MapGetWrapped("/devHashStore/load/{user}/{file}", DevHashStoreEndpoints.GetStoreHash);

/* Start running and log. */
Console.WriteLine("Running.");
app.Run();


