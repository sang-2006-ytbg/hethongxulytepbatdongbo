namespace Shared.Contracts.Constants;

public static class JobStatuses
{
    public const string Pending    = "Pending";
    public const string Processing = "Processing";
    public const string Completed  = "Completed";
    public const string Failed     = "Failed";
}

public static class JobTypes
{
    public const string Thumbnail     = "Thumbnail";
    public const string ConvertFormat = "ConvertFormat";
    public const string Compress      = "Compress";

    public static readonly string[] All = [Thumbnail, ConvertFormat, Compress];

    public static bool IsValid(string? type) =>
        !string.IsNullOrWhiteSpace(type) &&
        All.Contains(type.Trim(), StringComparer.OrdinalIgnoreCase);
}
