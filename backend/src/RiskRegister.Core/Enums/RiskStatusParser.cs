namespace RiskRegister.Core.Enums;

/// <summary>
/// Parses the <c>?status=</c> query-string value into a <see cref="RiskStatus"/> filter.
/// </summary>
/// <remarks>
/// Lives in Core, away from MVC, so the rule is a pure function that can be tested without a host.
/// </remarks>
public static class RiskStatusParser
{
    /// <summary>The accepted values, in a form suitable for an error message.</summary>
    public static string ValidValues { get; } = string.Join(", ", Enum.GetNames<RiskStatus>());

    /// <summary>
    /// Parses <paramref name="raw"/> case-insensitively.
    /// </summary>
    /// <param name="raw">The raw query-string value. Null, empty or whitespace means "no filter".</param>
    /// <param name="status">The parsed status, or null when no filter was requested.</param>
    /// <returns>False only when <paramref name="raw"/> was present but not a valid status name.</returns>
    public static bool TryParse(string? raw, out RiskStatus? status)
    {
        status = null;

        // An absent or blank filter is not an error — it means the whole register.
        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        var trimmed = raw.Trim();

        // Enum.TryParse happily accepts numbers: "2" parses to Accepted, and "99" parses to an
        // undefined RiskStatus(99) that would reach the database. The query string names a status;
        // it does not carry the enum's storage value. Reject anything that is not a name.
        if (!trimmed.All(char.IsLetter))
        {
            return false;
        }

        // ...and even a letters-only string could name a value outside the enum in theory, so the
        // IsDefined check stays as the final guard.
        if (!Enum.TryParse<RiskStatus>(trimmed, ignoreCase: true, out var parsed)
            || !Enum.IsDefined(parsed))
        {
            return false;
        }

        status = parsed;
        return true;
    }
}
