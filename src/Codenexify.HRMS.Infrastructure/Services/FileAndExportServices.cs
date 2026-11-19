using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Codenexify.HRMS.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Codenexify.HRMS.Infrastructure.Services;

/// <summary>
/// Stores tenant files on local disk outside the web root (served only through authorized controllers).
/// Keys look like "{orgId}/{yyyy}/{MM}/{guid}.ext"; they are generated here and validated on every read.
/// </summary>
public sealed partial class LocalFileStorage : IFileStorage
{
    private readonly string _root;

    /// <summary>Absolute folder holding every uploaded file (used by the Control Center data reset).</summary>
    public string Root => _root;

    public LocalFileStorage(IConfiguration config, IHostEnvironment env)
        : this(config["Storage:LocalRoot"] ?? Path.Combine(env.ContentRootPath, "App_Data", "uploads")) { }

    public LocalFileStorage(string root)
    {
        _root = Path.GetFullPath(root);
        Directory.CreateDirectory(_root);
    }

    public async Task<string> SaveAsync(int organizationId, Stream content, string extension, CancellationToken ct = default)
    {
        if (!ExtensionRegex().IsMatch(extension)) throw new ArgumentException("Invalid file extension.", nameof(extension));
        var now = DateTime.UtcNow;
        var key = $"{organizationId}/{now:yyyy}/{now:MM}/{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var path = PathFor(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(file, ct);
        return key;
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken ct = default)
    {
        var path = PathFor(key);
        return Task.FromResult<Stream?>(File.Exists(path) ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read) : null);
    }

    public Task DeleteAsync(string key, CancellationToken ct = default)
    {
        var path = PathFor(key);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    private string PathFor(string key)
    {
        if (!KeyRegex().IsMatch(key)) throw new ArgumentException("Invalid storage key.", nameof(key));
        var full = Path.GetFullPath(Path.Combine(_root, key.Replace('/', Path.DirectorySeparatorChar)));
        if (!full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Storage key escapes the storage root.", nameof(key));
        return full;
    }

    [GeneratedRegex(@"^\d+/\d{4}/\d{2}/[0-9a-f]{32}\.[a-z0-9]{1,5}$")]
    private static partial Regex KeyRegex();

    [GeneratedRegex(@"^\.[A-Za-z0-9]{1,5}$")]
    private static partial Regex ExtensionRegex();
}

public sealed class ClosedXmlExporter : IExcelExporter
{
    public byte[] Export(string sheetName, IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<object?>> rows)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add(sheetName);
        for (var c = 0; c < headers.Count; c++) ws.Cell(1, c + 1).Value = headers[c];
        var header = ws.Range(1, 1, 1, headers.Count);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#E2E8F0");

        var r = 2;
        foreach (var row in rows)
        {
            for (var c = 0; c < row.Count; c++)
            {
                var cell = ws.Cell(r, c + 1);
                switch (row[c])
                {
                    case null: break;
                    case DateTime d: cell.Value = d; cell.Style.DateFormat.Format = "dd-mmm-yyyy"; break;
                    case int i: cell.Value = i; break;
                    case decimal m: cell.Value = m; break;
                    // Text is written as text: prevents spreadsheet formula injection ("=HYPERLINK(...)").
                    default: cell.SetValue(Sanitize(row[c]!.ToString()!)); break;
                }
            }
            r++;
        }
        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents(1, Math.Min(r, 500));
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static string Sanitize(string s) => s.Length > 0 && "=+-@\t\r".Contains(s[0]) ? "'" + s : s;
}
