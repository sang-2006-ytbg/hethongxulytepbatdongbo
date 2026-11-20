namespace Shared.Contracts.Messaging;

/// <summary>
/// Thông điệp SignalR — máy chủ đẩy về máy khách khi trạng thái job thay đổi.
/// Tên sự kiện phía máy khách: "JobStatusChanged".
/// </summary>
public record JobStatusMessage(
    Guid            JobId,
    string          Status,
    DateTimeOffset  UpdatedAt);
