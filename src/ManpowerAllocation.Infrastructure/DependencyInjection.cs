using ManpowerAllocation.Application.Abstractions;
using ManpowerAllocation.Application.BreakGlass;
using ManpowerAllocation.Infrastructure.Alerts;
using ManpowerAllocation.Infrastructure.Attendance;
using ManpowerAllocation.Infrastructure.Auditing;
using ManpowerAllocation.Infrastructure.BreakGlass;
using ManpowerAllocation.Infrastructure.Import;
using ManpowerAllocation.Infrastructure.Persistence;
using ManpowerAllocation.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ManpowerAllocation.Infrastructure;

/// <summary>Registers the infrastructure implementations of the application abstractions.</summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds EF Core (MSSQL) persistence, the audit writer, the break-glass service and its
    /// background worker, outbound alerting and Excel import parsing to the container.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="configuration">Application configuration providing the connection string and options.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("ManpowerDatabase")
            ?? throw new InvalidOperationException("Connection string 'ManpowerDatabase' is not configured.");

        services.AddDbContext<ManpowerDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
            {
                sql.EnableRetryOnFailure();
                sql.MigrationsAssembly(typeof(ManpowerDbContext).Assembly.FullName);
            }));

        // Expose the context through the application-layer abstraction.
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ManpowerDbContext>());

        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<IAuditWriter, AuditWriter>();
        services.AddScoped<IExcelImportParser, ClosedXmlImportParser>();
        services.AddScoped<IBreakGlassService, BreakGlassService>();
        services.AddScoped<DatabaseInitializer>();

        // Options bound from protected configuration; the break-glass secret hash lives here,
        // never in the database.
        services.AddOptions<BreakGlassOptions>()
            .Bind(configuration.GetSection(BreakGlassOptions.SectionName));
        services.AddOptions<AlertOptions>()
            .Bind(configuration.GetSection(AlertOptions.SectionName));

        services.AddHttpClient(nameof(EmailTeamsAlertService));
        services.AddScoped<IAlertService, EmailTeamsAlertService>();

        // Enforces the four-hour auto-disable window and detects manual enables out of band.
        services.AddHostedService<BreakGlassLifecycleWorker>();

        AddAttendanceIntegration(services, configuration);

        return services;
    }

    /// <summary>
    /// Wires the external attendance integration. When a connection string is configured a
    /// read-only context and real presence provider are registered; otherwise a no-op provider is
    /// used so the sync stays safe. The scheduled worker is always registered and no-ops when
    /// disabled or unconfigured.
    /// </summary>
    private static void AddAttendanceIntegration(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AttendanceOptions>()
            .Bind(configuration.GetSection(AttendanceOptions.SectionName));

        var attendanceConnectionString = configuration.GetConnectionString("AttendanceDatabase");
        if (!string.IsNullOrWhiteSpace(attendanceConnectionString))
        {
            services.AddDbContext<AttendanceReadDbContext>(options =>
                options.UseSqlServer(attendanceConnectionString, sql => sql.EnableRetryOnFailure()));
            services.AddScoped<IPresenceProvider, AttendancePresenceProvider>();
        }
        else
        {
            services.AddScoped<IPresenceProvider, NullPresenceProvider>();
        }

        services.AddHostedService<AttendanceSyncWorker>();
    }
}
