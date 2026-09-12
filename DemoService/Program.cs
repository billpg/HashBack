using DemoService;
using DemoService.Data;
using DemoService.Services;
using billpg.SpartanHttpClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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

// Register IP filter.
builder.Services.AddSingleton<IIpFilter, IpFilter>();

// The real engine behind every SpartanRequest this service builds directly (currently
// just CallPermissionChecker's own well-known fetch). Tests substitute a fake
// ISpartanEngine instead of talking real HTTP.
builder.Services.AddSingleton<ISpartanEngine, SpartanEngine>();

// Singleton so its in-memory permission cache actually persists across requests.
builder.Services.AddSingleton<ICallPermissionChecker, CallPermissionChecker>();

// The /hash store, backed by PostgreSQL. The connection string (including its password)
// is deliberately never checked into source: set it via
//   dotnet user-secrets set "ConnectionStrings:HashDb" "Host=...;Database=...;Username=...;Password=..." --project DemoService
// for local development, or the ConnectionStrings__HashDb environment variable in production.
// HashController and CallController both depend on this, so it's a hard requirement now -
// fail fast at startup rather than serve requests that can only ever fail.
var hashDbConnectionString = builder.Configuration.GetConnectionString("HashDb")
    ?? throw new InvalidOperationException(
        "Missing ConnectionStrings:HashDb configuration. Set it via 'dotnet user-secrets set " +
        "\"ConnectionStrings:HashDb\" \"Host=...;Database=...;Username=...;Password=...\"' for " +
        "local development, or the ConnectionStrings__HashDb environment variable in production.");
builder.Services.AddDbContext<HashDbContext>(options => options.UseNpgsql(hashDbConnectionString));
builder.Services.AddScoped<IHashStore, HashStore>();
builder.Services.AddScoped<IHelloRequestLog, HelloRequestLog>();
builder.Services.AddScoped<IOutboundGetLog, OutboundGetLog>();
builder.Services.AddHostedService<HashCleanupService>();

// The HTTP getter. Scoped (not Singleton) because it now logs every fetch via
// IOutboundGetLog, which is itself Scoped to match HashDbContext.
builder.Services.AddScoped<IHttpGetter, HttpGetter>();

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

// Confirm the /hash store's tables exist, creating (or updating) them if not. Safe to run
// on every startup - a database already at the latest migration is a no-op.
using (var startupScope = app.Services.CreateScope())
{
    var hashDb = startupScope.ServiceProvider.GetRequiredService<HashDbContext>();
    hashDb.Database.Migrate();
}

// Log every request - first in the pipeline so it captures everything, even a request a
// later middleware goes on to reject.
app.UseMiddleware<RequestLoggingMiddleware>();

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

// Documentation pages.
app.MapGet("/permit", () => Results.Content(HtmlPages.Permit(), "text/html; charset=utf-8"));
app.MapGet("/", () => Results.Content(HtmlPages.Home(), "text/html; charset=utf-8"));
app.MapGet("/.well-known/demo-hashback-dev.json", () => Results.Redirect("https://www.youtube.com/watch?v=dQw4w9WgXcQ"));

// Start the service. This function will continue until the process stops.
app.Run();
