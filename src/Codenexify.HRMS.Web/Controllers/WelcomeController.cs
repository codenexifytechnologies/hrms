using Codenexify.HRMS.Application.CoreHr;
using Codenexify.HRMS.Application.Planning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.RateLimiting;

namespace Codenexify.HRMS.Web.Controllers;

/// <summary>
/// A new hire's pre-boarding page, opened from the emailed link without signing in. The token decides the organization and
/// the person; the service re-checks it on every request (unknown, cancelled, expired, feature removed, person left).
/// </summary>
[AllowAnonymous, EnableRateLimiting("preboarding")]
[Route("welcome")]
public sealed class WelcomeController(IPreboardingService preboarding) : Controller
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        // The link may carry personal data: never cache it, index it or send it on to other sites.
        Response.Headers.CacheControl = "no-store";
        Response.Headers["Referrer-Policy"] = "no-referrer";
        Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
        // A signed-in session belongs to someone else's account (often HR testing the link); keep the two apart.
        if (User.Identity?.IsAuthenticated == true) context.Result = View("SignedIn");
        base.OnActionExecuting(context);
    }

    private string Back(string token, string anchor) => $"/welcome?token={Uri.EscapeDataString(token)}#{anchor}";

    [HttpGet("")]
    public async Task<IActionResult> Index(string? token, CancellationToken ct)
    {
        var portal = await preboarding.PortalAsync(token ?? string.Empty, ct);
        if (portal is null) return View("Invalid");
        ViewBag.Token = token;
        return View(portal);
    }

    private async Task<IActionResult> ShowWithErrorsAsync(string token, Action<PreboardingPortal> adjust, CancellationToken ct)
    {
        var portal = await preboarding.PortalAsync(token, ct);
        if (portal is null) return View("Invalid");
        adjust(portal);
        ViewBag.Token = token;
        return View("Index", portal);
    }

    [HttpPost("personal")]
    public async Task<IActionResult> Personal(string token, [Bind(Prefix = "Personal")] PreboardingPersonalInput personal, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var r = await preboarding.SavePersonalAsync(token, personal, ct);
            if (r.Succeeded) { TempData["Success"] = "Personal details saved. Thank you!"; return Redirect(Back(token, "bank")); }
            ModelState.AddModelError(string.Empty, r.Error!);
        }
        ViewBag.Section = "personal";
        ViewBag.PostedPersonal = personal;
        return await ShowWithErrorsAsync(token, _ => { }, ct);
    }

    [HttpPost("bank")]
    public async Task<IActionResult> Bank(string token, [Bind(Prefix = "Bank")] BankInput bank, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var r = await preboarding.SaveBankAsync(token, bank, ct);
            if (r.Succeeded) { TempData["Success"] = "Bank details saved."; return Redirect(Back(token, "documents")); }
            ModelState.AddModelError(string.Empty, r.Error!);
        }
        ViewBag.Section = "bank";
        ViewBag.PostedBank = new BankInput { BankName = bank.BankName, AccountHolderName = bank.AccountHolderName, Ifsc = bank.Ifsc, BranchName = bank.BranchName };
        return await ShowWithErrorsAsync(token, _ => { }, ct);
    }

    [HttpPost("upload"), RequestSizeLimit(DocumentService.MaxBytes + 512 * 1024), RequestFormLimits(MultipartBodyLengthLimit = DocumentService.MaxBytes + 512 * 1024)]
    public async Task<IActionResult> Upload(string token, int itemId, IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) TempData["Error"] = "Choose a file to upload.";
        else
        {
            await using var stream = file.OpenReadStream();
            var r = await preboarding.UploadAsync(token, itemId, new UploadedFile(stream, file.FileName, file.Length), ct);
            if (r.Succeeded) TempData["Success"] = "Uploaded.";
            else TempData["Error"] = r.Error;
        }
        return Redirect(Back(token, $"item-{itemId}"));
    }

    [HttpPost("acknowledge")]
    public async Task<IActionResult> Acknowledge(string token, int itemId, CancellationToken ct)
    {
        var r = await preboarding.AcknowledgeAsync(token, itemId, ct);
        if (r.Succeeded) TempData["Success"] = "Thank you.";
        else TempData["Error"] = r.Error;
        return Redirect(Back(token, $"item-{itemId}"));
    }

    [HttpPost("submit")]
    public async Task<IActionResult> Submit(string token, CancellationToken ct)
    {
        var r = await preboarding.SubmitAsync(token, ct);
        if (r.Succeeded) TempData["Success"] = "All done! The HR team has been told. See you on your first day.";
        else TempData["Error"] = r.Error;
        return Redirect(Back(token, "top"));
    }
}
