using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Shared.Contracts.Dtos;
using Shared.Contracts.Messaging;

// URL API từ tham số dòng lệnh, mặc định http://localhost:5000
var apiBaseUrl = args.Length > 0 ? args[0].TrimEnd('/') : "http://localhost:5000";

using var http = new HttpClient { BaseAddress = new Uri(apiBaseUrl) };

Console.WriteLine("╔══════════════════════════════════════════════════════╗");
Console.WriteLine("║      HỆ THỐNG XỬ LÝ TỆP BẤT ĐỒNG BỘ — CLIENT      ║");
Console.WriteLine($"║      API: {apiBaseUrl,-42} ║");
Console.WriteLine("╚══════════════════════════════════════════════════════╝");
Console.WriteLine();

// ─── 1. Kiểm tra API health ──────────────────────────────────────────────────
Console.WriteLine("▶ [1] Kiểm tra trạng thái API...");
try
{
    var health = await http.GetStringAsync("/health");
    Console.WriteLine($"   ✓ API sẵn sàng: {health.Trim()}");
}
catch
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"   ✗ Không kết nối được API tại {apiBaseUrl}. Hãy khởi động File.Api trước.");
    Console.ResetColor();
    return;
}
Console.WriteLine();

// ─── 2. Kết nối SignalR Hub ──────────────────────────────────────────────────
// 3.2.3: Xây dựng kết nối SignalR với tự động kết nối lại
Console.WriteLine("▶ [2] Kết nối SignalR tới /hubs/job-status...");

await using var connection = new HubConnectionBuilder()
    .WithUrl($"{apiBaseUrl}/hubs/job-status")
    .WithAutomaticReconnect()           // 3.2.5: Tự động kết nối lại khi mất kết nối
    .Build();

// Nguồn hoàn thành — chờ khi nhận được trạng thái Completed hoặc Failed
var jobCompletedSource = new TaskCompletionSource();

// 3.2.3: Đăng ký hàm xử lý thông điệp từ máy chủ
connection.On<JobStatusMessage>("JobStatusChanged", message =>
{
    var color = message.Status switch
    {
        "Processing" => ConsoleColor.Yellow,
        "Completed"  => ConsoleColor.Green,
        "Failed"     => ConsoleColor.Red,
        _            => ConsoleColor.Gray,
    };
    Console.ForegroundColor = color;
    Console.WriteLine(
        $"   ◉ [SignalR] Job {message.JobId} → {message.Status} lúc {message.UpdatedAt:HH:mm:ss}");
    Console.ResetColor();

    if (message.Status is "Completed" or "Failed")
        jobCompletedSource.TrySetResult();
});

// 3.2.5: Vòng đời kết nối
connection.Reconnecting  += _ => { Console.WriteLine("   [Lifecycle] Đang kết nối lại..."); return Task.CompletedTask; };
connection.Reconnected   += _ => { Console.WriteLine("   [Lifecycle] Đã kết nối lại.");    return Task.CompletedTask; };
connection.Closed        += _ => { Console.WriteLine("   [Lifecycle] Kết nối đã đóng.");   return Task.CompletedTask; };

await connection.StartAsync();
Console.WriteLine($"   ✓ Đã kết nối SignalR (ConnectionId: {connection.ConnectionId![..8]}…)");
Console.WriteLine();

// ─── 3. Tải tệp lên (POST /files) qua multipart/form-data ───────────────────
Console.WriteLine("▶ [3] Đăng ký tệp vào hệ thống (POST /files)...");

var demoFilePath = Path.Combine(Path.GetTempPath(), "demo_image.txt");
await System.IO.File.WriteAllTextAsync(demoFilePath,
    "FILE NÀY DÙNG ĐỂ DEMO — Hệ thống xử lý tệp bất đồng bộ\nSinh viên: Nguyễn Trung Sáng");

using var form        = new MultipartFormDataContent();
using var fileContent = new StreamContent(System.IO.File.OpenRead(demoFilePath));
fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
form.Add(fileContent, "file", "demo_image.txt");

var uploadResponse = await http.PostAsync("/files", form);
if (uploadResponse.StatusCode != HttpStatusCode.Created)
{
    await PrintErrorAsync(uploadResponse, "POST /files");
    return;
}

var uploadedFile = await uploadResponse.Content.ReadFromJsonAsync<FileDto>();
Console.WriteLine($"   ✓ Tệp đã đăng ký:");
Console.WriteLine($"     FileId          = {uploadedFile!.Id}");
Console.WriteLine($"     OriginalFileName= {uploadedFile.OriginalFileName}");
Console.WriteLine($"     ContentType     = {uploadedFile.ContentType}");
Console.WriteLine($"     SizeBytes       = {uploadedFile.SizeBytes}");
Console.WriteLine();

// ─── 4. Tạo job xử lý tệp (POST /jobs) ─────────────────────────────────────
Console.WriteLine("▶ [4] Tạo job xử lý tệp (POST /jobs) — loại: Compress...");
var createJobRequest = new CreateJobRequest(
    FileId:       uploadedFile.Id,
    JobType:      "Compress",
    TargetFormat: null
);

var createJobResponse = await http.PostAsJsonAsync("/jobs", createJobRequest);
if (createJobResponse.StatusCode != HttpStatusCode.Created)
{
    await PrintErrorAsync(createJobResponse, "POST /jobs");
    return;
}

var createdJob = await createJobResponse.Content.ReadFromJsonAsync<JobDto>();
Console.WriteLine($"   ✓ Job đã tạo:");
Console.WriteLine($"     JobId   = {createdJob!.Id}");
Console.WriteLine($"     JobType = {createdJob.JobType}");
Console.WriteLine($"     Status  = {createdJob.Status}  ← trạng thái ban đầu (REST)");
Console.WriteLine();

// ─── 5. Đăng ký theo dõi job qua SignalR (WatchJob) ─────────────────────────
// 3.2.4: Tham gia nhóm theo dõi để nhận thông báo đúng địa chỉ
Console.WriteLine("▶ [5] Đăng ký theo dõi job qua SignalR (WatchJob)...");
await connection.InvokeAsync("WatchJob", createdJob.Id.ToString());
Console.WriteLine($"   ✓ Đang theo dõi nhóm job:{createdJob.Id}");
Console.WriteLine($"   Đang chờ thông báo trạng thái từ máy chủ...");
Console.WriteLine();

// ─── 6. Chờ SignalR đẩy trạng thái Completed/Failed (tối đa 10 giây) ────────
Console.WriteLine("▶ [6] Nhận thông báo thời gian thực qua SignalR:");
var completed = await Task.WhenAny(
    jobCompletedSource.Task,
    Task.Delay(TimeSpan.FromSeconds(10)));

if (completed != jobCompletedSource.Task)
    Console.WriteLine("   ⚠ Quá thời gian chờ thông báo SignalR (10 giây).");
Console.WriteLine();

// ─── 7. Đọc lại trạng thái cuối bằng REST (xác nhận đồng bộ) ────────────────
// Đây là điểm khác biệt với Buổi 2: máy khách chủ động gọi REST để xác nhận
// sau khi SignalR báo xong — không phải polling liên tục.
Console.WriteLine("▶ [7] Xác nhận trạng thái cuối bằng REST (GET /jobs/{id})...");
var latestJob = await http.GetFromJsonAsync<JobDto>($"/jobs/{createdJob.Id}");
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine($"   ✓ Trạng thái REST xác nhận: {latestJob?.Status}");
Console.ResetColor();
Console.WriteLine();

// ─── 8. Lấy danh sách jobs (GET /jobs) ──────────────────────────────────────
Console.WriteLine("▶ [8] Lấy danh sách jobs (GET /jobs)...");
var allJobs = await http.GetFromJsonAsync<List<JobDto>>("/jobs");
Console.WriteLine($"   ✓ Tổng số jobs: {allJobs?.Count ?? 0}");
foreach (var j in allJobs ?? [])
    Console.WriteLine($"     - [{j.Id}] {j.JobType} | Status={j.Status} | File={j.OriginalFileName}");
Console.WriteLine();

// ─── 9. Gửi request sai JobType (kỳ vọng 400) ───────────────────────────────
Console.WriteLine("▶ [9] Gửi job với loại xử lý không hợp lệ — kỳ vọng 400...");
var badJobResponse = await http.PostAsJsonAsync("/jobs", new CreateJobRequest(
    uploadedFile.Id, "invalid_type"));
Console.WriteLine($"   HTTP {(int)badJobResponse.StatusCode} {badJobResponse.StatusCode}");
var badErr = await badJobResponse.Content.ReadFromJsonAsync<ApiError>();
Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine($"   ⚠ {badErr?.Code}: {badErr?.Message}");
Console.ResetColor();
Console.WriteLine();

Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("✔ Demo Buổi 3 hoàn tất — REST + SignalR hoạt động đúng chuẩn.");
Console.ResetColor();

// ─── Hàm tiện ích ────────────────────────────────────────────────────────────
static async Task PrintErrorAsync(HttpResponseMessage response, string action)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"   ✗ [{action}] HTTP {(int)response.StatusCode}");
    Console.WriteLine($"     {await response.Content.ReadAsStringAsync()}");
    Console.ResetColor();
}
