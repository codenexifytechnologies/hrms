using System.ComponentModel.DataAnnotations;
using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Application.Common;
using Codenexify.HRMS.Application.Time;
using Codenexify.HRMS.Domain.CoreHr;
using Codenexify.HRMS.Domain.Payroll;
using Microsoft.EntityFrameworkCore;

namespace Codenexify.HRMS.Application.Payroll;

/// <summary>Starting payroll configuration for a new organization; everything is editable.</summary>
public static class PayrollDefaults
{
    public static void AddTo(IAppDbContext db, int organizationId)
    {
        db.PayrollSettings.Add(new PayrollSetting { OrganizationId = organizationId });
        var basic = new SalaryComponent { OrganizationId = organizationId, Name = "Basic", Code = "BASIC", IsBasic = true, IsPfWage = true, SortOrder = 10 };
        var hra = new SalaryComponent { OrganizationId = organizationId, Name = "House Rent Allowance", Code = "HRA", IsHra = true, SortOrder = 20 };
        var special = new SalaryComponent { OrganizationId = organizationId, Name = "Special Allowance", Code = "SPECIAL", SortOrder = 90 };
        db.SalaryComponents.AddRange(basic, hra, special);
        db.SalaryStructures.Add(new SalaryStructure
        {
            OrganizationId = organizationId, Name = "Standard", Description = "Basic 50% of gross, HRA 40% of basic, balance as special allowance",
            Lines =
            [
                new() { OrganizationId = organizationId, Component = basic, Calculation = ComponentCalculation.PercentOfGross, Value = 50 },
                new() { OrganizationId = organizationId, Component = hra, Calculation = ComponentCalculation.PercentOfBasic, Value = 40 },
                new() { OrganizationId = organizationId, Component = special, Calculation = ComponentCalculation.Balancing, Value = 0 }
            ]
        });
    }
}

public sealed class PayrollSettingsInput
{
    [Display(Name = "Provident Fund")] public bool PfEnabled { get; set; } = true;
    [Display(Name = "Restrict PF to the wage ceiling")] public bool PfRestrictToCeiling { get; set; } = true;
    [Display(Name = "ESI")] public bool EsiEnabled { get; set; } = true;
    [Display(Name = "Professional tax")] public bool PtEnabled { get; set; } = true;
    [Display(Name = "Labour welfare fund")] public bool LwfEnabled { get; set; } = true;
    [Display(Name = "Income tax (TDS)")] public bool TdsEnabled { get; set; } = true;
    [StringLength(100), Display(Name = "Default state (PT/LWF)")] public string? DefaultState { get; set; }
    [StringLength(30), Display(Name = "PF establishment code")] public string? PfEstablishmentCode { get; set; }
    [StringLength(30), Display(Name = "ESI employer code")] public string? EsiEmployerCode { get; set; }
    [StringLength(10), RegularExpression("^[A-Za-z]{4}[0-9]{5}[A-Za-z]$", ErrorMessage = "Enter a valid TAN."), Display(Name = "TAN")] public string? Tan { get; set; }
    [Range(1, 5), Display(Name = "Overtime rate (× hourly)")] public decimal OvertimeRateMultiplier { get; set; } = 2;
    [Range(20, 31), Display(Name = "Days for daily rate")] public decimal DailyRateDivisor { get; set; } = 26;
    [Range(1, 31), Display(Name = "Pay day")] public int PayDay { get; set; } = 1;
    [Required, StringLength(100), Display(Name = "Salary expense ledger")] public string SalaryExpenseAccount { get; set; } = "";
    [Required, StringLength(100), Display(Name = "Employer contributions ledger")] public string EmployerContributionAccount { get; set; } = "";
    [Required, StringLength(100), Display(Name = "Salary payable ledger")] public string SalaryPayableAccount { get; set; } = "";
    [Required, StringLength(100), Display(Name = "PF payable ledger")] public string PfPayableAccount { get; set; } = "";
    [Required, StringLength(100), Display(Name = "ESI payable ledger")] public string EsiPayableAccount { get; set; } = "";
    [Required, StringLength(100), Display(Name = "PT payable ledger")] public string PtPayableAccount { get; set; } = "";
    [Required, StringLength(100), Display(Name = "LWF payable ledger")] public string LwfPayableAccount { get; set; } = "";
    [Required, StringLength(100), Display(Name = "TDS payable ledger")] public string TdsPayableAccount { get; set; } = "";
    [Required, StringLength(100), Display(Name = "Loan recovery ledger")] public string LoanRecoveryAccount { get; set; } = "";
}

public sealed class ComponentInput
{
    [Required, StringLength(100)] public string Name { get; set; } = string.Empty;
    [Required, StringLength(20), RegularExpression("^[A-Za-z0-9_]+$", ErrorMessage = "Letters, digits and _ only.")] public string Code { get; set; } = string.Empty;
    public ComponentType Type { get; set; } = ComponentType.Earning;
    [Display(Name = "Taxable")] public bool IsTaxable { get; set; } = true;
    [Display(Name = "Part of PF wages")] public bool IsPfWage { get; set; }
    [Display(Name = "Part of ESI wages")] public bool IsEsiWage { get; set; } = true;
    [Display(Name = "Prorate by paid days")] public bool IsProrated { get; set; } = true;
    [Display(Name = "This is basic pay")] public bool IsBasic { get; set; }
    [Display(Name = "This is HRA")] public bool IsHra { get; set; }
    [Display(Name = "Active")] public bool IsActive { get; set; } = true;
    [Range(0, 1000), Display(Name = "Sort order")] public int SortOrder { get; set; }
}

public sealed class StructureLineInput
{
    public int ComponentId { get; set; }
    public bool Included { get; set; }
    public ComponentCalculation Calculation { get; set; }
    [Range(0, 10_000_000)] public decimal Value { get; set; }
}

public sealed class StructureInput
{
    [Required, StringLength(100)] public string Name { get; set; } = string.Empty;
    [StringLength(500)] public string? Description { get; set; }
    [Display(Name = "Active")] public bool IsActive { get; set; } = true;
    public List<StructureLineInput> Lines { get; set; } = [];
}

public sealed class SalaryAssignInput
{
    [Required, Display(Name = "Salary structure")] public int SalaryStructureId { get; set; }
    [Range(1, 1_000_000_000), Display(Name = "Annual gross (₹)")] public decimal AnnualGross { get; set; }
    [Required, Display(Name = "Effective from")] public DateOnly EffectiveFrom { get; set; }
    [Display(Name = "PF applicable")] public bool PfApplicable { get; set; } = true;
    [StringLength(300)] public string? Remarks { get; set; }
    /// <summary>Allow a revision back-dated into finalized months; the difference is paid (or recovered) in the next payroll.</summary>
    [Display(Name = "Back-dated: pay arrears in the next payroll")] public bool PayArrears { get; set; }
}

public sealed record SalaryRosterRow(int EmployeeId, string Name, string Code, string? Department, string? Structure, decimal? AnnualGross, DateOnly? EffectiveFrom, bool PfApplicable);
public sealed record SalaryBreakup(decimal AnnualGross, decimal MonthlyGross, IReadOnlyList<(string Name, ComponentType Type, decimal Monthly)> Components,
    decimal EmployerPf, decimal EmployerEsi, decimal MonthlyCtc, string? Error);

public interface IPayrollSetupService
{
    Task<PayrollSettingsInput> GetSettingsAsync(CancellationToken ct = default);
    Task<Result> SaveSettingsAsync(PayrollSettingsInput input, CancellationToken ct = default);
    Task<IReadOnlyList<SalaryComponent>> ComponentsAsync(CancellationToken ct = default);
    Task<ComponentInput?> GetComponentAsync(int id, CancellationToken ct = default);
    Task<Result> SaveComponentAsync(int? id, ComponentInput input, CancellationToken ct = default);
    Task<IReadOnlyList<SalaryStructure>> StructuresAsync(CancellationToken ct = default);
    Task<StructureInput> GetStructureAsync(int? id, CancellationToken ct = default);
    Task<Result> SaveStructureAsync(int? id, StructureInput input, CancellationToken ct = default);
    Task<PagedResult<SalaryRosterRow>> RosterAsync(string? search, bool missingOnly, int page, CancellationToken ct = default);
    Task<IReadOnlyList<EmployeeSalary>> HistoryAsync(int employeeId, CancellationToken ct = default);
    /// <returns>Arrears created for a back-dated revision: positive = paid, negative = recovered, 0 = none.</returns>
    Task<Result<decimal>> AssignAsync(int employeeId, SalaryAssignInput input, CancellationToken ct = default);
    Task<SalaryBreakup> PreviewAsync(int structureId, decimal annualGross, bool pfApplicable, CancellationToken ct = default);
    Task<EmployeeSalary?> SalaryAsOfAsync(int employeeId, DateOnly date, CancellationToken ct = default);
    Task<IReadOnlyList<ComponentDef>> DefinitionsAsync(int structureId, CancellationToken ct = default);
}

public sealed class PayrollSetupService(IAppDbContext db, ICurrentContext context, IClock clock, IStatutoryRuleService statutory) : IPayrollSetupService
{
    private async Task<PayrollSetting> SettingsEntityAsync(CancellationToken ct)
    {
        var s = await db.PayrollSettings.FirstOrDefaultAsync(ct);
        if (s is not null) return s;
        s = new PayrollSetting();
        db.PayrollSettings.Add(s);
        await db.SaveChangesAsync(ct);
        return s;
    }

    public async Task<PayrollSettingsInput> GetSettingsAsync(CancellationToken ct = default)
    {
        var s = await SettingsEntityAsync(ct);
        return new PayrollSettingsInput
        {
            PfEnabled = s.PfEnabled, PfRestrictToCeiling = s.PfRestrictToCeiling, EsiEnabled = s.EsiEnabled, PtEnabled = s.PtEnabled, LwfEnabled = s.LwfEnabled,
            TdsEnabled = s.TdsEnabled, DefaultState = s.DefaultState, PfEstablishmentCode = s.PfEstablishmentCode, EsiEmployerCode = s.EsiEmployerCode, Tan = s.Tan,
            OvertimeRateMultiplier = s.OvertimeRateMultiplier, DailyRateDivisor = s.DailyRateDivisor, PayDay = s.PayDay,
            SalaryExpenseAccount = s.SalaryExpenseAccount, EmployerContributionAccount = s.EmployerContributionAccount, SalaryPayableAccount = s.SalaryPayableAccount,
            PfPayableAccount = s.PfPayableAccount, EsiPayableAccount = s.EsiPayableAccount, PtPayableAccount = s.PtPayableAccount, LwfPayableAccount = s.LwfPayableAccount,
            TdsPayableAccount = s.TdsPayableAccount, LoanRecoveryAccount = s.LoanRecoveryAccount
        };
    }

    public async Task<Result> SaveSettingsAsync(PayrollSettingsInput i, CancellationToken ct = default)
    {
        var s = await SettingsEntityAsync(ct);
        s.PfEnabled = i.PfEnabled; s.PfRestrictToCeiling = i.PfRestrictToCeiling; s.EsiEnabled = i.EsiEnabled; s.PtEnabled = i.PtEnabled;
        s.LwfEnabled = i.LwfEnabled; s.TdsEnabled = i.TdsEnabled; s.DefaultState = i.DefaultState?.Trim(); s.PfEstablishmentCode = i.PfEstablishmentCode?.Trim();
        s.EsiEmployerCode = i.EsiEmployerCode?.Trim(); s.Tan = i.Tan?.Trim().ToUpperInvariant(); s.OvertimeRateMultiplier = i.OvertimeRateMultiplier;
        s.DailyRateDivisor = i.DailyRateDivisor; s.PayDay = i.PayDay; s.SalaryExpenseAccount = i.SalaryExpenseAccount.Trim();
        s.EmployerContributionAccount = i.EmployerContributionAccount.Trim(); s.SalaryPayableAccount = i.SalaryPayableAccount.Trim();
        s.PfPayableAccount = i.PfPayableAccount.Trim(); s.EsiPayableAccount = i.EsiPayableAccount.Trim(); s.PtPayableAccount = i.PtPayableAccount.Trim();
        s.LwfPayableAccount = i.LwfPayableAccount.Trim(); s.TdsPayableAccount = i.TdsPayableAccount.Trim(); s.LoanRecoveryAccount = i.LoanRecoveryAccount.Trim();
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<IReadOnlyList<SalaryComponent>> ComponentsAsync(CancellationToken ct = default) =>
        await db.SalaryComponents.AsNoTracking().OrderBy(c => c.Type).ThenBy(c => c.SortOrder).ThenBy(c => c.Name).ToListAsync(ct);

    public async Task<ComponentInput?> GetComponentAsync(int id, CancellationToken ct = default) =>
        await db.SalaryComponents.AsNoTracking().Where(c => c.Id == id).Select(c => new ComponentInput
        {
            Name = c.Name, Code = c.Code, Type = c.Type, IsTaxable = c.IsTaxable, IsPfWage = c.IsPfWage, IsEsiWage = c.IsEsiWage,
            IsProrated = c.IsProrated, IsBasic = c.IsBasic, IsHra = c.IsHra, IsActive = c.IsActive, SortOrder = c.SortOrder
        }).FirstOrDefaultAsync(ct);

    private static readonly HashSet<string> ReservedCodes = new(StringComparer.OrdinalIgnoreCase)
        { "PF", "PF-ER", "EPS", "EDLI", "PF-ADM", "ESI", "ESI-ER", "PT", "LWF", "LWF-ER", "TDS", "OT", "LEAVEENC" };

    public async Task<Result> SaveComponentAsync(int? id, ComponentInput i, CancellationToken ct = default)
    {
        var code = i.Code.Trim().ToUpperInvariant();
        if (ReservedCodes.Contains(code) || code.StartsWith("LOAN")) return Result.Fail($"{code} is reserved for statutory/system lines.");
        if (await db.SalaryComponents.AnyAsync(c => c.Code == code && c.Id != id, ct)) return Result.Fail($"Component code {code} is already used.");
        if (i.Type == ComponentType.Deduction && (i.IsBasic || i.IsHra || i.IsPfWage)) return Result.Fail("Deductions cannot be basic, HRA or PF wages.");
        if (i.IsBasic && await db.SalaryComponents.AnyAsync(c => c.IsBasic && c.Id != id, ct)) return Result.Fail("Another component is already marked as basic pay.");
        if (i.IsHra && await db.SalaryComponents.AnyAsync(c => c.IsHra && c.Id != id, ct)) return Result.Fail("Another component is already marked as HRA.");
        SalaryComponent? c;
        if (id is null) c = db.SalaryComponents.Add(new SalaryComponent()).Entity;
        else if ((c = await db.SalaryComponents.FirstOrDefaultAsync(x => x.Id == id, ct)) is null) return Result.Fail("Component not found.");
        c.Name = i.Name.Trim(); c.Code = code; c.Type = i.Type; c.IsTaxable = i.IsTaxable; c.IsPfWage = i.IsPfWage; c.IsEsiWage = i.IsEsiWage;
        c.IsProrated = i.IsProrated; c.IsBasic = i.IsBasic; c.IsHra = i.IsHra; c.IsActive = i.IsActive; c.SortOrder = i.SortOrder;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<IReadOnlyList<SalaryStructure>> StructuresAsync(CancellationToken ct = default) =>
        await db.SalaryStructures.AsNoTracking().Include(s => s.Lines).ThenInclude(l => l.Component).OrderBy(s => s.Name).ToListAsync(ct);

    public async Task<StructureInput> GetStructureAsync(int? id, CancellationToken ct = default)
    {
        var components = await ComponentsAsync(ct);
        var s = id is null ? null : await db.SalaryStructures.AsNoTracking().Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id, ct);
        return new StructureInput
        {
            Name = s?.Name ?? "", Description = s?.Description, IsActive = s?.IsActive ?? true,
            Lines = components.Where(c => c.IsActive || s?.Lines.Any(l => l.SalaryComponentId == c.Id) == true).Select(c =>
            {
                var line = s?.Lines.FirstOrDefault(l => l.SalaryComponentId == c.Id);
                return new StructureLineInput { ComponentId = c.Id, Included = line is not null, Calculation = line?.Calculation ?? ComponentCalculation.FixedAmount, Value = line?.Value ?? 0 };
            }).ToList()
        };
    }

    public async Task<Result> SaveStructureAsync(int? id, StructureInput input, CancellationToken ct = default)
    {
        var included = input.Lines.Where(l => l.Included).ToList();
        var components = await db.SalaryComponents.Where(c => included.Select(l => l.ComponentId).Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);
        if (components.Count != included.Count) return Result.Fail("Some components were not found.");
        if (included.Count(l => l.Calculation == ComponentCalculation.Balancing) > 1) return Result.Fail("Only one component can be the balancing amount.");
        if (included.Any(l => l.Calculation == ComponentCalculation.Balancing && components[l.ComponentId].Type != ComponentType.Earning))
            return Result.Fail("The balancing component must be an earning.");
        if (included.Any(l => l.Calculation == ComponentCalculation.PercentOfBasic) && !included.Any(l => components[l.ComponentId].IsBasic && l.Calculation != ComponentCalculation.PercentOfBasic))
            return Result.Fail("Percent-of-basic components need a basic component calculated another way.");
        if (included.Any(l => l.Calculation is ComponentCalculation.PercentOfGross or ComponentCalculation.PercentOfBasic && l.Value > 100))
            return Result.Fail("Percentages cannot exceed 100.");

        SalaryStructure? s;
        if (id is null) s = db.SalaryStructures.Add(new SalaryStructure()).Entity;
        else
        {
            s = await db.SalaryStructures.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id, ct);
            if (s is null) return Result.Fail("Structure not found.");
            db.SalaryStructureLines.RemoveRange(s.Lines);
        }
        s.Name = input.Name.Trim();
        s.Description = input.Description?.Trim();
        s.IsActive = input.IsActive;
        foreach (var l in included) s.Lines.Add(new SalaryStructureLine { SalaryComponentId = l.ComponentId, Calculation = l.Calculation, Value = l.Value });
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<IReadOnlyList<ComponentDef>> DefinitionsAsync(int structureId, CancellationToken ct = default) =>
        await db.SalaryStructureLines.AsNoTracking().Where(l => l.SalaryStructureId == structureId)
            .Select(l => new ComponentDef(l.Component.Code, l.Component.Name, l.Component.Type, l.Calculation, l.Value, l.Component.IsTaxable,
                l.Component.IsPfWage, l.Component.IsEsiWage, l.Component.IsProrated, l.Component.IsBasic, l.Component.IsHra, l.Component.SortOrder))
            .ToListAsync(ct);

    public async Task<PagedResult<SalaryRosterRow>> RosterAsync(string? search, bool missingOnly, int page, CancellationToken ct = default)
    {
        const int size = 25;
        page = Math.Max(1, page);
        var today = (await db.OrgClockAsync(context.OrgId(), clock, ct)).Today;
        var q = db.Employees.AsNoTracking().Where(e => e.Status != EmployeeStatus.Exited && e.Status != EmployeeStatus.Inactive);
        if (!string.IsNullOrWhiteSpace(search)) q = q.Where(e => e.FirstName.Contains(search) || e.LastName.Contains(search) || e.EmployeeCode.Contains(search));
        var rows = q.Select(e => new
        {
            e.Id, Name = e.FirstName + " " + e.LastName, e.EmployeeCode, Dept = e.Department != null ? e.Department.Name : null,
            Salary = db.EmployeeSalaries.Where(s => s.EmployeeId == e.Id && s.EffectiveFrom <= today).OrderByDescending(s => s.EffectiveFrom)
                .Select(s => new { s.AnnualGross, s.EffectiveFrom, s.PfApplicable, Structure = s.Structure.Name }).FirstOrDefault()
        });
        if (missingOnly) rows = rows.Where(r => r.Salary == null);
        var total = await rows.CountAsync(ct);
        var items = await rows.OrderBy(r => r.Name).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return new PagedResult<SalaryRosterRow>(items.Select(r => new SalaryRosterRow(r.Id, r.Name, r.EmployeeCode, r.Dept, r.Salary?.Structure,
            r.Salary?.AnnualGross, r.Salary?.EffectiveFrom, r.Salary?.PfApplicable ?? true)).ToList(), total, page, size);
    }

    public async Task<IReadOnlyList<EmployeeSalary>> HistoryAsync(int employeeId, CancellationToken ct = default) =>
        await db.EmployeeSalaries.AsNoTracking().Include(s => s.Structure).Where(s => s.EmployeeId == employeeId)
            .OrderByDescending(s => s.EffectiveFrom).ToListAsync(ct);

    public Task<EmployeeSalary?> SalaryAsOfAsync(int employeeId, DateOnly date, CancellationToken ct = default) =>
        db.EmployeeSalaries.AsNoTracking().Where(s => s.EmployeeId == employeeId && s.EffectiveFrom <= date)
            .OrderByDescending(s => s.EffectiveFrom).FirstOrDefaultAsync(ct);

    public async Task<Result<decimal>> AssignAsync(int employeeId, SalaryAssignInput input, CancellationToken ct = default)
    {
        var employee = await db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == employeeId, ct);
        if (employee is null) return Result<decimal>.Fail("Employee not found.");
        if (!employee.IsCurrent) return Result<decimal>.Fail("Salary cannot be set for a former employee.");
        if (!await db.SalaryStructures.AnyAsync(s => s.Id == input.SalaryStructureId && s.IsActive, ct)) return Result<decimal>.Fail("Select an active salary structure.");
        var preview = await PreviewAsync(input.SalaryStructureId, input.AnnualGross, input.PfApplicable, ct);
        if (preview.Error is not null) return Result<decimal>.Fail(preview.Error);

        // Finalized months never change. A back-dated revision instead creates a one-time arrears (or recovery)
        // equal to the difference between the new salary and what was actually paid in each finalized month.
        var lastFinal = await db.PayrollDetails.Where(d => d.EmployeeId == employeeId && (d.Run.Status == PayrollRunStatus.Finalized || d.Run.Status == PayrollRunStatus.Paid))
            .Select(d => (int?)(d.Run.Year * 100 + d.Run.Month)).MaxAsync(ct);
        var effKey = input.EffectiveFrom.Year * 100 + input.EffectiveFrom.Month;
        PayrollAdjustment? arrears = null;
        if (lastFinal is int lf && effKey <= lf)
        {
            if (!input.PayArrears)
                return Result<decimal>.Fail("Payroll is already finalized for that month. Tick “pay arrears” to back-date the revision: the difference is paid in the next payroll.");
            if (await db.PayrollAdjustments.AnyAsync(a => a.EmployeeId == employeeId && a.Source == AdjustmentSource.SalaryRevision && a.SettledRunId == null, ct))
                return Result<decimal>.Fail("Arrears from an earlier back-dated revision have not been paid yet. Finalize that payroll (or delete those arrears) first.");
            if (await db.PayrollAdjustments.AnyAsync(a => a.EmployeeId == employeeId && a.Source == AdjustmentSource.SalaryRevision && a.CoversTo >= input.EffectiveFrom, ct))
                return Result<decimal>.Fail("Arrears were already paid for part of this period. Enter further corrections as a one-time adjustment.");

            var paid = await db.PayrollDetails.AsNoTracking()
                .Where(d => d.EmployeeId == employeeId && (d.Run.Status == PayrollRunStatus.Finalized || d.Run.Status == PayrollRunStatus.Paid)
                            && d.Run.Year * 100 + d.Run.Month >= effKey && !d.OnHold)
                .Select(d => new { d.Run.Year, d.Run.Month, d.MonthlyGross, d.PaidDays, d.DaysInMonth }).ToListAsync(ct);
            var arrearsAmount = ArrearsCalculator.Compute(input.AnnualGross, input.EffectiveFrom,
                paid.Select(p => new PaidMonth(p.Year, p.Month, p.MonthlyGross, p.PaidDays, p.DaysInMonth)).ToList());
            if (arrearsAmount != 0)
            {
                var next = new DateOnly(lf / 100, lf % 100, 1).AddMonths(1);
                var coversTo = new DateOnly(lf / 100, lf % 100, 1).AddMonths(1).AddDays(-1);
                var period = $"{input.EffectiveFrom:MMM yyyy} – {coversTo:MMM yyyy}";
                arrears = new PayrollAdjustment
                {
                    EmployeeId = employeeId, Year = next.Year, Month = next.Month, Source = AdjustmentSource.SalaryRevision,
                    Category = arrearsAmount > 0 ? AdjustmentCategory.Arrears : AdjustmentCategory.Recovery,
                    Name = arrearsAmount > 0 ? $"Salary arrears ({period})" : $"Salary revision recovery ({period})",
                    Amount = Math.Abs(arrearsAmount), IsTaxable = arrearsAmount > 0, CoversFrom = input.EffectiveFrom, CoversTo = coversTo,
                    Notes = "From a back-dated salary revision. PF/ESI on arrears are not added automatically — add them as adjustments if they apply."
                };
                db.PayrollAdjustments.Add(arrears);
            }
        }

        var existing = await db.EmployeeSalaries.FirstOrDefaultAsync(s => s.EmployeeId == employeeId && s.EffectiveFrom == input.EffectiveFrom, ct);
        var salary = existing ?? db.EmployeeSalaries.Add(new EmployeeSalary { EmployeeId = employeeId }).Entity;
        salary.SalaryStructureId = input.SalaryStructureId;
        salary.AnnualGross = input.AnnualGross;
        salary.EffectiveFrom = input.EffectiveFrom;
        salary.PfApplicable = input.PfApplicable;
        salary.Remarks = input.Remarks?.Trim();
        await db.SaveChangesAsync(ct);
        return Result<decimal>.Ok(arrears is null ? 0 : arrears.IsDeduction ? -arrears.Amount : arrears.Amount);
    }

    public async Task<SalaryBreakup> PreviewAsync(int structureId, decimal annualGross, bool pfApplicable, CancellationToken ct = default)
    {
        var defs = await DefinitionsAsync(structureId, ct);
        var monthly = PayrollEngine.R(annualGross / 12m);
        var (amounts, error) = PayrollEngine.Breakup(monthly, defs);
        if (error is not null || defs.Count == 0) return new SalaryBreakup(annualGross, monthly, [], 0, 0, monthly, error ?? "The structure has no components.");
        var settings = await SettingsEntityAsync(ct);
        var rates = await statutory.RatesAsOfAsync(clock.Today, ct);
        decimal erPf = 0, erEsi = 0;
        if (rates is not null)
        {
            var pfWages = amounts.Where(a => a.Component.IsPfWage).Sum(a => a.MonthlyAmount);
            if (settings.PfEnabled && pfApplicable && pfWages > 0)
            {
                var b = settings.PfRestrictToCeiling ? Math.Min(pfWages, rates.PfWageCeiling) : pfWages;
                erPf = PayrollEngine.R(b * rates.PfEmployerRate / 100m) + PayrollEngine.R(Math.Min(pfWages, rates.PfWageCeiling) * rates.EdliRate / 100m) + PayrollEngine.R(b * rates.PfAdminChargesRate / 100m);
            }
            if (settings.EsiEnabled && monthly <= rates.EsiWageLimit)
                erEsi = Math.Ceiling(amounts.Where(a => a.Component.IsEsiWage && a.Component.Type == ComponentType.Earning).Sum(a => a.MonthlyAmount) * rates.EsiEmployerRate / 100m);
        }
        return new SalaryBreakup(annualGross, monthly, amounts.Select(a => (a.Component.Name, a.Component.Type, a.MonthlyAmount)).ToList(), erPf, erEsi, monthly + erPf + erEsi, null);
    }
}
