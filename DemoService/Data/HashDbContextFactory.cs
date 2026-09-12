using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace DemoService.Data;

/// <summary>
/// Lets "dotnet ef" commands run without booting the full app. Reads the same
/// ConnectionStrings:HashDb configuration the real app would (appsettings.json, User
/// Secrets, then environment variables, in that order) so "dotnet ef database update" uses
/// your real connection - falling back to a placeholder, never actually connected to, only
/// when nothing real is configured (so "migrations add" still works with no database
/// available at all, e.g. to generate a migration file for review before you have Postgres
/// set up).
/// </summary>
public class HashDbContextFactory : IDesignTimeDbContextFactory<HashDbContext>
{
    public HashDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddUserSecrets<HashDbContext>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        string connectionString = configuration.GetConnectionString("HashDb")
            ?? "Host=localhost;Database=hashback;Username=hashback;Password=unused-design-time-placeholder";

        var optionsBuilder = new DbContextOptionsBuilder<HashDbContext>();
        optionsBuilder.UseNpgsql(connectionString);
        return new HashDbContext(optionsBuilder.Options);
    }
}
