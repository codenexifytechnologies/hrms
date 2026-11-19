BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112023952_TimeAttendance'
)
BEGIN
    ALTER TABLE [OrganizationSettings] ADD [LatePoints] int NOT NULL DEFAULT 3;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112023952_TimeAttendance'
)
BEGIN
    ALTER TABLE [OrganizationSettings] ADD [OnTimePoints] int NOT NULL DEFAULT 10;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112023952_TimeAttendance'
)
BEGIN
    CREATE TABLE [AttendancePunches] (
        [Id] int NOT NULL IDENTITY,
        [EmployeeId] int NOT NULL,
        [TimestampUtc] datetime2 NOT NULL,
        [AttendanceDate] date NOT NULL,
        [Direction] nvarchar(40) NOT NULL,
        [Source] nvarchar(40) NOT NULL,
        [IpAddress] nvarchar(64) NULL,
        [Note] nvarchar(300) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_AttendancePunches] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AttendancePunches_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_AttendancePunches_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112023952_TimeAttendance'
)
BEGIN
    CREATE TABLE [Holidays] (
        [Id] int NOT NULL IDENTITY,
        [Date] date NOT NULL,
        [Name] nvarchar(150) NOT NULL,
        [IsOptional] bit NOT NULL,
        [BranchId] int NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_Holidays] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Holidays_Branches_BranchId] FOREIGN KEY ([BranchId]) REFERENCES [Branches] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Holidays_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112023952_TimeAttendance'
)
BEGIN
    CREATE TABLE [LeaveTypes] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(100) NOT NULL,
        [Code] nvarchar(10) NOT NULL,
        [Color] nvarchar(7) NOT NULL,
        [IsPaid] bit NOT NULL,
        [AnnualQuota] decimal(6,2) NOT NULL,
        [AccrualMethod] nvarchar(40) NOT NULL,
        [CarryForwardLimit] decimal(6,2) NOT NULL,
        [MaxEncashableDaysPerYear] decimal(6,2) NOT NULL,
        [AllowHalfDay] bit NOT NULL,
        [MinNoticeDays] int NOT NULL,
        [ApplicableGender] nvarchar(40) NULL,
        [IsActive] bit NOT NULL,
        [SortOrder] int NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_LeaveTypes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LeaveTypes_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112023952_TimeAttendance'
)
BEGIN
    CREATE TABLE [Notifications] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [Title] nvarchar(150) NOT NULL,
        [Message] nvarchar(500) NOT NULL,
        [Link] nvarchar(300) NULL,
        [IsRead] bit NOT NULL,
        [ReadAtUtc] datetime2 NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_Notifications] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Notifications_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Notifications_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112023952_TimeAttendance'
)
BEGIN
    CREATE TABLE [OvertimeRequests] (
        [Id] int NOT NULL IDENTITY,
        [EmployeeId] int NOT NULL,
        [Date] date NOT NULL,
        [Minutes] int NOT NULL,
        [Reason] nvarchar(500) NOT NULL,
        [Status] nvarchar(40) NOT NULL,
        [ApproverUserId] int NULL,
        [ApprovedMinutes] int NULL,
        [DecidedBy] nvarchar(200) NULL,
        [DecidedAtUtc] datetime2 NULL,
        [DecisionNotes] nvarchar(500) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_OvertimeRequests] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OvertimeRequests_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_OvertimeRequests_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_OvertimeRequests_Users_ApproverUserId] FOREIGN KEY ([ApproverUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112023952_TimeAttendance'
)
BEGIN
    CREATE TABLE [Shifts] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(100) NOT NULL,
        [Code] nvarchar(10) NOT NULL,
        [StartTime] time NOT NULL,
        [EndTime] time NOT NULL,
        [BreakMinutes] int NOT NULL,
        [GraceMinutes] int NOT NULL,
        [MinHoursFullDay] decimal(4,2) NOT NULL,
        [MinHoursHalfDay] decimal(4,2) NOT NULL,
        [OvertimeThresholdMinutes] int NOT NULL,
        [WeeklyOffs] nvarchar(100) NOT NULL,
        [IsDefault] bit NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_Shifts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Shifts_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112023952_TimeAttendance'
)
BEGIN
    CREATE TABLE [LeaveApplications] (
        [Id] int NOT NULL IDENTITY,
        [EmployeeId] int NOT NULL,
        [LeaveTypeId] int NOT NULL,
        [FromDate] date NOT NULL,
        [ToDate] date NOT NULL,
        [IsHalfDay] bit NOT NULL,
        [HalfDaySession] nvarchar(40) NULL,
        [Days] decimal(6,2) NOT NULL,
        [Reason] nvarchar(500) NOT NULL,
        [Status] nvarchar(40) NOT NULL,
        [ApproverUserId] int NULL,
        [DecidedBy] nvarchar(200) NULL,
        [DecidedAtUtc] datetime2 NULL,
        [DecisionNotes] nvarchar(500) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_LeaveApplications] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LeaveApplications_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_LeaveApplications_LeaveTypes_LeaveTypeId] FOREIGN KEY ([LeaveTypeId]) REFERENCES [LeaveTypes] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_LeaveApplications_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_LeaveApplications_Users_ApproverUserId] FOREIGN KEY ([ApproverUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112023952_TimeAttendance'
)
BEGIN
    CREATE TABLE [LeaveBalances] (
        [Id] int NOT NULL IDENTITY,
        [EmployeeId] int NOT NULL,
        [LeaveTypeId] int NOT NULL,
        [Year] int NOT NULL,
        [Opening] decimal(6,2) NOT NULL,
        [Accrued] decimal(6,2) NOT NULL,
        [Adjusted] decimal(6,2) NOT NULL,
        [Used] decimal(6,2) NOT NULL,
        [Encashed] decimal(6,2) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_LeaveBalances] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LeaveBalances_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_LeaveBalances_LeaveTypes_LeaveTypeId] FOREIGN KEY ([LeaveTypeId]) REFERENCES [LeaveTypes] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_LeaveBalances_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112023952_TimeAttendance'
)
BEGIN
    CREATE TABLE [Attendance] (
        [Id] int NOT NULL IDENTITY,
        [EmployeeId] int NOT NULL,
        [Date] date NOT NULL,
        [ShiftId] int NULL,
        [FirstInUtc] datetime2 NULL,
        [LastOutUtc] datetime2 NULL,
        [WorkedMinutes] int NOT NULL,
        [LateMinutes] int NOT NULL,
        [OvertimeMinutes] int NOT NULL,
        [Status] nvarchar(40) NOT NULL,
        [IsRegularized] bit NOT NULL,
        [Remarks] nvarchar(300) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_Attendance] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Attendance_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Attendance_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Attendance_Shifts_ShiftId] FOREIGN KEY ([ShiftId]) REFERENCES [Shifts] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112023952_TimeAttendance'
)
BEGIN
    CREATE TABLE [ShiftAssignments] (
        [Id] int NOT NULL IDENTITY,
        [EmployeeId] int NOT NULL,
        [ShiftId] int NOT NULL,
        [EffectiveFrom] date NOT NULL,
        [EffectiveTo] date NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_ShiftAssignments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ShiftAssignments_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ShiftAssignments_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ShiftAssignments_Shifts_ShiftId] FOREIGN KEY ([ShiftId]) REFERENCES [Shifts] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112023952_TimeAttendance'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Attendance_EmployeeId_Date] ON [Attendance] ([EmployeeId], [Date]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112023952_TimeAttendance'
)
BEGIN
    CREATE INDEX [IX_Attendance_OrganizationId_Date] ON [Attendance] ([OrganizationId], [Date]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112023952_TimeAttendance'
)
BEGIN
    CREATE INDEX [IX_Attendance_ShiftId] ON [Attendance] ([ShiftId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112023952_TimeAttendance'
)
BEGIN
    CREATE INDEX [IX_AttendancePunches_EmployeeId_AttendanceDate] ON [AttendancePunches] ([EmployeeId], [AttendanceDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112023952_TimeAttendance'
)
BEGIN
    CREATE INDEX [IX_AttendancePunches_OrganizationId] ON [AttendancePunches] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112023952_TimeAttendance'
)
BEGIN
    CREATE INDEX [IX_Holidays_BranchId] ON [Holidays] ([BranchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112023952_TimeAttendance'
)
BEGIN
    CREATE INDEX [IX_Holidays_OrganizationId_Date] ON [Holidays] ([OrganizationId], [Date]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112023952_TimeAttendance'
)
BEGIN
    CREATE INDEX [IX_LeaveApplications_ApproverUserId_Status] ON [LeaveApplications] ([ApproverUserId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112023952_TimeAttendance'
)
BEGIN
    CREATE INDEX [IX_LeaveApplications_EmployeeId_FromDate_ToDate] ON [LeaveApplications] ([EmployeeId], [FromDate], [ToDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112023952_TimeAttendance'
)
BEGIN
    CREATE INDEX [IX_LeaveApplications_LeaveTypeId] ON [LeaveApplications] ([LeaveTypeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112023952_TimeAttendance'
)
BEGIN
    CREATE INDEX [IX_LeaveApplications_OrganizationId_Status] ON [LeaveApplications] ([OrganizationId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112023952_TimeAttendance'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_LeaveBalances_EmployeeId_LeaveTypeId_Year] ON [LeaveBalances] ([EmployeeId], [LeaveTypeId], [Year]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112023952_TimeAttendance'
)
BEGIN
    CREATE INDEX [IX_LeaveBalances_LeaveTypeId] ON [LeaveBalances] ([LeaveTypeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112023952_TimeAttendance'
)
BEGIN
    CREATE INDEX [IX_LeaveBalances_OrganizationId] ON [LeaveBalances] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112023952_TimeAttendance'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_LeaveTypes_OrganizationId_Code] ON [LeaveTypes] ([OrganizationId], [Code]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112023952_TimeAttendance'
)
BEGIN
    CREATE INDEX [IX_Notifications_OrganizationId] ON [Notifications] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112023952_TimeAttendance'
)
BEGIN
    CREATE INDEX [IX_Notifications_UserId_IsRead] ON [Notifications] ([UserId], [IsRead]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112023952_TimeAttendance'
)
BEGIN
    CREATE INDEX [IX_OvertimeRequests_ApproverUserId] ON [OvertimeRequests] ([ApproverUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112023952_TimeAttendance'
)
BEGIN
    CREATE INDEX [IX_OvertimeRequests_EmployeeId_Date] ON [OvertimeRequests] ([EmployeeId], [Date]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112023952_TimeAttendance'
)
BEGIN
    CREATE INDEX [IX_OvertimeRequests_OrganizationId_Status] ON [OvertimeRequests] ([OrganizationId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112023952_TimeAttendance'
)
BEGIN
    CREATE INDEX [IX_ShiftAssignments_EmployeeId_EffectiveFrom] ON [ShiftAssignments] ([EmployeeId], [EffectiveFrom]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112023952_TimeAttendance'
)
BEGIN
    CREATE INDEX [IX_ShiftAssignments_OrganizationId] ON [ShiftAssignments] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112023952_TimeAttendance'
)
BEGIN
    CREATE INDEX [IX_ShiftAssignments_ShiftId] ON [ShiftAssignments] ([ShiftId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112023952_TimeAttendance'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Shifts_OrganizationId_Code] ON [Shifts] ([OrganizationId], [Code]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112023952_TimeAttendance'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261112023952_TimeAttendance', N'9.0.10');
END;

COMMIT;
GO

