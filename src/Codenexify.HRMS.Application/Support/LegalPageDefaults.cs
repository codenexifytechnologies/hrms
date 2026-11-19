using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Domain.Platform;
using Microsoft.EntityFrameworkCore;

namespace Codenexify.HRMS.Application.Support;

/// <summary>
/// Starter drafts for the public policy pages, created once when missing. They describe how this product actually
/// works (roles under the DPDP Act, sub-processors, cookies, billing) but are NOT legal advice: they are marked
/// "not reviewed" in the Control Center until a Super Admin confirms a lawyer has checked them.
/// </summary>
public static class LegalPageDefaults
{
    public const string Privacy = "privacy";
    public const string Terms = "terms";
    public const string Refunds = "refund-policy";
    public const string Contact = "contact";

    public static async Task EnsureAsync(IAppDbContext db, DateOnly today, CancellationToken ct = default)
    {
        var existing = await db.LegalPages.Select(p => p.Slug).ToListAsync(ct);
        var order = 0;
        foreach (var (slug, title, summary, body) in All)
        {
            order += 10;
            if (existing.Contains(slug)) continue;
            db.LegalPages.Add(new LegalPage { Slug = slug, Title = title, Summary = summary, Body = body.Trim(), EffectiveOn = today, SortOrder = order });
        }
        await db.SaveChangesAsync(ct);
    }

    private static readonly (string Slug, string Title, string Summary, string Body)[] All =
    [
        (Privacy, "Privacy Policy", "How {{CompanyName}} collects, uses and protects personal data in {{ProductName}}.", """
{{CompanyName}} ("we", "us") provides {{ProductName}}, an online HR, attendance and payroll service for businesses. This policy explains what personal data we handle, why, and the choices you have. It is written for the Digital Personal Data Protection Act, 2023 and the Information Technology Act, 2000 and its rules.

## Who decides what happens to your data

- **Employee data inside a company's workspace.** The company that subscribes to {{ProductName}} (your employer) decides what employee data to record and why. Under the DPDP Act it is the **Data Fiduciary**; we process that data on its behalf as a **Data Processor**, only to provide the service. Questions or requests about this data should go first to your employer's HR team.
- **Data about our customers and visitors.** For the details of company administrators who sign up, billing contacts, support conversations and visitors to our website, we are the Data Fiduciary.

## What we collect

- **Account details:** name, work email, mobile number, company name and company code, and a hashed password (we never store passwords in readable form).
- **Employee records entered by the employer:** job, department, reporting manager, joining and exit dates, documents, leave, attendance, salary and statutory details (such as PAN, UAN, bank account), tax declarations, assets, onboarding tasks and similar HR information. PAN and bank account numbers are stored encrypted.
- **Attendance location and photos, only if the employer turns them on:** the GPS position at the moment of clocking in or out (no tracking in between), and a selfie with a clock-in, which is deleted automatically after the period the employer chooses.
- **Usage and security logs:** sign-in history, IP address, browser type and an audit trail of changes, kept to protect accounts and investigate problems.
- **Billing details:** invoices, GSTIN and payment status. Card, UPI and bank payments are handled by our payment partner; we do not see or store card numbers.
- **Support conversations** you send us.

## Why we use it

- To provide, secure and support the service the company has subscribed to.
- To calculate attendance, leave and payroll as configured by the employer.
- To send service emails and, if enabled, sign-in codes by SMS.
- To bill subscriptions and meet tax and accounting obligations.
- To prevent fraud and misuse, and to comply with law and lawful requests.

We do not sell personal data and we do not use it for advertising.

## Who we share it with

We use a small number of service providers, only for the purposes above and under contract:

- Hosting and database infrastructure that runs {{ProductName}}.
- Email delivery, for notifications, payslip alerts and password resets.
- Razorpay, to collect subscription payments.
- MSG91, to send sign-in codes by SMS, only if a company enables mobile sign-in.
- Google and Microsoft, only if a company enables sign-in with those accounts; we receive your verified email address from them.

We may disclose data when required by law, court order or a government authority. **Codenexify support staff can see a company's workspace only when that company grants time-limited, read-only access**, and every page they view is recorded in the company's audit log.

## How long we keep it

We keep workspace data while the company's subscription is active. After a subscription ends, the company can ask for an export; we then delete or anonymise the workspace, except records we must keep for longer under tax, company or other applicable law (for example, invoices). Security logs are kept only as long as needed to protect the service.

## How we protect it

Data is sent over encrypted HTTPS connections. Passwords are hashed, sensitive identifiers are encrypted, each company's data is kept separate from every other company's, staff accounts can use two-step sign-in, and access is limited by role and recorded in audit logs. No system is perfectly secure; if a personal data breach affects you, we will inform the affected company and the authorities as the law requires.

## Your rights

You can ask to access, correct, complete or erase your personal data, withdraw consent where processing is based on consent, and nominate someone to exercise your rights. For employee data, send the request to your employer, who controls it; we will help them respond. For other data, write to us at the address below. We will not discriminate against you for using these rights.

## Cookies

We use only cookies needed for the service to work: to keep you signed in, to protect forms against forgery and to show one-time messages. Your light or dark theme choice is saved in your browser. We do not use advertising or tracking cookies.

## Children

{{ProductName}} is a workplace service and is not meant for children. We do not knowingly collect data about anyone under 18 except as part of an employer's lawful records.

## Grievance Officer

If you have a concern about how your data is handled, contact our Grievance Officer: **{{GrievanceOfficer}}**, [{{GrievanceEmail}}](mailto:{{GrievanceEmail}}), {{CompanyName}}, {{Address}}. We will acknowledge your complaint and try to resolve it within the time required by law. If you are not satisfied, you may approach the Data Protection Board of India.

## Changes to this policy

We will post any changes here with a new effective date, and tell customers by email or in the app before material changes take effect.
"""),
        (Terms, "Terms of Service", "The agreement between {{CompanyName}} and companies that use {{ProductName}}.", """
These terms are an agreement between {{CompanyName}} ("we", "us") and the business that creates a {{ProductName}} workspace ("you", "the customer"). The person who registers confirms they are authorised to accept these terms for that business.

## 1. The service

{{ProductName}} is an online service for HR records, attendance, leave, payroll and related tasks. The modules available to you depend on your plan, as shown on our website and in your workspace. We may improve or change features; we will not remove a core feature of your paid plan during a paid period without notice.

## 2. Accounts and security

You are responsible for the people you give access to, the roles you assign them, and keeping sign-in details confidential. Tell us immediately at [{{SupportEmail}}](mailto:{{SupportEmail}}) if you suspect unauthorised access.

## 3. Free trial

New workspaces may start with a free trial for the period shown at sign-up. No payment is taken during the trial. At the end of the trial the workspace stops working until a plan is purchased.

## 4. Fees, invoices and taxes

- Subscription fees are as shown for your plan and billing cycle, or as agreed in writing. Per-employee plans are charged for the number of employees in the workspace.
- Fees are billed in advance and GST is added as applicable. We issue GST tax invoices from our GSTIN {{Gstin}}.
- Invoices are payable by the due date shown on them, online through our payment partner or by bank transfer.

## 5. Renewal, late payment and suspension

Subscriptions renew for the same cycle unless cancelled. We send the renewal invoice before the current period ends. If payment is late, access continues for a short grace period, after which the workspace may be suspended until payment is received. Your data is not deleted because of a suspension.

## 6. Your data

- You own the data you put into {{ProductName}}. You are responsible for having a lawful basis to collect and use your employees' data, giving them any required notices, and the accuracy of what you enter.
- We process that data only to provide and support the service, as described in our [Privacy Policy](/privacy), and act on your instructions as your data processor.
- Our support staff can open your workspace only when you grant time-limited, read-only access from your settings; you can revoke it at any time.

## 7. Payroll, tax and statutory figures

{{ProductName}} calculates salaries, PF, ESI, professional tax, labour welfare fund and income tax from the rules and rates configured in the system and the data you enter. Rates and thresholds change by law; you must check the results and the statutory settings before paying salaries or filing returns. Statutory filings and payments remain your responsibility.

## 8. Acceptable use

You will not use the service to break the law, upload malicious code, try to access other customers' data, overload or probe the system, or resell it without our written permission.

## 9. Availability and maintenance

We aim to keep the service available at all times but do not guarantee uninterrupted operation. We may carry out planned maintenance, announced in advance where possible.

## 10. Intellectual property

The software, design and content of {{ProductName}} belong to {{CompanyName}}. You receive a non-exclusive, non-transferable right to use it for your business during your subscription.

## 11. Confidentiality

Each party will keep the other's non-public information confidential and use it only for this agreement.

## 12. Limitation of liability

To the extent the law allows, neither party is liable for indirect or consequential losses, or loss of profit or data arising from the other party's actions. Our total liability under these terms is limited to the fees you paid us in the twelve months before the claim.

## 13. Ending the agreement

You can cancel at any time; see our [Refund & Cancellation Policy](/refund-policy). We may end or suspend the service for serious breach of these terms or non-payment. After the subscription ends you may request an export of your data before it is deleted as described in the Privacy Policy.

## 14. Changes to these terms

We may update these terms. We will post the new version here with its effective date and tell customers before material changes take effect. Continuing to use the service after that date means you accept the new terms.

## 15. Governing law

These terms are governed by the laws of India. The courts at **{{Jurisdiction}}** have exclusive jurisdiction.

## 16. Contact

{{CompanyName}}, {{Address}}. Email: [{{SupportEmail}}](mailto:{{SupportEmail}}).
"""),
        (Refunds, "Refund & Cancellation Policy", "How cancellations, refunds and service delivery work for {{ProductName}} subscriptions.", """
## Free trial

You can try {{ProductName}} free for the trial period shown at sign-up. No payment details are needed and nothing is charged.

## Cancelling a subscription

You can cancel at any time by writing to [{{BillingEmail}}](mailto:{{BillingEmail}}) from your registered email, or through your workspace where available. Cancellation takes effect at the end of the period you have already paid for; you keep full access until then and are not charged again.

## Refunds

Subscription fees are paid in advance for a monthly or annual period and are **not refundable for a period that has already started**, except in these cases:

- you were charged twice for the same invoice;
- you were charged after cancelling, because of an error on our side;
- the service was unavailable for a long period because of a fault on our side.

To request a refund, email [{{BillingEmail}}](mailto:{{BillingEmail}}) within 30 days of the payment with your company code and invoice number. Approved refunds are issued with a GST credit note and paid back to the original payment method, normally within 5–7 working days of approval; your bank may take longer to show it.

## Delivery of the service

{{ProductName}} is an online service. Your workspace is available immediately after registration, and paid plans are activated as soon as payment is confirmed. Nothing is shipped physically.

## Contact

{{CompanyName}}, {{Address}}. Phone: {{Phone}}. Email: [{{BillingEmail}}](mailto:{{BillingEmail}}).
"""),
        (Contact, "Contact Us", "How to reach {{CompanyName}}.", """
## {{CompanyName}}

{{Address}}

- **Support:** [{{SupportEmail}}](mailto:{{SupportEmail}})
- **Billing:** [{{BillingEmail}}](mailto:{{BillingEmail}})
- **Phone:** {{Phone}}
- **GSTIN:** {{Gstin}}
- **Website:** [{{Website}}]({{Website}})

## Already a customer?

Sign in to your workspace and open **Support** to raise a ticket; you can follow the conversation there and we reply by email too.

## Privacy questions

Write to our Grievance Officer, {{GrievanceOfficer}}, at [{{GrievanceEmail}}](mailto:{{GrievanceEmail}}). See our [Privacy Policy](/privacy).
"""),
    ];
}
