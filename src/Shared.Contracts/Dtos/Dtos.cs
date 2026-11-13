namespace Shared.Contracts.Dtos;

// ─── Lỗi API chuẩn ──────────────────────────────────────────────────────────
public sealed record ApiError(string Code, string Message, string? CorrelationId = null);

// ─── Tệp (File) ─────────────────────────────────────────────────────────────
public sealed record FileDto(
    Guid   Id,
    string OriginalFileName,
    string ContentType,
    long   SizeBytes,
    DateTimeOffset UploadedAt,
    Guid?  LatestJobId,
    string? LatestJobStatus,
    bool   HasResult
);

// ─── Job ─────────────────────────────────────────────────────────────────────
public sealed record CreateJobRequest(
    Guid   FileId,
    string JobType,
    string? TargetFormat = null
);

public sealed record JobDto(
    Guid   Id,
    Guid   FileId,
    string OriginalFileName,
    string JobType,
    string Status,
    string? ResultFileName,
    long?  ResultSizeBytes,
    string? ErrorMessage,
    DateTimeOffset  CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? DownloadUrl
);
