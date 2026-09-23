using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DemoService.Data;

/// <summary>Lets "dotnet ef" commands run without booting the full app.</summary>
public class HashDbContextFactory : IDesignTimeDbContextFactory<HashDbContext>
{
    public HashDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<HashDbContext>();
        optionsBuilder.UseSqlite($"Data Source={ServiceData.DbFilePath}");
        return new HashDbContext(optionsBuilder.Options);
    }
}
