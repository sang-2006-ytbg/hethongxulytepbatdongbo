using Microsoft.AspNetCore.SignalR;

namespace File.Api.Hubs;

/// <summary>
/// SignalR hub — điểm giao tiếp thời gian thực giữa máy chủ và máy khách.
/// Máy khách gọi <see cref="WatchJob"/> để đăng ký theo dõi một job cụ thể.
/// Mỗi job có một nhóm riêng (theo mẫu "job:{jobId}") để máy chủ có thể
/// đẩy thông báo đúng địa chỉ mà không cần phát tới tất cả kết nối.
/// </summary>
public sealed class JobStatusHub : Hub
{
    // 3.2.1: Ánh xạ hub vào tuyến /hubs/job-status (thực hiện trong Program.cs)

    /// <summary>
    /// Máy khách gọi phương thức này để tham gia nhóm theo dõi một job.
    /// Sau khi tham gia, mọi thay đổi trạng thái của job sẽ được đẩy tới nhóm.
    /// </summary>
    public Task WatchJob(string jobId)
    {
        return Groups.AddToGroupAsync(Context.ConnectionId, JobGroups.ForJob(jobId));
    }
}
