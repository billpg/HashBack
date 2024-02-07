using billpg.CrteDemo;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

Console.WriteLine("CRTE DEMO STARTED");

var app = WebApplication.Create();
app.Urls.Add("http://localhost:3000");

app.MapGetHtml("/", WebAssets.RootIndex);
app.MapGetHtml("/Issuer/", WebAssets.IssuerIndex);
app.MapGetHtml("/GrantPermission/", WebAssets.GrantPermission);
app.MapResources("/Assets", typeof(WebAssets));
app.MapPost("/Issuer/TokenCall", IssuerDemo.TokenCall);

Console.WriteLine("Running.");
app.Run();

