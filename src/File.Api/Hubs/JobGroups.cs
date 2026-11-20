namespace File.Api.Hubs;

/// <summary>
/// Quy ước đặt tên nhóm SignalR cho từng job.
/// Dùng chung giữa hub và các endpoint REST để đảm bảo nhất quán.
/// </summary>
public static class JobGroups
{
    /// <summary>Trả về tên nhóm cho một job theo jobId dạng Guid.</summary>
    public static string ForJob(Guid jobId)   => ForJob(jobId.ToString());

    /// <summary>Trả về tên nhóm cho một job theo jobId dạng chuỗi.</summary>
    public static string ForJob(string jobId) => $"job:{jobId}";
}
