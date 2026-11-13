namespace File.Api.Data.Entities;

/// <summary>Thực thể tệp - ánh xạ sang bảng files trong DB (buổi sau sẽ dùng EF Core)</summary>
public sealed class FileEntity
{
    public Guid   Id               { get; init; } = Guid.NewGuid();
    public string OriginalFileName { get; init; } = string.Empty;
    public string StoredFileName   { get; init; } = string.Empty;
    public string StoredPath       { get; init; } = string.Empty;
    public string ContentType      { get; init; } = string.Empty;
    public long   SizeBytes        { get; init; }
    public DateTimeOffset UploadedAt { get; init; } = DateTimeOffset.UtcNow;
}
