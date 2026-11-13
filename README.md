# Hệ Thống Xử Lý Tệp Bất Đồng Bộ

**Học phần:** Phát triển ứng dụng phân tán  
**Đề tài 05:** Hệ thống xử lý tệp bất đồng bộ  
**Sinh viên:** Nguyễn Trung Sáng

---

## Mô tả dự án

Hệ thống cho phép người dùng tải tệp lên và yêu cầu xử lý nền (tạo thumbnail, chuyển định dạng, nén tệp). API tiếp nhận yêu cầu, tạo job với trạng thái ban đầu và trả về ngay cho client.

Kiến trúc hệ thống:

```
Client ──HTTP/REST──▶  File.Api  ──▶  [RabbitMQ]  ──▶  Processing.Worker
                           │                                    │
                       [Database]  ◀─────────────────────────── ┘
                           │
                     File Storage
```

> **Trạng thái hiện tại (Buổi 2):** Lưu trữ in-memory, chưa có Database, RabbitMQ và Worker.

---

## Cấu trúc thư mục

```
AsyncFileProcessor/
├── AsyncFileProcessor.sln
├── src/
│   ├── Shared.Contracts/        ← DTOs, Constants, Messages dùng chung
│   │   ├── Constants/
│   │   │   └── JobConstants.cs  ← JobStatuses, JobTypes
│   │   ├── Dtos/
│   │   │   └── Dtos.cs          ← FileDto, JobDto, CreateJobRequest, ApiError
│   │   └── Messaging/
│   │       └── JobQueuedMessage.cs
│   │
│   ├── File.Api/                ← ASP.NET Core Minimal API .NET 9
│   │   ├── Data/
│   │   │   ├── AppStore.cs      ← IAppStore + InMemoryAppStore
│   │   │   └── Entities/        ← FileEntity, JobEntity
│   │   ├── Endpoints/
│   │   │   ├── FileEndpoints.cs ← POST/GET /files, GET /files/{id}/result
│   │   │   └── JobEndpoints.cs  ← POST/GET /jobs, GET /jobs/{id}
│   │   └── Program.cs
│   │
│   └── File.Client/             ← Console Client gọi API
│       └── Program.cs           ← Demo 9 bước kiểm tra API
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
dotnet run --project src/File.Client
```
