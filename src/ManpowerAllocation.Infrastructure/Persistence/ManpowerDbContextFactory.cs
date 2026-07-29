using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace ManpowerAllocation.Infrastructure.Persistence;

/// <summary>
/// Design-time factory so the EF Core tools (<c>dotnet ef migrations add</c>,
/// <c>dotnet ef database update</c>) can create the context without starting the web host.
/// It reads the connection string from configuration or the
/// <c>ManpowerAllocation_ConnectionString</c> environment variable, falling back to a
/// local placeholder used only for generating migration scaffolding.
/// </summary>
public sealed class ManpowerDbContextFactory : IDesignTimeDbContextFactory<ManpowerDbContext>
{
    /// <summary>Creates a context instance for design-time tooling.</summary>
    /// <param name="args">Arguments passed by the EF Core tools (unused).</param>
    /// <returns>A configured <see cref="ManpowerDbContext"/>.</returns>
    public ManpowerDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();

        var connectionString =
            configuration["ConnectionStrings:ManpowerDatabase"]
            ?? configuration["ManpowerAllocation_ConnectionString"]
            ?? "Server=(localdb)\\MSSQLLocalDB;Database=ManpowerAllocation;Trusted_Connection=True;MultipleActiveResultSets=true";

        var options = new DbContextOptionsBuilder<ManpowerDbContext>()
            .UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure())
            .Options;

        return new ManpowerDbContext(options);
    }
}
