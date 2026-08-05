using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using DemoService;
using System.Reflection;
using System.IO;
using Swashbuckle.AspNetCore.Annotations;
using Microsoft.OpenApi;
using DemoService.Services;

var builder = WebApplication.CreateBuilder(args);

// Add controllers and OpenAPI/Swagger generator
builder.Services.AddControllers();

// Persist ServiceData as a singleton service so state is kept across requests
builder.Services.AddSingleton<ServiceData>();

// Register IHttpGetter as a typed HttpClient service
builder.Services.AddHttpClient<IHttpGetter, HttpGetter>();

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

app.MapGet("/", () => Results.Text("DemoService running on http://localhost:9001"));

app.Run();
