namespace Shared.Contracts.Messaging;

public sealed record JobQueuedMessage(
    Guid   JobId,
    Guid   FileId,
    string JobType,
    string? TargetFormat,
    string CorrelationId,
    DateTimeOffset EnqueuedAt
);
