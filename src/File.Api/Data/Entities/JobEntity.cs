namespace File.Api.Data.Entities;

/// <summary>Thực thể job xử lý - ánh xạ sang bảng jobs trong DB (buổi sau sẽ dùng EF Core)</summary>
public sealed class JobEntity
{
    public Guid   Id              { get; set; } = Guid.NewGuid();
    public Guid   FileId          { get; init; }
    public string JobType         { get; init; } = string.Empty;
    public string? TargetFormat   { get; init; }
    public string Status          { get; set; } = string.Empty;
    public string? ResultFileName { get; set; }
    public string? ResultPath     { get; set; }
    public string? ResultContentType { get; set; }
    public long?  ResultSizeBytes { get; set; }
    public string? ErrorMessage   { get; set; }
    public string CorrelationId   { get; init; } = string.Empty;
    public int    RetryCount      { get; set; }
    public DateTimeOffset  CreatedAt   { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StartedAt   { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
