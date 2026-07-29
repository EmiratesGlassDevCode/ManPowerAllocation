using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

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
        // Read the connection string directly from the environment to keep this design-time
        // factory free of additional configuration package dependencies. ASP.NET Core maps
        // "ConnectionStrings:ManpowerDatabase" to the "ConnectionStrings__ManpowerDatabase" variable.
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__ManpowerDatabase")
            ?? Environment.GetEnvironmentVariable("ManpowerAllocation_ConnectionString")
            ?? "Server=(localdb)\\MSSQLLocalDB;Database=ManpowerAllocation;Trusted_Connection=True;MultipleActiveResultSets=true";

        var options = new DbContextOptionsBuilder<ManpowerDbContext>()
            .UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure())
            .Options;

        return new ManpowerDbContext(options);
    }
}
