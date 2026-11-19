BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    ALTER TABLE [LeaveBalances] ADD [EncashmentPaid] decimal(6,2) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE TABLE [EmployeeLoans] (
        [Id] int NOT NULL IDENTITY,
        [EmployeeId] int NOT NULL,
        [Type] nvarchar(40) NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [InstallmentAmount] decimal(18,2) NOT NULL,
        [RecoveryStart] date NOT NULL,
        [Outstanding] decimal(18,2) NOT NULL,
        [Reason] nvarchar(500) NOT NULL,
        [Status] nvarchar(40) NOT NULL,
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
        CONSTRAINT [PK_EmployeeLoans] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_EmployeeLoans_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EmployeeLoans_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE TABLE [FullAndFinalSettlements] (
        [Id] int NOT NULL IDENTITY,
        [EmployeeId] int NOT NULL,
        [LastWorkingDay] date NOT NULL,
        [Status] nvarchar(40) NOT NULL,
        [TotalPayable] decimal(18,2) NOT NULL,
        [TotalRecoverable] decimal(18,2) NOT NULL,
        [NetAmount] decimal(18,2) NOT NULL,
        [ApprovedBy] nvarchar(200) NULL,
        [ApprovedAtUtc] datetime2 NULL,
        [PaidOn] date NULL,
        [Notes] nvarchar(1000) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_FullAndFinalSettlements] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_FullAndFinalSettlements_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_FullAndFinalSettlements_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE TABLE [IncomeTaxRules] (
        [Id] int NOT NULL IDENTITY,
        [FinancialYear] int NOT NULL,
        [Regime] nvarchar(40) NOT NULL,
        [StandardDeduction] decimal(18,2) NOT NULL,
        [RebateIncomeLimit] decimal(18,2) NOT NULL,
        [RebateMaxAmount] decimal(18,2) NOT NULL,
        [CessRate] decimal(9,4) NOT NULL,
        [Section80CLimit] decimal(18,2) NOT NULL,
        [Section80DLimit] decimal(18,2) NOT NULL,
        [Section80CCD1BLimit] decimal(18,2) NOT NULL,
        [HomeLoanInterestLimit] decimal(18,2) NOT NULL,
        [IsVerified] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_IncomeTaxRules] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE TABLE [LabourWelfareFundRules] (
        [Id] int NOT NULL IDENTITY,
        [State] nvarchar(100) NOT NULL,
        [EffectiveFrom] date NOT NULL,
        [EmployeeAmount] decimal(18,2) NOT NULL,
        [EmployerAmount] decimal(18,2) NOT NULL,
        [Months] nvarchar(40) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_LabourWelfareFundRules] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE TABLE [PayrollRuns] (
        [Id] int NOT NULL IDENTITY,
        [Year] int NOT NULL,
        [Month] int NOT NULL,
        [Status] nvarchar(40) NOT NULL,
        [ProcessedAtUtc] datetime2 NULL,
        [FinalizedAtUtc] datetime2 NULL,
        [FinalizedBy] nvarchar(200) NULL,
        [PaidOn] date NULL,
        [EmployeeCount] int NOT NULL,
        [TotalGross] decimal(18,2) NOT NULL,
        [TotalDeductions] decimal(18,2) NOT NULL,
        [TotalNet] decimal(18,2) NOT NULL,
        [TotalEmployerCost] decimal(18,2) NOT NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_PayrollRuns] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PayrollRuns_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE TABLE [PayrollSettings] (
        [Id] int NOT NULL IDENTITY,
        [PfEnabled] bit NOT NULL,
        [PfRestrictToCeiling] bit NOT NULL,
        [EsiEnabled] bit NOT NULL,
        [PtEnabled] bit NOT NULL,
        [LwfEnabled] bit NOT NULL,
        [TdsEnabled] bit NOT NULL,
        [DefaultState] nvarchar(100) NULL,
        [PfEstablishmentCode] nvarchar(30) NULL,
        [EsiEmployerCode] nvarchar(30) NULL,
        [Tan] nvarchar(10) NULL,
        [OvertimeRateMultiplier] decimal(9,4) NOT NULL,
        [DailyRateDivisor] decimal(9,4) NOT NULL,
        [PayDay] int NOT NULL,
        [SalaryExpenseAccount] nvarchar(100) NOT NULL,
        [EmployerContributionAccount] nvarchar(100) NOT NULL,
        [SalaryPayableAccount] nvarchar(100) NOT NULL,
        [PfPayableAccount] nvarchar(100) NOT NULL,
        [EsiPayableAccount] nvarchar(100) NOT NULL,
        [PtPayableAccount] nvarchar(100) NOT NULL,
        [LwfPayableAccount] nvarchar(100) NOT NULL,
        [TdsPayableAccount] nvarchar(100) NOT NULL,
        [LoanRecoveryAccount] nvarchar(100) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_PayrollSettings] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PayrollSettings_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE TABLE [ProfessionalTaxSlabs] (
        [Id] int NOT NULL IDENTITY,
        [State] nvarchar(100) NOT NULL,
        [EffectiveFrom] date NOT NULL,
        [Gender] nvarchar(40) NULL,
        [MinMonthlyGross] decimal(18,2) NOT NULL,
        [MaxMonthlyGross] decimal(18,2) NULL,
        [MonthlyAmount] decimal(18,2) NOT NULL,
        [SpecialMonth] int NULL,
        [SpecialMonthAmount] decimal(18,2) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_ProfessionalTaxSlabs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE TABLE [SalaryComponents] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(100) NOT NULL,
        [Code] nvarchar(20) NOT NULL,
        [Type] nvarchar(40) NOT NULL,
        [IsTaxable] bit NOT NULL,
        [IsPfWage] bit NOT NULL,
        [IsEsiWage] bit NOT NULL,
        [IsProrated] bit NOT NULL,
        [IsHra] bit NOT NULL,
        [IsBasic] bit NOT NULL,
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
        CONSTRAINT [PK_SalaryComponents] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SalaryComponents_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE TABLE [SalaryStructures] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(100) NOT NULL,
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
        CONSTRAINT [PK_SalaryStructures] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SalaryStructures_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE TABLE [StatutoryRateSets] (
        [Id] int NOT NULL IDENTITY,
        [EffectiveFrom] date NOT NULL,
        [PfEmployeeRate] decimal(9,4) NOT NULL,
        [PfEmployerRate] decimal(9,4) NOT NULL,
        [EpsRate] decimal(9,4) NOT NULL,
        [PfWageCeiling] decimal(18,2) NOT NULL,
        [EdliRate] decimal(9,4) NOT NULL,
        [PfAdminChargesRate] decimal(9,4) NOT NULL,
        [EsiEmployeeRate] decimal(9,4) NOT NULL,
        [EsiEmployerRate] decimal(9,4) NOT NULL,
        [EsiWageLimit] decimal(18,2) NOT NULL,
        [GratuityMinServiceYears] decimal(9,4) NOT NULL,
        [GratuityDaysPerYear] decimal(9,4) NOT NULL,
        [GratuityWageDivisor] decimal(9,4) NOT NULL,
        [GratuityCeiling] decimal(18,2) NOT NULL,
        [IsVerified] bit NOT NULL,
        [Notes] nvarchar(1000) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_StatutoryRateSets] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE TABLE [TaxDeclarations] (
        [Id] int NOT NULL IDENTITY,
        [EmployeeId] int NOT NULL,
        [FinancialYear] int NOT NULL,
        [Regime] nvarchar(40) NOT NULL,
        [Section80C] decimal(18,2) NOT NULL,
        [Section80D] decimal(18,2) NOT NULL,
        [Section80CCD1B] decimal(18,2) NOT NULL,
        [HomeLoanInterest] decimal(18,2) NOT NULL,
        [OtherDeductions] decimal(18,2) NOT NULL,
        [MonthlyRentPaid] decimal(18,2) NOT NULL,
        [MetroCity] bit NOT NULL,
        [PreviousEmployerIncome] decimal(18,2) NOT NULL,
        [PreviousEmployerTds] decimal(18,2) NOT NULL,
        [Status] nvarchar(40) NOT NULL,
        [ReviewedBy] nvarchar(200) NULL,
        [ReviewedAtUtc] datetime2 NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_TaxDeclarations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TaxDeclarations_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TaxDeclarations_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE TABLE [SettlementLines] (
        [Id] int NOT NULL IDENTITY,
        [FullAndFinalSettlementId] int NOT NULL,
        [Description] nvarchar(200) NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [IsManual] bit NOT NULL,
        [SortOrder] int NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_SettlementLines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SettlementLines_FullAndFinalSettlements_FullAndFinalSettlementId] FOREIGN KEY ([FullAndFinalSettlementId]) REFERENCES [FullAndFinalSettlements] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SettlementLines_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE TABLE [IncomeTaxSlabs] (
        [Id] int NOT NULL IDENTITY,
        [IncomeTaxRuleId] int NOT NULL,
        [FromIncome] decimal(18,2) NOT NULL,
        [ToIncome] decimal(18,2) NULL,
        [Rate] decimal(9,4) NOT NULL,
        CONSTRAINT [PK_IncomeTaxSlabs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_IncomeTaxSlabs_IncomeTaxRules_IncomeTaxRuleId] FOREIGN KEY ([IncomeTaxRuleId]) REFERENCES [IncomeTaxRules] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE TABLE [IncomeTaxSurcharges] (
        [Id] int NOT NULL IDENTITY,
        [IncomeTaxRuleId] int NOT NULL,
        [AboveIncome] decimal(18,2) NOT NULL,
        [Rate] decimal(9,4) NOT NULL,
        CONSTRAINT [PK_IncomeTaxSurcharges] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_IncomeTaxSurcharges_IncomeTaxRules_IncomeTaxRuleId] FOREIGN KEY ([IncomeTaxRuleId]) REFERENCES [IncomeTaxRules] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE TABLE [LoanRepayments] (
        [Id] int NOT NULL IDENTITY,
        [EmployeeLoanId] int NOT NULL,
        [Date] date NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [PayrollRunId] int NULL,
        [Note] nvarchar(300) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_LoanRepayments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LoanRepayments_EmployeeLoans_EmployeeLoanId] FOREIGN KEY ([EmployeeLoanId]) REFERENCES [EmployeeLoans] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_LoanRepayments_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_LoanRepayments_PayrollRuns_PayrollRunId] FOREIGN KEY ([PayrollRunId]) REFERENCES [PayrollRuns] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE TABLE [PayrollDetails] (
        [Id] int NOT NULL IDENTITY,
        [PayrollRunId] int NOT NULL,
        [EmployeeId] int NOT NULL,
        [DaysInMonth] int NOT NULL,
        [PaidDays] decimal(6,2) NOT NULL,
        [LopDays] decimal(6,2) NOT NULL,
        [LopOverride] decimal(6,2) NULL,
        [OnHold] bit NOT NULL,
        [HoldReason] nvarchar(300) NULL,
        [MonthlyGross] decimal(18,2) NOT NULL,
        [GrossEarnings] decimal(18,2) NOT NULL,
        [TotalDeductions] decimal(18,2) NOT NULL,
        [NetPay] decimal(18,2) NOT NULL,
        [EmployerContributions] decimal(18,2) NOT NULL,
        [PfWages] decimal(18,2) NOT NULL,
        [EsiWages] decimal(18,2) NOT NULL,
        [TaxableEarnings] decimal(18,2) NOT NULL,
        [ProjectedAnnualTax] decimal(18,2) NOT NULL,
        [LeaveEncashmentDays] decimal(6,2) NOT NULL,
        [Regime] nvarchar(40) NOT NULL,
        [State] nvarchar(100) NULL,
        [Warnings] nvarchar(1000) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_PayrollDetails] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PayrollDetails_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PayrollDetails_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PayrollDetails_PayrollRuns_PayrollRunId] FOREIGN KEY ([PayrollRunId]) REFERENCES [PayrollRuns] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE TABLE [EmployeeSalaries] (
        [Id] int NOT NULL IDENTITY,
        [EmployeeId] int NOT NULL,
        [SalaryStructureId] int NOT NULL,
        [AnnualGross] decimal(18,2) NOT NULL,
        [EffectiveFrom] date NOT NULL,
        [PfApplicable] bit NOT NULL,
        [Remarks] nvarchar(300) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_EmployeeSalaries] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_EmployeeSalaries_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EmployeeSalaries_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EmployeeSalaries_SalaryStructures_SalaryStructureId] FOREIGN KEY ([SalaryStructureId]) REFERENCES [SalaryStructures] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE TABLE [SalaryStructureLines] (
        [Id] int NOT NULL IDENTITY,
        [SalaryStructureId] int NOT NULL,
        [SalaryComponentId] int NOT NULL,
        [Calculation] nvarchar(40) NOT NULL,
        [Value] decimal(18,2) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_SalaryStructureLines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SalaryStructureLines_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SalaryStructureLines_SalaryComponents_SalaryComponentId] FOREIGN KEY ([SalaryComponentId]) REFERENCES [SalaryComponents] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SalaryStructureLines_SalaryStructures_SalaryStructureId] FOREIGN KEY ([SalaryStructureId]) REFERENCES [SalaryStructures] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE TABLE [PayrollLines] (
        [Id] int NOT NULL IDENTITY,
        [PayrollDetailId] int NOT NULL,
        [Code] nvarchar(20) NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [Kind] nvarchar(40) NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [FullAmount] decimal(18,2) NULL,
        [IsTaxable] bit NOT NULL,
        [SortOrder] int NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(200) NULL,
        [OrganizationId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_PayrollLines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PayrollLines_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PayrollLines_PayrollDetails_PayrollDetailId] FOREIGN KEY ([PayrollDetailId]) REFERENCES [PayrollDetails] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE INDEX [IX_EmployeeLoans_EmployeeId] ON [EmployeeLoans] ([EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE INDEX [IX_EmployeeLoans_OrganizationId_Status] ON [EmployeeLoans] ([OrganizationId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE INDEX [IX_EmployeeSalaries_EmployeeId_EffectiveFrom] ON [EmployeeSalaries] ([EmployeeId], [EffectiveFrom]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE INDEX [IX_EmployeeSalaries_OrganizationId] ON [EmployeeSalaries] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE INDEX [IX_EmployeeSalaries_SalaryStructureId] ON [EmployeeSalaries] ([SalaryStructureId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_FullAndFinalSettlements_EmployeeId] ON [FullAndFinalSettlements] ([EmployeeId]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE INDEX [IX_FullAndFinalSettlements_OrganizationId] ON [FullAndFinalSettlements] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE UNIQUE INDEX [IX_IncomeTaxRules_FinancialYear_Regime] ON [IncomeTaxRules] ([FinancialYear], [Regime]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE INDEX [IX_IncomeTaxSlabs_IncomeTaxRuleId] ON [IncomeTaxSlabs] ([IncomeTaxRuleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE INDEX [IX_IncomeTaxSurcharges_IncomeTaxRuleId] ON [IncomeTaxSurcharges] ([IncomeTaxRuleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE INDEX [IX_LabourWelfareFundRules_State_EffectiveFrom] ON [LabourWelfareFundRules] ([State], [EffectiveFrom]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE INDEX [IX_LoanRepayments_EmployeeLoanId] ON [LoanRepayments] ([EmployeeLoanId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE INDEX [IX_LoanRepayments_OrganizationId] ON [LoanRepayments] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE INDEX [IX_LoanRepayments_PayrollRunId] ON [LoanRepayments] ([PayrollRunId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE INDEX [IX_PayrollDetails_EmployeeId_PayrollRunId] ON [PayrollDetails] ([EmployeeId], [PayrollRunId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE INDEX [IX_PayrollDetails_OrganizationId] ON [PayrollDetails] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_PayrollDetails_PayrollRunId_EmployeeId] ON [PayrollDetails] ([PayrollRunId], [EmployeeId]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE INDEX [IX_PayrollLines_OrganizationId] ON [PayrollLines] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE INDEX [IX_PayrollLines_PayrollDetailId] ON [PayrollLines] ([PayrollDetailId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_PayrollRuns_OrganizationId_Year_Month] ON [PayrollRuns] ([OrganizationId], [Year], [Month]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PayrollSettings_OrganizationId] ON [PayrollSettings] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE INDEX [IX_ProfessionalTaxSlabs_State_EffectiveFrom] ON [ProfessionalTaxSlabs] ([State], [EffectiveFrom]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_SalaryComponents_OrganizationId_Code] ON [SalaryComponents] ([OrganizationId], [Code]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE INDEX [IX_SalaryStructureLines_OrganizationId] ON [SalaryStructureLines] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE INDEX [IX_SalaryStructureLines_SalaryComponentId] ON [SalaryStructureLines] ([SalaryComponentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE INDEX [IX_SalaryStructureLines_SalaryStructureId] ON [SalaryStructureLines] ([SalaryStructureId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE INDEX [IX_SalaryStructures_OrganizationId] ON [SalaryStructures] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE INDEX [IX_SettlementLines_FullAndFinalSettlementId] ON [SettlementLines] ([FullAndFinalSettlementId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE INDEX [IX_SettlementLines_OrganizationId] ON [SettlementLines] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE UNIQUE INDEX [IX_StatutoryRateSets_EffectiveFrom] ON [StatutoryRateSets] ([EffectiveFrom]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_TaxDeclarations_EmployeeId_FinancialYear] ON [TaxDeclarations] ([EmployeeId], [FinancialYear]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    CREATE INDEX [IX_TaxDeclarations_OrganizationId] ON [TaxDeclarations] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112032206_Payroll'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261112032206_Payroll', N'9.0.10');
END;

COMMIT;
GO

