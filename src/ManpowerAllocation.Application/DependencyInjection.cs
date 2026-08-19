using FluentValidation;
using ManpowerAllocation.Application.Attendance;
using ManpowerAllocation.Application.Auditing;
using ManpowerAllocation.Application.Dashboard;
using ManpowerAllocation.Application.Departments;
using ManpowerAllocation.Application.Employees;
using ManpowerAllocation.Application.Import;
using ManpowerAllocation.Application.Roles;
using ManpowerAllocation.Application.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace ManpowerAllocation.Application;

/// <summary>Registers the application layer's use-case services and validators.</summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds the application services and all FluentValidation validators to the container.
    /// Infrastructure implementations (persistence, auditing, break-glass, alerts, import
    /// parsing) are registered separately by the infrastructure layer.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IDepartmentService, DepartmentService>();
        services.AddScoped<IEmployeeService, EmployeeService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IMasterDataImportService, MasterDataImportService>();
        services.AddScoped<IAuditReadService, AuditReadService>();
        services.AddScoped<IAttendanceSyncService, AttendanceSyncService>();
        services.AddScoped<Reconciliation.IReconciliationService, Reconciliation.ReconciliationService>();
        services.AddScoped<IShiftSettingsService, ShiftSettingsService>();
        services.AddScoped<Shifts.IShiftScheduleService, Shifts.ShiftScheduleService>();
        services.AddScoped<Snapshots.IAllocationSnapshotService, Snapshots.AllocationSnapshotService>();
        services.AddScoped<Snapshots.IAllocationHistoryService, Snapshots.AllocationHistoryService>();
        services.AddScoped<Email.IEmailSettingsService, Email.EmailSettingsService>();
        services.AddScoped<DepartmentHeads.IDepartmentHeadService, DepartmentHeads.DepartmentHeadService>();
        services.AddScoped<Absences.IAbsenceCategoryService, Absences.AbsenceCategoryService>();
        services.AddScoped<Absences.IAbsenceService, Absences.AbsenceService>();
        services.AddScoped<Analytics.IAnalyticsService, Analytics.AnalyticsService>();
        services.AddScoped<Allocation.IAllocationResetService, Allocation.AllocationResetService>();
        services.AddScoped<MasterHistory.IMasterHistoryService, MasterHistory.MasterHistoryService>();

        // Shared holder for the last attendance-sync result shown on the admin screen.
        services.AddSingleton<AttendanceSyncStatus>();

        // Liveness of the daily-snapshot worker, for the health endpoint and the nav footer.
        services.AddSingleton<Snapshots.SnapshotHeartbeat>();

        // Most recent daily-report email outcome shown on the admin screen.
        services.AddSingleton<Email.EmailSendStatus>();

        // Registers every AbstractValidator in this assembly (Create/Update department,
        // employee status/shift/move, role upsert, and so on).
        services.AddValidatorsFromAssemblyContaining<CreateDepartmentRequestValidator>(ServiceLifetime.Scoped);

        return services;
    }
}
