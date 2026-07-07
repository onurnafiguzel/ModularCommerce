using System.Text.RegularExpressions;
using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Identity.Domain.Users;

/// <summary>
/// Normalize edilmiş e-posta value object'i: trim + küçük harf.
/// Unique kontrolün DB'de tek biçimle çalışması için normalize burada,
/// tek yerde yapılır (FR-1.5).
/// </summary>
public sealed partial record Email
{
    /// <summary>RFC 5321 pratik üst sınırı.</summary>
    public const int MaxLength = 254;

    public string Value { get; }

    private Email(string value) => Value = value;

    public static Result<Email> Create(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant();

        if (string.IsNullOrEmpty(normalized)
            || normalized.Length > MaxLength
            || !FormatRegex().IsMatch(normalized))
        {
            return Result.Failure<Email>(IdentityErrors.InvalidEmail);
        }

        return Result.Success(new Email(normalized));
    }

    public static Email Rehydrate(string value) => new(value);

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex FormatRegex();
}
