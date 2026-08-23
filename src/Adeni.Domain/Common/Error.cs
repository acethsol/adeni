namespace Adeni.Domain.Common;

public sealed record Error(
    string Code,
    string? Message = null,
    IReadOnlyDictionary<string, object?>? Params = null)
{
    public static Error NotFound(string resource) =>
        new(
            "not_found",
            $"{resource} was not found.",
            new Dictionary<string, object?> { ["resource"] = resource });

    public static Error Forbidden(string detail) =>
        new("forbidden", detail);

    public static Error Validation(string detail) =>
        new("validation", detail);

    public static Error Conflict(string detail) =>
        new("conflict", detail);
}
