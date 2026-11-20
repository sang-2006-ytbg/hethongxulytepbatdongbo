using File.Api.Data;
using File.Api.Data.Entities;
using File.Api.Hubs;
using Microsoft.AspNetCore.SignalR;
using Shared.Contracts.Constants;
using Shared.Contracts.Dtos;
using Shared.Contracts.Messaging;

namespace File.Api.Endpoints;

public static class JobEndpoints
{
    public static IEndpointRouteBuilder MapJobEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/jobs").WithTags("Jobs");

        // POST /jobs — Tạo job xử lý tệp
        group.MapPost("/", CreateJob)
             .WithSummary("Tạo job xử lý tệp và đưa vào hàng đợi");

        // GET /jobs — Danh sách jobs (hỗ trợ lọc ?status=&fileId=)
        group.MapGet("/", GetAllJobs)
             .WithSummary("Lấy danh sách jobs, hỗ trợ lọc theo status và fileId");

        // GET /jobs/{id} — Trạng thái một job
        group.MapGet("/{id:guid}", GetJobById)
             .WithSummary("Tra cứu chi tiết và trạng thái một job");

        return app;
    }

    // ─── Handlers ────────────────────────────────────────────────────────────

    static IResult CreateJob(
        CreateJobRequest                   request,
        IAppStore                          store,
        IHubContext<JobStatusHub>          hubContext,
        ILogger<Program>                   logger)
    {
        // Kiểm tra tệp tồn tại
        var file = store.GetFile(request.FileId);
        if (file is null)
            return Results.NotFound(new ApiError(
                "FILE_NOT_FOUND", "Không tìm thấy tệp cần xử lý."));

        // Kiểm tra loại job hợp lệ
        if (!JobTypes.IsValid(request.JobType))
            return Results.BadRequest(new ApiError(
                "INVALID_JOB_TYPE",
                $"Loại xử lý không hợp lệ. Chỉ hỗ trợ: {string.Join(", ", JobTypes.All)}."));

        var correlationId = Guid.NewGuid().ToString();

        var job = store.AddJob(new JobEntity
        {
            FileId        = request.FileId,
            JobType       = request.JobType.Trim(),
            TargetFormat  = request.TargetFormat?.Trim(),
            Status        = JobStatuses.Pending,
            CorrelationId = correlationId,
        });

        logger.LogInformation(
            "[Job] Đã tạo: {JobId} | File: {FileId} | Loại: {Type} | CorrelationId: {CorrId}",
            job.Id, job.FileId, job.JobType, job.CorrelationId);

        // 3.3.1: Khởi động tác vụ nền mô phỏng xử lý → đẩy trạng thái qua SignalR
        // Trong hệ thống thật, phần này sẽ do Worker Service xử lý (các buổi sau).
        _ = SimulateProcessingAsync(job.Id, store, hubContext, logger);

        return Results.Created($"/jobs/{job.Id}", ToDto(job, file.OriginalFileName));
    }

    static IResult GetAllJobs(
        string?   status,
        Guid?     fileId,
        IAppStore store)
    {
        var jobs = fileId.HasValue
            ? store.GetJobsByFileId(fileId.Value)
            : store.GetAllJobs();

        if (!string.IsNullOrWhiteSpace(status))
            jobs = jobs.Where(j => string.Equals(j.Status, status, StringComparison.OrdinalIgnoreCase))
                       .ToList();

        var result = jobs
            .OrderByDescending(j => j.CreatedAt)
            .Select(j =>
            {
                var file = store.GetFile(j.FileId);
                return ToDto(j, file?.OriginalFileName ?? string.Empty);
            })
            .ToList();

        return Results.Ok(result);
    }

    static IResult GetJobById(Guid id, IAppStore store)
    {
        var job = store.GetJob(id);
        if (job is null)
            return Results.NotFound(new ApiError(
                "JOB_NOT_FOUND", $"Không tìm thấy job với id = {id}."));

        var file = store.GetFile(job.FileId);
        return Results.Ok(ToDto(job, file?.OriginalFileName ?? string.Empty));
    }

    // ─── Mô phỏng xử lý bất đồng bộ (Buổi 3) ────────────────────────────────
    // Hàm này lần lượt chuyển trạng thái job và đẩy thông báo SignalR tới nhóm.
    // Trong hệ thống thực tế, luồng này sẽ được thay bằng Worker Service + hàng đợi.

    static async Task SimulateProcessingAsync(
        Guid                      jobId,
        IAppStore                 store,
        IHubContext<JobStatusHub> hubContext,
        ILogger                   logger)
    {
        await Task.Delay(TimeSpan.FromSeconds(1));
        await UpdateStatusAsync(jobId, JobStatuses.Processing, store, hubContext, logger);

        await Task.Delay(TimeSpan.FromSeconds(2));
        await UpdateStatusAsync(jobId, JobStatuses.Completed, store, hubContext, logger);
    }

    static async Task UpdateStatusAsync(
        Guid                      jobId,
        string                    status,
        IAppStore                 store,
        IHubContext<JobStatusHub> hubContext,
        ILogger                   logger)
    {
        store.UpdateJobStatus(jobId, status);

        var message = new JobStatusMessage(jobId, status, DateTimeOffset.UtcNow);

        // Gửi thông báo tới đúng nhóm máy khách đang theo dõi job này
        await hubContext.Clients
            .Group(JobGroups.ForJob(jobId))
            .SendAsync("JobStatusChanged", message);

        logger.LogInformation(
            "[SignalR] Đã đẩy trạng thái: {JobId} → {Status}", jobId, status);
    }

    // ─── Mapper ──────────────────────────────────────────────────────────────

    static JobDto ToDto(JobEntity j, string originalFileName) => new(
        Id:               j.Id,
        FileId:           j.FileId,
        OriginalFileName: originalFileName,
        JobType:          j.JobType,
        Status:           j.Status,
        ResultFileName:   j.ResultFileName,
        ResultSizeBytes:  j.ResultSizeBytes,
        ErrorMessage:     j.ErrorMessage,
        CreatedAt:        j.CreatedAt,
        StartedAt:        j.StartedAt,
        CompletedAt:      j.CompletedAt,
        DownloadUrl:      j.Status == JobStatuses.Completed
                              ? $"/files/{j.FileId}/result?jobId={j.Id}"
                              : null
    );
}
