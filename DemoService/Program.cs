using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi.Models;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Add controllers and OpenAPI/Swagger generator
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "DemoService", Version = "v1" });
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
