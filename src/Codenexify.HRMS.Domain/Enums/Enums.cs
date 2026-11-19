namespace Codenexify.HRMS.Domain.Enums;

public enum OrganizationStatus
{
    PendingVerification,
    Trial,
    Active,
    Suspended,
    Inactive,
    Expired
}

public enum SubscriptionStatus
{
    Trial,
    Active,
    PastDue,
    Paused,
    Cancelled,
    Expired
}

public enum BillingCycle
{
    Monthly,
    Yearly
}

public enum FeatureTier
{
    Foundation,
    Strength,
    Growth,
    AddOn
}

public enum PlatformRole
{
    SuperAdmin,
    SupportAdmin,
    FinanceAdmin,
    SalesAdmin
}

public enum PrincipalType
{
    Platform,
    Tenant
}
