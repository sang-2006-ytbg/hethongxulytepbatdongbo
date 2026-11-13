using File.Api.Data;
using File.Api.Data.Entities;
using Shared.Contracts.Constants;
using Shared.Contracts.Dtos;

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
        CreateJobRequest request,
        IAppStore        store,
        ILogger<Program> logger)
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

        // Buổi 2: Chưa có RabbitMQ → chỉ lưu DB, trạng thái giữ Pending
        // Buổi 6+ sẽ gửi JobQueuedMessage vào file_processing_queue tại đây

        logger.LogInformation(
            "[Job] Đã tạo: {JobId} | File: {FileId} | Loại: {Type} | CorrelationId: {CorrId}",
            job.Id, job.FileId, job.JobType, job.CorrelationId);

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
