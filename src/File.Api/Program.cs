using File.Api.Data;
using File.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);

// ─── Dịch vụ ────────────────────────────────────────────────────────────────
builder.Services.AddSingleton<IAppStore, InMemoryAppStore>();
builder.Services.AddEndpointsApiExplorer();
builder.Logging.AddConsole();

var app = builder.Build();

// ─── Health check ────────────────────────────────────────────────────────────
app.MapGet("/", () => Results.Ok(new { service = "File.Api", status = "running", version = "buoi-2" }));
app.MapGet("/health", () => Results.Ok(new { healthy = true }));

// ─── Endpoints nghiệp vụ ─────────────────────────────────────────────────────
app.MapFileEndpoints();
app.MapJobEndpoints();

app.Run();
