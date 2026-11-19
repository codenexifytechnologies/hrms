using Codenexify.HRMS.Domain.CoreHr;
using Codenexify.HRMS.Domain.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Codenexify.HRMS.Infrastructure.Persistence;

internal static class PlatformConfig
{
    public static void Audit<T>(EntityTypeBuilder<T> b) where T : Domain.Common.BaseEntity
    {
        b.Property(x => x.CreatedBy).HasMaxLength(200);
        b.Property(x => x.UpdatedBy).HasMaxLength(200);
    }

    public static void Money<T>(EntityTypeBuilder<T> b, params string[] properties) where T : class
    {
        foreach (var p in properties) b.Property(p).HasPrecision(18, 2);
    }

    public static void Rate<T>(EntityTypeBuilder<T> b, params string[] properties) where T : class
    {
        foreach (var p in properties) b.Property(p).HasPrecision(9, 4);
    }
}

internal sealed class StatutoryRateSetConfiguration : IEntityTypeConfiguration<StatutoryRateSet>
{
    public void Configure(EntityTypeBuilder<StatutoryRateSet> b)
    {
        b.ToTable("StatutoryRateSets");
        PlatformConfig.Audit(b);
        PlatformConfig.Rate(b, nameof(StatutoryRateSet.PfEmployeeRate), nameof(StatutoryRateSet.PfEmployerRate), nameof(StatutoryRateSet.EpsRate),
            nameof(StatutoryRateSet.EdliRate), nameof(StatutoryRateSet.PfAdminChargesRate), nameof(StatutoryRateSet.EsiEmployeeRate),
            nameof(StatutoryRateSet.EsiEmployerRate), nameof(StatutoryRateSet.GratuityMinServiceYears), nameof(StatutoryRateSet.GratuityDaysPerYear), nameof(StatutoryRateSet.GratuityWageDivisor));
        PlatformConfig.Money(b, nameof(StatutoryRateSet.PfWageCeiling), nameof(StatutoryRateSet.EsiWageLimit), nameof(StatutoryRateSet.GratuityCeiling));
        b.Property(x => x.Notes).HasMaxLength(1000);
        b.HasIndex(x => x.EffectiveFrom).IsUnique();
    }
}

internal sealed class ProfessionalTaxSlabConfiguration : IEntityTypeConfiguration<ProfessionalTaxSlab>
{
    public void Configure(EntityTypeBuilder<ProfessionalTaxSlab> b)
    {
        b.ToTable("ProfessionalTaxSlabs");
        PlatformConfig.Audit(b);
        b.Property(x => x.State).HasMaxLength(100).IsRequired();
        PlatformConfig.Money(b, nameof(ProfessionalTaxSlab.MinMonthlyGross), nameof(ProfessionalTaxSlab.MaxMonthlyGross), nameof(ProfessionalTaxSlab.MonthlyAmount), nameof(ProfessionalTaxSlab.SpecialMonthAmount));
        b.HasIndex(x => new { x.State, x.EffectiveFrom });
    }
}

internal sealed class LabourWelfareFundRuleConfiguration : IEntityTypeConfiguration<LabourWelfareFundRule>
{
    public void Configure(EntityTypeBuilder<LabourWelfareFundRule> b)
    {
        b.ToTable("LabourWelfareFundRules");
        PlatformConfig.Audit(b);
        b.Property(x => x.State).HasMaxLength(100).IsRequired();
        b.Property(x => x.Months).HasMaxLength(40).IsRequired();
        PlatformConfig.Money(b, nameof(LabourWelfareFundRule.EmployeeAmount), nameof(LabourWelfareFundRule.EmployerAmount));
        b.HasIndex(x => new { x.State, x.EffectiveFrom });
    }
}

internal sealed class IncomeTaxRuleConfiguration : IEntityTypeConfiguration<IncomeTaxRule>
{
    public void Configure(EntityTypeBuilder<IncomeTaxRule> b)
    {
        b.ToTable("IncomeTaxRules");
        PlatformConfig.Audit(b);
        PlatformConfig.Money(b, nameof(IncomeTaxRule.StandardDeduction), nameof(IncomeTaxRule.RebateIncomeLimit), nameof(IncomeTaxRule.RebateMaxAmount),
            nameof(IncomeTaxRule.Section80CLimit), nameof(IncomeTaxRule.Section80DLimit), nameof(IncomeTaxRule.Section80CCD1BLimit), nameof(IncomeTaxRule.HomeLoanInterestLimit));
        PlatformConfig.Rate(b, nameof(IncomeTaxRule.CessRate));
        b.HasIndex(x => new { x.FinancialYear, x.Regime }).IsUnique();
        b.HasMany(x => x.Slabs).WithOne().HasForeignKey(x => x.IncomeTaxRuleId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Surcharges).WithOne().HasForeignKey(x => x.IncomeTaxRuleId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class IncomeTaxSlabConfiguration : IEntityTypeConfiguration<IncomeTaxSlab>
{
    public void Configure(EntityTypeBuilder<IncomeTaxSlab> b)
    {
        b.ToTable("IncomeTaxSlabs");
        PlatformConfig.Money(b, nameof(IncomeTaxSlab.FromIncome), nameof(IncomeTaxSlab.ToIncome));
        PlatformConfig.Rate(b, nameof(IncomeTaxSlab.Rate));
    }
}

internal sealed class IncomeTaxSurchargeConfiguration : IEntityTypeConfiguration<IncomeTaxSurcharge>
{
    public void Configure(EntityTypeBuilder<IncomeTaxSurcharge> b)
    {
        b.ToTable("IncomeTaxSurcharges");
        PlatformConfig.Money(b, nameof(IncomeTaxSurcharge.AboveIncome));
        PlatformConfig.Rate(b, nameof(IncomeTaxSurcharge.Rate));
    }
}

internal sealed class PayrollSettingConfiguration : IEntityTypeConfiguration<PayrollSetting>
{
    public void Configure(EntityTypeBuilder<PayrollSetting> b)
    {
        TenantConfig.Base(b, "PayrollSettings");
        b.HasIndex(x => x.OrganizationId).IsUnique();
        b.Property(x => x.DefaultState).HasMaxLength(100);
        b.Property(x => x.PfEstablishmentCode).HasMaxLength(30);
        b.Property(x => x.EsiEmployerCode).HasMaxLength(30);
        b.Property(x => x.Tan).HasMaxLength(10);
        PlatformConfig.Rate(b, nameof(PayrollSetting.OvertimeRateMultiplier), nameof(PayrollSetting.DailyRateDivisor));
        foreach (var p in new[] { nameof(PayrollSetting.SalaryExpenseAccount), nameof(PayrollSetting.EmployerContributionAccount), nameof(PayrollSetting.SalaryPayableAccount),
                     nameof(PayrollSetting.PfPayableAccount), nameof(PayrollSetting.EsiPayableAccount), nameof(PayrollSetting.PtPayableAccount),
                     nameof(PayrollSetting.LwfPayableAccount), nameof(PayrollSetting.TdsPayableAccount), nameof(PayrollSetting.LoanRecoveryAccount) })
            b.Property(p).HasMaxLength(100).IsRequired();
    }
}

internal sealed class SalaryComponentConfiguration : IEntityTypeConfiguration<SalaryComponent>
{
    public void Configure(EntityTypeBuilder<SalaryComponent> b)
    {
        TenantConfig.Base(b, "SalaryComponents");
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.Code).HasMaxLength(20).IsRequired();
        b.HasIndex(x => new { x.OrganizationId, x.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

internal sealed class SalaryStructureConfiguration : IEntityTypeConfiguration<SalaryStructure>
{
    public void Configure(EntityTypeBuilder<SalaryStructure> b)
    {
        TenantConfig.Base(b, "SalaryStructures");
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.Description).HasMaxLength(500);
        b.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.SalaryStructureId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class SalaryStructureLineConfiguration : IEntityTypeConfiguration<SalaryStructureLine>
{
    public void Configure(EntityTypeBuilder<SalaryStructureLine> b)
    {
        TenantConfig.Base(b, "SalaryStructureLines");
        b.HasOne(x => x.Component).WithMany().HasForeignKey(x => x.SalaryComponentId).OnDelete(DeleteBehavior.Restrict);
        PlatformConfig.Money(b, nameof(SalaryStructureLine.Value));
    }
}

internal sealed class EmployeeSalaryConfiguration : IEntityTypeConfiguration<EmployeeSalary>
{
    public void Configure(EntityTypeBuilder<EmployeeSalary> b)
    {
        TenantConfig.Base(b, "EmployeeSalaries");
        b.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Structure).WithMany().HasForeignKey(x => x.SalaryStructureId).OnDelete(DeleteBehavior.Restrict);
        PlatformConfig.Money(b, nameof(EmployeeSalary.AnnualGross));
        b.Property(x => x.Remarks).HasMaxLength(300);
        b.HasIndex(x => new { x.EmployeeId, x.EffectiveFrom });
    }
}

internal sealed class TaxDeclarationConfiguration : IEntityTypeConfiguration<TaxDeclaration>
{
    public void Configure(EntityTypeBuilder<TaxDeclaration> b)
    {
        TenantConfig.Base(b, "TaxDeclarations");
        b.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        PlatformConfig.Money(b, nameof(TaxDeclaration.Section80C), nameof(TaxDeclaration.Section80D), nameof(TaxDeclaration.Section80CCD1B),
            nameof(TaxDeclaration.HomeLoanInterest), nameof(TaxDeclaration.OtherDeductions), nameof(TaxDeclaration.MonthlyRentPaid),
            nameof(TaxDeclaration.PreviousEmployerIncome), nameof(TaxDeclaration.PreviousEmployerTds));
        b.Property(x => x.ReviewedBy).HasMaxLength(200);
        b.HasIndex(x => new { x.EmployeeId, x.FinancialYear }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

internal sealed class EmployeeLoanConfiguration : IEntityTypeConfiguration<EmployeeLoan>
{
    public void Configure(EntityTypeBuilder<EmployeeLoan> b)
    {
        TenantConfig.Base(b, "EmployeeLoans");
        b.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Repayments).WithOne().HasForeignKey(x => x.EmployeeLoanId).OnDelete(DeleteBehavior.Restrict);
        PlatformConfig.Money(b, nameof(EmployeeLoan.Amount), nameof(EmployeeLoan.InstallmentAmount), nameof(EmployeeLoan.Outstanding));
        b.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        b.Property(x => x.DecidedBy).HasMaxLength(200);
        b.Property(x => x.DecisionNotes).HasMaxLength(500);
        b.HasIndex(x => new { x.OrganizationId, x.Status });
    }
}

internal sealed class LoanRepaymentConfiguration : IEntityTypeConfiguration<LoanRepayment>
{
    public void Configure(EntityTypeBuilder<LoanRepayment> b)
    {
        TenantConfig.Base(b, "LoanRepayments");
        PlatformConfig.Money(b, nameof(LoanRepayment.Amount));
        b.Property(x => x.Note).HasMaxLength(300);
        b.HasOne<PayrollRun>().WithMany().HasForeignKey(x => x.PayrollRunId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PayrollRunConfiguration : IEntityTypeConfiguration<PayrollRun>
{
    public void Configure(EntityTypeBuilder<PayrollRun> b)
    {
        TenantConfig.Base(b, "PayrollRuns");
        b.Property(x => x.FinalizedBy).HasMaxLength(200);
        b.Property(x => x.RowVersion).IsRowVersion();
        PlatformConfig.Money(b, nameof(PayrollRun.TotalGross), nameof(PayrollRun.TotalDeductions), nameof(PayrollRun.TotalNet), nameof(PayrollRun.TotalEmployerCost));
        b.HasIndex(x => new { x.OrganizationId, x.Year, x.Month }).IsUnique().HasFilter("[IsDeleted] = 0");
        b.HasMany(x => x.Details).WithOne(d => d.Run).HasForeignKey(x => x.PayrollRunId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PayrollDetailConfiguration : IEntityTypeConfiguration<PayrollDetail>
{
    public void Configure(EntityTypeBuilder<PayrollDetail> b)
    {
        TenantConfig.Base(b, "PayrollDetails");
        b.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.PayrollDetailId).OnDelete(DeleteBehavior.Restrict);
        PlatformConfig.Money(b, nameof(PayrollDetail.MonthlyGross), nameof(PayrollDetail.GrossEarnings), nameof(PayrollDetail.TotalDeductions), nameof(PayrollDetail.NetPay),
            nameof(PayrollDetail.EmployerContributions), nameof(PayrollDetail.PfWages), nameof(PayrollDetail.EsiWages), nameof(PayrollDetail.TaxableEarnings), nameof(PayrollDetail.ProjectedAnnualTax));
        b.Property(x => x.PaidDays).HasPrecision(6, 2);
        b.Property(x => x.LopDays).HasPrecision(6, 2);
        b.Property(x => x.LopOverride).HasPrecision(6, 2);
        b.Property(x => x.LeaveEncashmentDays).HasPrecision(6, 2);
        b.Property(x => x.HoldReason).HasMaxLength(300);
        b.Property(x => x.State).HasMaxLength(100);
        b.Property(x => x.Warnings).HasMaxLength(1000);
        b.HasIndex(x => new { x.PayrollRunId, x.EmployeeId }).IsUnique().HasFilter("[IsDeleted] = 0");
        b.HasIndex(x => new { x.EmployeeId, x.PayrollRunId });
    }
}

internal sealed class PayrollLineConfiguration : IEntityTypeConfiguration<PayrollLine>
{
    public void Configure(EntityTypeBuilder<PayrollLine> b)
    {
        TenantConfig.Base(b, "PayrollLines");
        b.Property(x => x.Code).HasMaxLength(20).IsRequired();
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        PlatformConfig.Money(b, nameof(PayrollLine.Amount), nameof(PayrollLine.FullAmount));
    }
}

internal sealed class FullAndFinalSettlementConfiguration : IEntityTypeConfiguration<FullAndFinalSettlement>
{
    public void Configure(EntityTypeBuilder<FullAndFinalSettlement> b)
    {
        TenantConfig.Base(b, "FullAndFinalSettlements");
        b.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.FullAndFinalSettlementId).OnDelete(DeleteBehavior.Restrict);
        PlatformConfig.Money(b, nameof(FullAndFinalSettlement.TotalPayable), nameof(FullAndFinalSettlement.TotalRecoverable), nameof(FullAndFinalSettlement.NetAmount));
        b.Property(x => x.ApprovedBy).HasMaxLength(200);
        b.Property(x => x.Notes).HasMaxLength(1000);
        b.HasIndex(x => x.EmployeeId).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

internal sealed class SettlementLineConfiguration : IEntityTypeConfiguration<SettlementLine>
{
    public void Configure(EntityTypeBuilder<SettlementLine> b)
    {
        TenantConfig.Base(b, "SettlementLines");
        b.Property(x => x.Description).HasMaxLength(200).IsRequired();
        PlatformConfig.Money(b, nameof(SettlementLine.Amount));
    }
}
