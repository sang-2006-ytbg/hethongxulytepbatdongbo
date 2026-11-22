namespace Shared.Contracts.Dtos;

public sealed record ApiError(string Code, string Message, string? CorrelationId = null);

