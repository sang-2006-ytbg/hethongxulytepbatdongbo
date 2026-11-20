# Hệ Thống Xử Lý Tệp Bất Đồng Bộ

**Học phần:** Phát triển ứng dụng phân tán  
**Đề tài 05:** Hệ thống xử lý tệp bất đồng bộ  
**Sinh viên:** Nguyễn Trung Sáng

---

## Mô tả dự án

Hệ thống cho phép người dùng tải tệp lên và yêu cầu xử lý nền (tạo thumbnail, chuyển định dạng, nén tệp). API tiếp nhận yêu cầu, tạo job và đẩy cập nhật trạng thái thời gian thực tới client qua SignalR.

Kiến trúc hệ thống:

```
Client ──HTTP/REST──▶  File.Api  ──▶  [RabbitMQ]  ──▶  Processing.Worker
   │                       │                                    │
   │ ◀──SignalR (push)── [Hub]           [Database]  ◀──────── ┘
   │                                         │
   │                                   File Storage
   └────────────────────────────────────────────────
```

> **Trạng thái hiện tại (Buổi 3):** Lưu trữ in-memory, tích hợp SignalR để đẩy trạng thái job thời gian thực. Chưa có Database, RabbitMQ và Worker thật.

---

## Cấu trúc thư mục

```
AsyncFileProcessor/
├── AsyncFileProcessor.sln
├── src/
│   ├── Shared.Contracts/        ← DTOs, Constants, Messages dùng chung
│   │   ├── Constants/
│   │   │   ├── JobStatuses.cs   ← Pending, Processing, Completed, Failed
│   │   │   └── JobTypes.cs      ← Thumbnail, ConvertFormat, Compress
│   │   ├── Dtos/
│   │   │   ├── FileDtos.cs
│   │   │   ├── JobDtos.cs
│   │   │   └── ApiError.cs
│   │   └── Messaging/
│   │       ├── JobQueuedMessage.cs
│   │       └── JobStatusMessage.cs  ← Thông điệp SignalR (Buổi 3)
│   │
│   ├── File.Api/                ← ASP.NET Core Minimal API .NET 9
│   │   ├── Data/
│   │   │   ├── AppStore.cs      ← IAppStore + InMemoryAppStore
│   │   │   └── Entities/        ← FileEntity, JobEntity
│   │   ├── Endpoints/
│   │   │   ├── FileEndpoints.cs ← POST/GET /files, GET /files/{id}/result
│   │   │   └── JobEndpoints.cs  ← POST/GET /jobs, GET /jobs/{id} + SignalR push
│   │   ├── Hubs/                ← SignalR Hubs (Buổi 3)
│   │   │   ├── JobStatusHub.cs  ← Hub cho thông báo trạng thái job
│   │   │   └── JobGroups.cs     ← Quy ước đặt tên nhóm SignalR
│   │   └── Program.cs
│   │
│   └── File.Client/             ← Console Client gọi API + SignalR
│       └── Program.cs           ← Demo 9 bước: REST + SignalR thời gian thực
└── README.md
```

---

## API Endpoints

| Method | Endpoint | Mô tả | HTTP Response |
|--------|----------|-------|---------------|
| `GET`  | `/` | Kiểm tra API | `200 OK` |
| `GET`  | `/health` | Health check | `200 OK` |
| `POST` | `/files` | Tải tệp lên (multipart/form-data) | `201 Created` |
| `GET`  | `/files` | Danh sách tệp | `200 OK` |
| `GET`  | `/files/{id}` | Chi tiết tệp | `200` / `404` |
| `GET`  | `/files/{id}/result` | Tải tệp kết quả | `200` / `400` / `404` |
| `POST` | `/jobs` | Tạo job xử lý | `201 Created` |
| `GET`  | `/jobs` | Danh sách jobs | `200 OK` |
| `GET`  | `/jobs/{id}` | Trạng thái job | `200` / `404` |

### SignalR Hub

| Hub URL | Phương thức client gọi | Sự kiện máy chủ đẩy |
|---------|------------------------|----------------------|
| `/hubs/job-status` | `WatchJob(jobId)` | `JobStatusChanged(JobStatusMessage)` |

Luồng hoạt động SignalR:
1. Client kết nối tới `/hubs/job-status`
2. Client gọi REST `POST /jobs` để tạo job
3. Client gọi hub `WatchJob(jobId)` để tham gia nhóm theo dõi
4. Máy chủ tự động đẩy: `Pending → Processing → Completed` qua SignalR

### Các loại xử lý tệp

| JobType | Mô tả |
|---------|-------|
| `Thumbnail` | Tạo ảnh thu nhỏ (320px) |
| `ConvertFormat` | Chuyển đổi định dạng ảnh |
| `Compress` | Nén tệp thành `.zip` |

### Trạng thái job

`Pending` → `Processing` → `Completed` / `Failed`

---

## Hướng dẫn chạy

### Yêu cầu
- [.NET 9 SDK](https://dotnet.microsoft.com/download)

### Chạy API

```bash
dotnet run --project src/File.Api --urls http://localhost:5000
```

### Chạy Client (mở terminal mới)

```bash
dotnet run --project src/File.Client -- http://localhost:5000
```

Kết quả mong đợi ở client:

```
✓ Đã kết nối SignalR...
✓ Job đã tạo:  Status = Pending  ← trạng thái ban đầu (REST)
◉ [SignalR] Job <id> → Processing lúc HH:mm:ss
◉ [SignalR] Job <id> → Completed  lúc HH:mm:ss
✓ Trạng thái REST xác nhận: Completed
```
