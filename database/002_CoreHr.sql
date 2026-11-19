BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    CREATE TABLE [Branches] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(150) NOT NULL,
        [Code] nvarchar(20) NOT NULL,
        [AddressLine] nvarchar(300) NULL,
        [City] nvarchar(100) NULL,
        [State] nvarchar(100) NULL,
        [Country] nvarchar(100) NOT NULL,
        [PostalCode] nvarchar(10) NULL,
        [Phone] nvarchar(20) NULL,
        [IsHeadOffice] bit NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_Branches] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Branches_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    CREATE TABLE [Designations] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(150) NOT NULL,
        [Level] int NULL,
        [Description] nvarchar(500) NULL,
        [IsActive] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_Designations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Designations_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    CREATE TABLE [ExitChecklistTemplateItems] (
        [Id] int NOT NULL IDENTITY,
        [Title] nvarchar(200) NOT NULL,
        [Owner] nvarchar(50) NOT NULL,
        [SortOrder] int NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_ExitChecklistTemplateItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ExitChecklistTemplateItems_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    CREATE TABLE [OrganizationSettings] (
        [Id] int NOT NULL IDENTITY,
        [EmployeeIdPrefix] nvarchar(10) NOT NULL,
        [EmployeeIdPadding] int NOT NULL,
        [NextEmployeeNumber] int NOT NULL,
        [DefaultNoticePeriodDays] int NOT NULL,
        [DefaultProbationMonths] int NOT NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_OrganizationSettings] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OrganizationSettings_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    CREATE TABLE [Departments] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(150) NOT NULL,
        [Code] nvarchar(20) NULL,
        [Description] nvarchar(500) NULL,
        [ParentDepartmentId] int NULL,
        [HeadEmployeeId] int NULL,
        [IsActive] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_Departments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Departments_Departments_ParentDepartmentId] FOREIGN KEY ([ParentDepartmentId]) REFERENCES [Departments] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Departments_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    CREATE TABLE [Employees] (
        [Id] int NOT NULL IDENTITY,
        [EmployeeCode] nvarchar(30) NOT NULL,
        [FirstName] nvarchar(100) NOT NULL,
        [MiddleName] nvarchar(100) NULL,
        [LastName] nvarchar(100) NOT NULL,
        [WorkEmail] nvarchar(200) NOT NULL,
        [PersonalEmail] nvarchar(200) NULL,
        [MobilePhone] nvarchar(20) NULL,
        [Gender] nvarchar(40) NULL,
        [DateOfBirth] date NULL,
        [MaritalStatus] nvarchar(40) NULL,
        [BloodGroup] nvarchar(5) NULL,
        [Nationality] nvarchar(60) NOT NULL,
        [CurrentAddress] nvarchar(500) NULL,
        [PermanentAddress] nvarchar(500) NULL,
        [EmergencyContactName] nvarchar(150) NULL,
        [EmergencyContactRelation] nvarchar(50) NULL,
        [EmergencyContactPhone] nvarchar(20) NULL,
        [PanEncrypted] nvarchar(1000) NULL,
        [PanMasked] nvarchar(10) NULL,
        [Uan] nvarchar(12) NULL,
        [BranchId] int NULL,
        [DepartmentId] int NULL,
        [DesignationId] int NULL,
        [ReportingManagerId] int NULL,
        [EmploymentType] nvarchar(40) NOT NULL,
        [Status] nvarchar(40) NOT NULL,
        [DateOfJoining] date NOT NULL,
        [ProbationEndDate] date NULL,
        [ConfirmationDate] date NULL,
        [DateOfExit] date NULL,
        [NoticePeriodDays] int NOT NULL,
        [UserId] int NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_Employees] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Employees_Branches_BranchId] FOREIGN KEY ([BranchId]) REFERENCES [Branches] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Employees_Departments_DepartmentId] FOREIGN KEY ([DepartmentId]) REFERENCES [Departments] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Employees_Designations_DesignationId] FOREIGN KEY ([DesignationId]) REFERENCES [Designations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Employees_Employees_ReportingManagerId] FOREIGN KEY ([ReportingManagerId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Employees_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Employees_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    CREATE TABLE [EmployeeBankDetails] (
        [Id] int NOT NULL IDENTITY,
        [EmployeeId] int NOT NULL,
        [BankName] nvarchar(150) NOT NULL,
        [AccountHolderName] nvarchar(150) NOT NULL,
        [AccountNumberEncrypted] nvarchar(1000) NOT NULL,
        [AccountNumberLast4] nvarchar(4) NOT NULL,
        [Ifsc] nvarchar(11) NOT NULL,
        [BranchName] nvarchar(150) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_EmployeeBankDetails] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_EmployeeBankDetails_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EmployeeBankDetails_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    CREATE TABLE [EmployeeDocuments] (
        [Id] int NOT NULL IDENTITY,
        [EmployeeId] int NOT NULL,
        [Category] nvarchar(40) NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [FileName] nvarchar(255) NOT NULL,
        [StorageKey] nvarchar(300) NOT NULL,
        [ContentType] nvarchar(100) NOT NULL,
        [SizeBytes] bigint NOT NULL,
        [ExpiresOn] date NULL,
        [IsVerified] bit NOT NULL,
        [VerifiedBy] nvarchar(200) NULL,
        [VerifiedAtUtc] datetime2 NULL,
        [VisibleToEmployee] bit NOT NULL,
        [Notes] nvarchar(500) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_EmployeeDocuments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_EmployeeDocuments_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EmployeeDocuments_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    CREATE TABLE [EmployeeEducation] (
        [Id] int NOT NULL IDENTITY,
        [EmployeeId] int NOT NULL,
        [Qualification] nvarchar(150) NOT NULL,
        [Institution] nvarchar(200) NOT NULL,
        [Specialization] nvarchar(150) NULL,
        [YearOfPassing] int NULL,
        [Grade] nvarchar(30) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_EmployeeEducation] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_EmployeeEducation_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EmployeeEducation_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    CREATE TABLE [EmployeeExperience] (
        [Id] int NOT NULL IDENTITY,
        [EmployeeId] int NOT NULL,
        [Company] nvarchar(200) NOT NULL,
        [Designation] nvarchar(150) NULL,
        [FromDate] date NOT NULL,
        [ToDate] date NULL,
        [Notes] nvarchar(500) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_EmployeeExperience] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_EmployeeExperience_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EmployeeExperience_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    CREATE TABLE [ExitRequests] (
        [Id] int NOT NULL IDENTITY,
        [EmployeeId] int NOT NULL,
        [Type] nvarchar(40) NOT NULL,
        [Reason] nvarchar(1000) NOT NULL,
        [InitiatedByEmployee] bit NOT NULL,
        [RequestedOn] date NOT NULL,
        [RequestedLastWorkingDay] date NOT NULL,
        [ApprovedLastWorkingDay] date NULL,
        [Status] nvarchar(40) NOT NULL,
        [DecidedBy] nvarchar(200) NULL,
        [DecidedAtUtc] datetime2 NULL,
        [DecisionNotes] nvarchar(1000) NULL,
        [ExitInterviewNotes] nvarchar(2000) NULL,
        [CompletedAtUtc] datetime2 NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_ExitRequests] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ExitRequests_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ExitRequests_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    CREATE TABLE [ExitChecklistItems] (
        [Id] int NOT NULL IDENTITY,
        [ExitRequestId] int NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Owner] nvarchar(50) NOT NULL,
        [SortOrder] int NOT NULL,
        [IsCompleted] bit NOT NULL,
        [CompletedBy] nvarchar(200) NULL,
        [CompletedAtUtc] datetime2 NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_ExitChecklistItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ExitChecklistItems_ExitRequests_ExitRequestId] FOREIGN KEY ([ExitRequestId]) REFERENCES [ExitRequests] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ExitChecklistItems_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Branches_OrganizationId_Code] ON [Branches] ([OrganizationId], [Code]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    CREATE INDEX [IX_Departments_HeadEmployeeId] ON [Departments] ([HeadEmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    CREATE INDEX [IX_Departments_OrganizationId_Name] ON [Departments] ([OrganizationId], [Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    CREATE INDEX [IX_Departments_ParentDepartmentId] ON [Departments] ([ParentDepartmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    CREATE INDEX [IX_Designations_OrganizationId_Name] ON [Designations] ([OrganizationId], [Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_EmployeeBankDetails_EmployeeId] ON [EmployeeBankDetails] ([EmployeeId]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    CREATE INDEX [IX_EmployeeBankDetails_OrganizationId] ON [EmployeeBankDetails] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    CREATE INDEX [IX_EmployeeDocuments_EmployeeId] ON [EmployeeDocuments] ([EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    CREATE INDEX [IX_EmployeeDocuments_OrganizationId_ExpiresOn] ON [EmployeeDocuments] ([OrganizationId], [ExpiresOn]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    CREATE INDEX [IX_EmployeeEducation_EmployeeId] ON [EmployeeEducation] ([EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    CREATE INDEX [IX_EmployeeEducation_OrganizationId] ON [EmployeeEducation] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    CREATE INDEX [IX_EmployeeExperience_EmployeeId] ON [EmployeeExperience] ([EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    CREATE INDEX [IX_EmployeeExperience_OrganizationId] ON [EmployeeExperience] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    CREATE INDEX [IX_Employees_BranchId] ON [Employees] ([BranchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    CREATE INDEX [IX_Employees_DepartmentId] ON [Employees] ([DepartmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    CREATE INDEX [IX_Employees_DesignationId] ON [Employees] ([DesignationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    CREATE INDEX [IX_Employees_OrganizationId_Status] ON [Employees] ([OrganizationId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    CREATE INDEX [IX_Employees_OrganizationId_WorkEmail] ON [Employees] ([OrganizationId], [WorkEmail]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    CREATE INDEX [IX_Employees_ReportingManagerId] ON [Employees] ([ReportingManagerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Employees_UserId] ON [Employees] ([UserId]) WHERE [UserId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    CREATE UNIQUE INDEX [UX_Employees_OrganizationId_Code] ON [Employees] ([OrganizationId], [EmployeeCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    CREATE INDEX [IX_ExitChecklistItems_ExitRequestId] ON [ExitChecklistItems] ([ExitRequestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    CREATE INDEX [IX_ExitChecklistItems_OrganizationId] ON [ExitChecklistItems] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    CREATE INDEX [IX_ExitChecklistTemplateItems_OrganizationId] ON [ExitChecklistTemplateItems] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    CREATE INDEX [IX_ExitRequests_EmployeeId] ON [ExitRequests] ([EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    CREATE INDEX [IX_ExitRequests_OrganizationId_Status] ON [ExitRequests] ([OrganizationId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    CREATE UNIQUE INDEX [IX_OrganizationSettings_OrganizationId] ON [OrganizationSettings] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    ALTER TABLE [Departments] ADD CONSTRAINT [FK_Departments_Employees_HeadEmployeeId] FOREIGN KEY ([HeadEmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112020634_CoreHr'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261112020634_CoreHr', N'9.0.10');
END;

COMMIT;
GO

