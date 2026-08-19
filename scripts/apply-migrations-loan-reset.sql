-- =============================================================================
-- Manpower Allocation — schema upgrade for the Loan / Shift-Reset / Pools /
-- Master-History release.
--
-- Applies the three pending EF Core migrations:
--   20260819151039_AddHomeDepartmentAndPool
--   20260819153930_AddAllocationSettings
--   20260819155015_AddMasterSnapshots
--
-- This script is IDEMPOTENT: every step is guarded, so it is safe to run more
-- than once and safe to run against a database that is already partly upgraded.
--
-- Run it as a login that HAS DDL rights (db_ddladmin or db_owner) against the
-- application database. Afterwards the app's normal runtime login (read/write
-- only) will find nothing left to migrate at startup, so it will start cleanly.
--
-- Usage (SQLCMD):  sqlcmd -S <server> -d <database> -i apply-migrations-loan-reset.sql
--        (SSMS):   open the file, make sure the correct database is selected, Execute.
-- =============================================================================

IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

-- ---------------------------------------------------------------------------
-- 20260819151039_AddHomeDepartmentAndPool
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260819151039_AddHomeDepartmentAndPool')
    AND NOT EXISTS (SELECT * FROM sys.columns WHERE [name] = N'HomeDepartmentId' AND [object_id] = OBJECT_ID(N'[Employees]'))
BEGIN
    ALTER TABLE [Employees] ADD [HomeDepartmentId] int NULL;
END;
GO

IF NOT EXISTS (SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260819151039_AddHomeDepartmentAndPool')
    AND NOT EXISTS (SELECT * FROM sys.columns WHERE [name] = N'IsPool' AND [object_id] = OBJECT_ID(N'[Departments]'))
BEGIN
    ALTER TABLE [Departments] ADD [IsPool] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260819151039_AddHomeDepartmentAndPool')
    AND NOT EXISTS (SELECT * FROM sys.indexes WHERE [name] = N'IX_Employees_HomeDepartmentId' AND [object_id] = OBJECT_ID(N'[Employees]'))
BEGIN
    CREATE INDEX [IX_Employees_HomeDepartmentId] ON [Employees] ([HomeDepartmentId]);
END;
GO

IF NOT EXISTS (SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260819151039_AddHomeDepartmentAndPool')
    AND NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE [name] = N'FK_Employees_Departments_HomeDepartmentId')
BEGIN
    ALTER TABLE [Employees] ADD CONSTRAINT [FK_Employees_Departments_HomeDepartmentId]
        FOREIGN KEY ([HomeDepartmentId]) REFERENCES [Departments] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260819151039_AddHomeDepartmentAndPool')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260819151039_AddHomeDepartmentAndPool', N'8.0.8');
END;
GO

-- ---------------------------------------------------------------------------
-- 20260819153930_AddAllocationSettings
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260819153930_AddAllocationSettings')
    AND OBJECT_ID(N'[AllocationSettings]') IS NULL
BEGIN
    CREATE TABLE [AllocationSettings] (
        [Id] int NOT NULL,
        [AutoShiftResetEnabled] bit NOT NULL,
        [LastMasterUploadUtc] datetime2 NULL,
        [LastResetMarker] nvarchar(32) NULL,
        [LastResetAtUtc] datetime2 NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        [UpdatedByObjectId] nvarchar(100) NULL,
        CONSTRAINT [PK_AllocationSettings] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260819153930_AddAllocationSettings')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260819153930_AddAllocationSettings', N'8.0.8');
END;
GO

-- ---------------------------------------------------------------------------
-- 20260819155015_AddMasterSnapshots
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260819155015_AddMasterSnapshots')
    AND OBJECT_ID(N'[MasterSnapshots]') IS NULL
BEGIN
    CREATE TABLE [MasterSnapshots] (
        [Id] bigint NOT NULL IDENTITY,
        [CapturedAtUtc] datetime2 NOT NULL,
        [CapturedByObjectId] nvarchar(64) NULL,
        [CapturedByName] nvarchar(256) NULL,
        [Source] nvarchar(64) NOT NULL,
        [EmployeeCount] int NOT NULL,
        [DepartmentCount] int NOT NULL,
        CONSTRAINT [PK_MasterSnapshots] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260819155015_AddMasterSnapshots')
    AND OBJECT_ID(N'[MasterSnapshotDepartments]') IS NULL
BEGIN
    CREATE TABLE [MasterSnapshotDepartments] (
        [Id] bigint NOT NULL IDENTITY,
        [SnapshotId] bigint NOT NULL,
        [DepartmentName] nvarchar(120) NOT NULL,
        [Division] int NOT NULL,
        [RequiredDay] int NOT NULL,
        [RequiredNight] int NOT NULL,
        [IsPool] bit NOT NULL,
        CONSTRAINT [PK_MasterSnapshotDepartments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MasterSnapshotDepartments_MasterSnapshots_SnapshotId]
            FOREIGN KEY ([SnapshotId]) REFERENCES [MasterSnapshots] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260819155015_AddMasterSnapshots')
    AND OBJECT_ID(N'[MasterSnapshotEmployees]') IS NULL
BEGIN
    CREATE TABLE [MasterSnapshotEmployees] (
        [Id] bigint NOT NULL IDENTITY,
        [SnapshotId] bigint NOT NULL,
        [EmployeeId] int NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [BadgeNumber] nvarchar(50) NULL,
        [Division] int NOT NULL,
        [HomeDepartmentName] nvarchar(120) NOT NULL,
        [Shift] int NOT NULL,
        [IsSupply] bit NOT NULL,
        CONSTRAINT [PK_MasterSnapshotEmployees] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MasterSnapshotEmployees_MasterSnapshots_SnapshotId]
            FOREIGN KEY ([SnapshotId]) REFERENCES [MasterSnapshots] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260819155015_AddMasterSnapshots')
    AND NOT EXISTS (SELECT * FROM sys.indexes WHERE [name] = N'IX_MasterSnapshotDepartments_SnapshotId' AND [object_id] = OBJECT_ID(N'[MasterSnapshotDepartments]'))
BEGIN
    CREATE INDEX [IX_MasterSnapshotDepartments_SnapshotId] ON [MasterSnapshotDepartments] ([SnapshotId]);
END;
GO

IF NOT EXISTS (SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260819155015_AddMasterSnapshots')
    AND NOT EXISTS (SELECT * FROM sys.indexes WHERE [name] = N'IX_MasterSnapshotEmployees_SnapshotId' AND [object_id] = OBJECT_ID(N'[MasterSnapshotEmployees]'))
BEGIN
    CREATE INDEX [IX_MasterSnapshotEmployees_SnapshotId] ON [MasterSnapshotEmployees] ([SnapshotId]);
END;
GO

IF NOT EXISTS (SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260819155015_AddMasterSnapshots')
    AND NOT EXISTS (SELECT * FROM sys.indexes WHERE [name] = N'IX_MasterSnapshots_CapturedAtUtc' AND [object_id] = OBJECT_ID(N'[MasterSnapshots]'))
BEGIN
    CREATE INDEX [IX_MasterSnapshots_CapturedAtUtc] ON [MasterSnapshots] ([CapturedAtUtc]);
END;
GO

IF NOT EXISTS (SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260819155015_AddMasterSnapshots')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260819155015_AddMasterSnapshots', N'8.0.8');
END;
GO

COMMIT;
GO
