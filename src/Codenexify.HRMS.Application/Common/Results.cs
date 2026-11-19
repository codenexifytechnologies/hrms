namespace Codenexify.HRMS.Application.Common;

public class Result
{
    public bool Succeeded { get; protected init; }
    public string? Error { get; protected init; }

    public static Result Ok() => new() { Succeeded = true };
    public static Result Fail(string error) => new() { Succeeded = false, Error = error };
}

public sealed class Result<T> : Result
{
    public T? Value { get; private init; }

    public static Result<T> Ok(T value) => new() { Succeeded = true, Value = value };
    public new static Result<T> Fail(string error) => new() { Succeeded = false, Error = error };
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}

/// <summary>Id + label (+ optional hint) for pickers.</summary>
public sealed record LookupOption(int Id, string Name, string? Hint = null);

/// <summary>Raised when code attempts to touch another tenant's data. Never caught silently.</summary>
public sealed class TenantIsolationException(string message) : Exception(message);
