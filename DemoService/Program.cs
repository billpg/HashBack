using DemoService;
using DemoService.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.Annotations;
using System.IO;
using System.Reflection;
using System.Text;

// Read the command line for a "Allow Get Localhost" flag.
ServiceData.AllowGetLocalhost
    = Environment.GetCommandLineArgs().Contains("GetLocalhost");

// Web Service core handler.
var builder = WebApplication.CreateBuilder(args);

// Add controllers and OpenAPI/Swagger generator
builder.Services.AddControllers();

// Persist ServiceData as a singleton service so state is kept across requests
builder.Services.AddSingleton<ServiceData>();

// Register IP filter and IHttpGetter implementation
builder.Services.AddSingleton<IIpFilter, IpFilter>();
builder.Services.AddSingleton<IHttpGetter, HttpGetter>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "DemoService", Version = "v1", Description = "Demo HashBack service API" });

    // Enable attribute annotations like [SwaggerOperation]
    c.EnableAnnotations();

    // Include XML comments (requires GenerateDocumentationFile in .csproj)
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        c.IncludeXmlComments(xmlPath);
});


// Configure Kestrel to listen on localhost:9001 (HTTP only, loopback)
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(9001); // HTTP only on loopback
});

var app = builder.Build();

// Global exception handling middleware -- converts exceptions into HTTP responses
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Add IP rate limiting middleware early so it applies to all requests.
// The middleware will return 429 Too Many Requests when a single IP exceeds
// the configured request limit in the sliding window.
app.UseMiddleware<IpRateLimitMiddleware>();

// Serve generated OpenAPI JSON at /openapi/v1.json and expose a friendly /openapi redirect
app.UseSwagger(c =>
{
    // expose JSON at /openapi/{documentName}.json (e.g. /openapi/v1.json)
    c.RouteTemplate = "openapi/{documentName}.json";
});

// Enable attribute routed controllers
app.MapControllers();

// Optional: a short redirect so clients that request /openapi get the real JSON
app.MapGet("/openapi", (HttpContext ctx) =>
{
    ctx.Response.Redirect("/openapi/v1.json", permanent: false);
    return Results.StatusCode(StatusCodes.Status302Found);
});

// Return the generated HTML home page
app.MapGet("/", () => Results.Content(HtmlPages.Home(), "text/html; charset=utf-8"));

// Start the service. This function will continue until the process stops.
app.Run();
