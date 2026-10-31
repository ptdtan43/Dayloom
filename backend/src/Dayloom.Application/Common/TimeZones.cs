namespace Dayloom.Application.Common;

/// <summary>Every user has one official IANA time zone; all date boundaries use it (docs/DESIGN.md §3).</summary>
public static class TimeZones
{
    public const string Default = "Asia/Ho_Chi_Minh";
    public const int MaxIdLength = 64;

    /// <summary>
    /// Returns <paramref name="requested"/> if it is a valid IANA id, otherwise <see cref="Default"/>.
    /// Windows ids ("SE Asia Standard Time") are rejected even on Windows, so stored ids are portable.
    /// </summary>
    public static string ResolveOrDefault(string? requested)
    {
        var id = requested?.Trim();
        var isIana = !string.IsNullOrEmpty(id)
            && id.Length <= MaxIdLength
            && TimeZoneInfo.TryConvertIanaIdToWindowsId(id, out _)
            && TimeZoneInfo.TryFindSystemTimeZoneById(id, out _);

        return isIana ? id! : Default;
    }
}
