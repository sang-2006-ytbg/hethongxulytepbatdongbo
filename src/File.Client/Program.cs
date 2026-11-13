using System.Net;
using System.Net.Http.Json;
using Shared.Contracts.Dtos;

// URL API từ tham số dòng lệnh, mặc định http://localhost:5000
var apiBaseUrl = args.Length > 0 ? args[0] : "http://localhost:5000";

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

// ─── 2. Tải tệp lên (POST /files) qua multipart/form-data ───────────────────
Console.WriteLine("▶ [2] Đăng ký tệp vào hệ thống (POST /files)...");

// Tạo một tệp giả để demo
var demoFilePath = Path.Combine(Path.GetTempPath(), "demo_image.txt");
await System.IO.File.WriteAllTextAsync(demoFilePath,
    "FILE NÀY DÙNG ĐỂ DEMO — Hệ thống xử lý tệp bất đồng bộ\nSinh viên: Nguyễn Trung Sáng");

using var form = new MultipartFormDataContent();
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
Console.WriteLine($"     UploadedAt      = {uploadedFile.UploadedAt:yyyy-MM-dd HH:mm:ss} UTC");
Console.WriteLine();

// ─── 3. Lấy danh sách tệp (GET /files) ──────────────────────────────────────
Console.WriteLine("▶ [3] Lấy danh sách tệp (GET /files)...");
var files = await http.GetFromJsonAsync<List<FileDto>>("/files");
Console.WriteLine($"   ✓ Tổng số tệp: {files?.Count ?? 0}");
foreach (var f in files ?? [])
    Console.WriteLine($"     - [{f.Id}] {f.OriginalFileName} | HasResult={f.HasResult}");
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
Console.WriteLine($"     Status  = {createdJob.Status}  ← trạng thái ban đầu");
Console.WriteLine();

// ─── 5. Tra cứu trạng thái job (GET /jobs/{id}) ─────────────────────────────
Console.WriteLine("▶ [5] Tra cứu trạng thái job (GET /jobs/{id})...");
var jobDetail = await http.GetFromJsonAsync<JobDto>($"/jobs/{createdJob.Id}");
Console.WriteLine($"   ✓ Job [{jobDetail!.Id}] — Status: {jobDetail.Status} | File: {jobDetail.OriginalFileName}");
Console.WriteLine();

// ─── 6. Lấy danh sách tất cả jobs (GET /jobs) ───────────────────────────────
Console.WriteLine("▶ [6] Lấy danh sách jobs (GET /jobs)...");
var allJobs = await http.GetFromJsonAsync<List<JobDto>>("/jobs");
Console.WriteLine($"   ✓ Tổng số jobs: {allJobs?.Count ?? 0}");
foreach (var j in allJobs ?? [])
    Console.WriteLine($"     - [{j.Id}] {j.JobType} | Status={j.Status} | File={j.OriginalFileName}");
Console.WriteLine();

// ─── 7. Lấy kết quả (chưa xử lý → 400 JOB_NOT_COMPLETED) ──────────────────
Console.WriteLine("▶ [7] Lấy kết quả tệp — kỳ vọng lỗi vì job đang Pending...");
var resultResponse = await http.GetAsync($"/files/{uploadedFile.Id}/result");
Console.WriteLine($"   HTTP {(int)resultResponse.StatusCode} {resultResponse.StatusCode}");
if (!resultResponse.IsSuccessStatusCode)
{
    var err = await resultResponse.Content.ReadFromJsonAsync<ApiError>();
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine($"   ⚠ {err?.Code}: {err?.Message}");
    Console.ResetColor();
}
Console.WriteLine();

// ─── 8. Gửi request sai JobType (kỳ vọng 400) ───────────────────────────────
Console.WriteLine("▶ [8] Gửi job với loại xử lý không hợp lệ — kỳ vọng 400...");
var badJobResponse = await http.PostAsJsonAsync("/jobs", new CreateJobRequest(
    uploadedFile.Id, "invalid_type"));
Console.WriteLine($"   HTTP {(int)badJobResponse.StatusCode} {badJobResponse.StatusCode}");
var badErr = await badJobResponse.Content.ReadFromJsonAsync<ApiError>();
Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine($"   ⚠ {badErr?.Code}: {badErr?.Message}");
Console.ResetColor();
Console.WriteLine();

// ─── 9. Tìm tệp không tồn tại (kỳ vọng 404) ─────────────────────────────────
Console.WriteLine("▶ [9] Tra cứu tệp không tồn tại — kỳ vọng 404...");
var notFoundResponse = await http.GetAsync($"/files/{Guid.NewGuid()}");
Console.WriteLine($"   HTTP {(int)notFoundResponse.StatusCode} {notFoundResponse.StatusCode}");
var notFoundErr = await notFoundResponse.Content.ReadFromJsonAsync<ApiError>();
Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine($"   ⚠ {notFoundErr?.Code}: {notFoundErr?.Message}");
Console.ResetColor();
Console.WriteLine();

Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("✔ Demo Buổi 2 hoàn tất — RESTful API hoạt động đúng chuẩn HTTP.");
Console.ResetColor();

// ─── Hàm tiện ích ────────────────────────────────────────────────────────────
static async Task PrintErrorAsync(HttpResponseMessage response, string action)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"   ✗ [{action}] HTTP {(int)response.StatusCode}");
    Console.WriteLine($"     {await response.Content.ReadAsStringAsync()}");
    Console.ResetColor();
}
