namespace Shared.Contracts.Dtos;

public sealed record FileDto(
    Guid Id,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    DateTimeOffset UploadedAt,
    Guid? LatestJobId,
    string? LatestJobStatus,
    bool HasResult
);
