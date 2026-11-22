namespace Shared.Contracts.Dtos;

public sealed record CreateJobRequest(
    Guid FileId,
    string JobType,
    string? TargetFormat = null
);

public sealed record JobDto(
    Guid Id,
    Guid FileId,
    string OriginalFileName,
    string JobType,
    string Status,
    string? ResultFileName,
    long? ResultSizeBytes,
    string? ErrorMessage,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? DownloadUrl
);

