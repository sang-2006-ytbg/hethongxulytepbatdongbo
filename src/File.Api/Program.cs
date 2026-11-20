using File.Api.Data;
using File.Api.Endpoints;
using File.Api.Hubs;

var builder = WebApplication.CreateBuilder(args);

// ─── Dịch vụ ────────────────────────────────────────────────────────────────
builder.Services.AddSingleton<IAppStore, InMemoryAppStore>();
builder.Services.AddEndpointsApiExplorer();
builder.Logging.AddConsole();

// 3.2.1: Đăng ký SignalR vào container DI
builder.Services.AddSignalR();

var app = builder.Build();

// ─── Health check ────────────────────────────────────────────────────────────
app.MapGet("/", () => Results.Ok(new { service = "File.Api", status = "running", version = "buoi-3" }));
app.MapGet("/health", () => Results.Ok(new { healthy = true }));

// ─── Endpoints nghiệp vụ ─────────────────────────────────────────────────────
app.MapFileEndpoints();
app.MapJobEndpoints();

// 3.2.1: Ánh xạ hub vào tuyến /hubs/job-status
app.MapHub<JobStatusHub>("/hubs/job-status");

app.Run();
