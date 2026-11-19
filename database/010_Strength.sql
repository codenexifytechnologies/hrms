BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    ALTER TABLE [OrganizationSettings] ADD [GeoMode] nvarchar(20) NOT NULL DEFAULT N'Off';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    ALTER TABLE [Employees] ADD [GeoFenceExempt] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    ALTER TABLE [AttendancePunches] ADD [AccuracyMeters] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    ALTER TABLE [AttendancePunches] ADD [DistanceMeters] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    ALTER TABLE [AttendancePunches] ADD [Latitude] decimal(9,6) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    ALTER TABLE [AttendancePunches] ADD [LocationStatus] nvarchar(20) NOT NULL DEFAULT N'NotCaptured';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    ALTER TABLE [AttendancePunches] ADD [Longitude] decimal(9,6) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    ALTER TABLE [AttendancePunches] ADD [OfficeLocationId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    CREATE TABLE [Assets] (
        [Id] int NOT NULL IDENTITY,
        [AssetTag] nvarchar(40) NOT NULL,
        [Name] nvarchar(150) NOT NULL,
        [Category] nvarchar(60) NOT NULL,
        [SerialNumber] nvarchar(100) NULL,
        [Description] nvarchar(500) NULL,
        [PurchaseDate] date NULL,
        [PurchaseCost] decimal(18,2) NULL,
        [WarrantyUntil] date NULL,
        [Status] nvarchar(40) NOT NULL,
        [Condition] nvarchar(40) NOT NULL,
        [CurrentEmployeeId] int NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_Assets] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Assets_Employees_CurrentEmployeeId] FOREIGN KEY ([CurrentEmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Assets_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    CREATE TABLE [FeedbackThreads] (
        [Id] int NOT NULL IDENTITY,
        [Category] nvarchar(60) NOT NULL,
        [Subject] nvarchar(150) NOT NULL,
        [Status] nvarchar(40) NOT NULL,
        [AccessCodeHash] nvarchar(64) NOT NULL,
        [CreatedOn] date NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_FeedbackThreads] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_FeedbackThreads_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    CREATE TABLE [OfficeLocations] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(100) NOT NULL,
        [BranchId] int NULL,
        [Latitude] decimal(9,6) NOT NULL,
        [Longitude] decimal(9,6) NOT NULL,
        [RadiusMeters] int NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_OfficeLocations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OfficeLocations_Branches_BranchId] FOREIGN KEY ([BranchId]) REFERENCES [Branches] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_OfficeLocations_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    CREATE TABLE [Surveys] (
        [Id] int NOT NULL IDENTITY,
        [Title] nvarchar(150) NOT NULL,
        [Description] nvarchar(1000) NULL,
        [Kind] nvarchar(40) NOT NULL,
        [IsAnonymous] bit NOT NULL,
        [Status] nvarchar(40) NOT NULL,
        [DepartmentId] int NULL,
        [DurationDays] int NOT NULL,
        [OpenedAtUtc] datetime2 NULL,
        [ClosesAtUtc] datetime2 NULL,
        [MinResponses] int NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_Surveys] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Surveys_Departments_DepartmentId] FOREIGN KEY ([DepartmentId]) REFERENCES [Departments] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Surveys_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    CREATE TABLE [AssetAssignments] (
        [Id] int NOT NULL IDENTITY,
        [AssetId] int NOT NULL,
        [EmployeeId] int NOT NULL,
        [AssignedOn] date NOT NULL,
        [ExpectedReturnOn] date NULL,
        [ConditionOut] nvarchar(40) NOT NULL,
        [AssignedBy] nvarchar(150) NOT NULL,
        [AcknowledgedAtUtc] datetime2 NULL,
        [ReturnedOn] date NULL,
        [ConditionIn] nvarchar(40) NULL,
        [ReturnedTo] nvarchar(150) NULL,
        [Notes] nvarchar(500) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_AssetAssignments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AssetAssignments_Assets_AssetId] FOREIGN KEY ([AssetId]) REFERENCES [Assets] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_AssetAssignments_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_AssetAssignments_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    CREATE TABLE [FeedbackMessages] (
        [Id] int NOT NULL IDENTITY,
        [ThreadId] int NOT NULL,
        [FromHr] bit NOT NULL,
        [AuthorName] nvarchar(150) NULL,
        [Body] nvarchar(4000) NOT NULL,
        [SentOn] date NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_FeedbackMessages] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_FeedbackMessages_FeedbackThreads_ThreadId] FOREIGN KEY ([ThreadId]) REFERENCES [FeedbackThreads] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_FeedbackMessages_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    CREATE TABLE [SurveyParticipations] (
        [SurveyId] int NOT NULL,
        [UserId] int NOT NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_SurveyParticipations] PRIMARY KEY ([SurveyId], [UserId]),
        CONSTRAINT [FK_SurveyParticipations_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SurveyParticipations_Surveys_SurveyId] FOREIGN KEY ([SurveyId]) REFERENCES [Surveys] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SurveyParticipations_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    CREATE TABLE [SurveyQuestions] (
        [Id] int NOT NULL IDENTITY,
        [SurveyId] int NOT NULL,
        [SortOrder] int NOT NULL,
        [Text] nvarchar(300) NOT NULL,
        [Type] nvarchar(40) NOT NULL,
        [Options] nvarchar(1000) NULL,
        [Required] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_SurveyQuestions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SurveyQuestions_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SurveyQuestions_Surveys_SurveyId] FOREIGN KEY ([SurveyId]) REFERENCES [Surveys] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    CREATE TABLE [SurveyResponses] (
        [Id] int NOT NULL IDENTITY,
        [SurveyId] int NOT NULL,
        [RespondentUserId] int NULL,
        [RespondentName] nvarchar(150) NULL,
        [SubmittedOn] date NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_SurveyResponses] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SurveyResponses_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SurveyResponses_Surveys_SurveyId] FOREIGN KEY ([SurveyId]) REFERENCES [Surveys] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    CREATE TABLE [SurveyAnswers] (
        [Id] int NOT NULL IDENTITY,
        [ResponseId] int NOT NULL,
        [QuestionId] int NOT NULL,
        [Number] int NULL,
        [Text] nvarchar(2000) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_SurveyAnswers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SurveyAnswers_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SurveyAnswers_SurveyQuestions_QuestionId] FOREIGN KEY ([QuestionId]) REFERENCES [SurveyQuestions] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SurveyAnswers_SurveyResponses_ResponseId] FOREIGN KEY ([ResponseId]) REFERENCES [SurveyResponses] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    CREATE INDEX [IX_AttendancePunches_OfficeLocationId] ON [AttendancePunches] ([OfficeLocationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    CREATE INDEX [IX_AssetAssignments_AssetId] ON [AssetAssignments] ([AssetId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    CREATE INDEX [IX_AssetAssignments_EmployeeId_ReturnedOn] ON [AssetAssignments] ([EmployeeId], [ReturnedOn]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    CREATE INDEX [IX_AssetAssignments_OrganizationId] ON [AssetAssignments] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    CREATE INDEX [IX_Assets_CurrentEmployeeId] ON [Assets] ([CurrentEmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Assets_OrganizationId_AssetTag] ON [Assets] ([OrganizationId], [AssetTag]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    CREATE INDEX [IX_Assets_OrganizationId_Status] ON [Assets] ([OrganizationId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    CREATE INDEX [IX_FeedbackMessages_OrganizationId] ON [FeedbackMessages] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    CREATE INDEX [IX_FeedbackMessages_ThreadId] ON [FeedbackMessages] ([ThreadId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    CREATE UNIQUE INDEX [IX_FeedbackThreads_AccessCodeHash] ON [FeedbackThreads] ([AccessCodeHash]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    CREATE INDEX [IX_FeedbackThreads_OrganizationId] ON [FeedbackThreads] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    CREATE INDEX [IX_OfficeLocations_BranchId] ON [OfficeLocations] ([BranchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    CREATE INDEX [IX_OfficeLocations_OrganizationId] ON [OfficeLocations] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    CREATE INDEX [IX_SurveyAnswers_OrganizationId] ON [SurveyAnswers] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    CREATE INDEX [IX_SurveyAnswers_QuestionId] ON [SurveyAnswers] ([QuestionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    CREATE INDEX [IX_SurveyAnswers_ResponseId] ON [SurveyAnswers] ([ResponseId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    CREATE INDEX [IX_SurveyParticipations_OrganizationId] ON [SurveyParticipations] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    CREATE INDEX [IX_SurveyParticipations_UserId] ON [SurveyParticipations] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    CREATE INDEX [IX_SurveyQuestions_OrganizationId] ON [SurveyQuestions] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    CREATE INDEX [IX_SurveyQuestions_SurveyId] ON [SurveyQuestions] ([SurveyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    CREATE INDEX [IX_SurveyResponses_OrganizationId] ON [SurveyResponses] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    CREATE INDEX [IX_SurveyResponses_SurveyId] ON [SurveyResponses] ([SurveyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    CREATE INDEX [IX_Surveys_DepartmentId] ON [Surveys] ([DepartmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    CREATE INDEX [IX_Surveys_OrganizationId_Status] ON [Surveys] ([OrganizationId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    ALTER TABLE [AttendancePunches] ADD CONSTRAINT [FK_AttendancePunches_OfficeLocations_OfficeLocationId] FOREIGN KEY ([OfficeLocationId]) REFERENCES [OfficeLocations] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112092632_Strength'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261112092632_Strength', N'9.0.10');
END;

COMMIT;
GO

