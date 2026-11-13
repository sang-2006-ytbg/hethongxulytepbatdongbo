using File.Api.Data.Entities;

namespace File.Api.Data;

/// <summary>
/// Lưu trữ dữ liệu in-memory — dùng cho giai đoạn Buổi 2 (chưa có Database).
/// Interface này sẽ được thay bằng EF Core + PostgreSQL ở các buổi sau.
/// </summary>
public interface IAppStore
{
    // Files
    FileEntity AddFile(FileEntity file);
    FileEntity? GetFile(Guid id);
    IReadOnlyList<FileEntity> GetAllFiles();

    // Jobs
    JobEntity AddJob(JobEntity job);
    JobEntity? GetJob(Guid id);
    IReadOnlyList<JobEntity> GetAllJobs();
    IReadOnlyList<JobEntity> GetJobsByFileId(Guid fileId);
}

public sealed class InMemoryAppStore : IAppStore
{
    private readonly List<FileEntity> _files = [];
    private readonly List<JobEntity>  _jobs  = [];
    private readonly Lock _lock = new();

    public FileEntity AddFile(FileEntity file)
    {
        lock (_lock) _files.Add(file);
        return file;
    }

    public FileEntity? GetFile(Guid id)
    {
        lock (_lock) return _files.FirstOrDefault(f => f.Id == id);
    }

    public IReadOnlyList<FileEntity> GetAllFiles()
    {
        lock (_lock) return [.. _files];
    }

    public JobEntity AddJob(JobEntity job)
    {
        lock (_lock) _jobs.Add(job);
        return job;
    }

    public JobEntity? GetJob(Guid id)
    {
        lock (_lock) return _jobs.FirstOrDefault(j => j.Id == id);
    }

    public IReadOnlyList<JobEntity> GetAllJobs()
    {
        lock (_lock) return [.. _jobs];
    }

    public IReadOnlyList<JobEntity> GetJobsByFileId(Guid fileId)
    {
        lock (_lock) return _jobs.Where(j => j.FileId == fileId).ToList();
    }
}
