using File.Api.Data;
using File.Api.Data.Entities;
using Microsoft.AspNetCore.Http.HttpResults;
using Shared.Contracts.Constants;
using Shared.Contracts.Dtos;

namespace File.Api.Endpoints;

public static class FileEndpoints
{
    public static IEndpointRouteBuilder MapFileEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/files").WithTags("Files");

        // POST /files — Tải tệp lên (multipart/form-data)
        group.MapPost("/", UploadFileAsync)
             .DisableAntiforgery()
             .WithSummary("Tải tệp lên hệ thống");

        // GET /files — Danh sách tệp
        group.MapGet("/", GetAllFiles)
             .WithSummary("Lấy danh sách tệp đã đăng ký");

        // GET /files/{id} — Chi tiết tệp
        group.MapGet("/{id:guid}", GetFileById)
             .WithSummary("Lấy chi tiết một tệp");

        // GET /files/{id}/result — Tải hoặc xem kết quả
        group.MapGet("/{id:guid}/result", GetFileResult)
             .WithSummary("Tải kết quả tệp đã xử lý");

        return app;
    }

    // ─── Handlers ────────────────────────────────────────────────────────────

    static async Task<IResult> UploadFileAsync(
        IFormFile?  file,
        IAppStore   store,
        IConfiguration config,
        ILogger<Program> logger)
    {
        if (file is null || file.Length == 0)
            return Results.BadRequest(new ApiError("EMPTY_FILE", "Tệp tải lên không được để trống."));

        // Giới hạn 50 MB
        const long MaxBytes = 50L * 1024 * 1024;
        if (file.Length > MaxBytes)
            return Results.BadRequest(new ApiError("FILE_TOO_LARGE", "Tệp vượt quá giới hạn 50 MB."));

        var storagePath = config["Storage:RootPath"] ?? "/app/storage";
        var uploadDir   = Path.Combine(storagePath, "uploads");
        Directory.CreateDirectory(uploadDir);

        var fileId       = Guid.NewGuid();
        var safeFileName = Path.GetFileName(file.FileName);
        var storedName   = $"{fileId}_{safeFileName}";
        var fullPath     = Path.Combine(uploadDir, storedName);

        await using (var stream = System.IO.File.Create(fullPath))
            await file.CopyToAsync(stream);

        var entity = store.AddFile(new FileEntity
        {
            Id               = fileId,
            OriginalFileName = safeFileName,
            StoredFileName   = storedName,
            StoredPath       = fullPath,
            ContentType      = file.ContentType,
            SizeBytes        = file.Length,
        });

        logger.LogInformation("[File] Đã tải lên: {FileId} — {FileName} ({Size} bytes)",
            entity.Id, entity.OriginalFileName, entity.SizeBytes);

        return Results.Created($"/files/{entity.Id}/result", ToDto(entity, store));
    }

    static IResult GetAllFiles(IAppStore store)
    {
        var files = store.GetAllFiles().Select(f => ToDto(f, store)).ToList();
        return Results.Ok(files);
    }

    static IResult GetFileById(Guid id, IAppStore store)
    {
        var entity = store.GetFile(id);
        if (entity is null)
            return Results.NotFound(new ApiError("FILE_NOT_FOUND", $"Không tìm thấy tệp với id = {id}."));

        return Results.Ok(ToDto(entity, store));
    }

    static IResult GetFileResult(Guid id, IAppStore store)
    {
        var file = store.GetFile(id);
        if (file is null)
            return Results.NotFound(new ApiError("FILE_NOT_FOUND", $"Không tìm thấy tệp với id = {id}."));

        var latestJob = store.GetJobsByFileId(id)
            .OrderByDescending(j => j.CreatedAt)
            .FirstOrDefault();

        if (latestJob is null)
            return Results.NotFound(new ApiError(
                "FILE_OR_JOB_NOT_FOUND", "Tệp chưa có yêu cầu xử lý nào."));

        if (latestJob.Status is JobStatuses.Pending or JobStatuses.Processing)
            return Results.BadRequest(new ApiError(
                "JOB_NOT_COMPLETED",
                $"Tệp đang ở trạng thái {latestJob.Status}, chưa có kết quả."));

        if (latestJob.Status == JobStatuses.Failed)
            return Results.BadRequest(new ApiError(
                "JOB_FAILED", latestJob.ErrorMessage ?? "Xử lý tệp thất bại."));

        // Completed — trả về luồng tệp để tải về
        if (!System.IO.File.Exists(latestJob.ResultPath))
            return Results.NotFound(new ApiError(
                "RESULT_FILE_MISSING", "Tệp kết quả không tìm thấy trên máy chủ."));

        var stream = System.IO.File.OpenRead(latestJob.ResultPath!);
        return Results.File(stream,
            latestJob.ResultContentType ?? "application/octet-stream",
            fileDownloadName: latestJob.ResultFileName);
    }

    // ─── Mapper ──────────────────────────────────────────────────────────────

    static FileDto ToDto(FileEntity f, IAppStore store)
    {
        var latestJob = store.GetJobsByFileId(f.Id)
            .OrderByDescending(j => j.CreatedAt)
            .FirstOrDefault();

        return new FileDto(
            Id:               f.Id,
            OriginalFileName: f.OriginalFileName,
            ContentType:      f.ContentType,
            SizeBytes:        f.SizeBytes,
            UploadedAt:       f.UploadedAt,
            LatestJobId:      latestJob?.Id,
            LatestJobStatus:  latestJob?.Status,
            HasResult:        latestJob?.Status == JobStatuses.Completed
        );
    }
}
