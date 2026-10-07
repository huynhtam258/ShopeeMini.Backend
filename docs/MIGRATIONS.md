# Hướng dẫn EF Core Migrations

DbContext (`AppDbContext`) nằm ở project **ShopeeMini.Infrastructure**, còn project khởi động (startup project — chứa `Program.cs`, connection string) là **ShopeeMini.API**. Mọi lệnh `dotnet ef` đều cần biết 2 project này qua `--project` (project chứa DbContext) và `--startup-project` (project khởi động).

Tài liệu này viết cho trường hợp bạn đang đứng ở thư mục `ShopeeMiniBackEnd/ShopeeMini.API` (không phải thư mục gốc repo).

## 0. Chuẩn bị

Cài công cụ `dotnet-ef` (chỉ cần 1 lần / máy):
```bash
dotnet tool install --global dotnet-ef
# nếu đã cài rồi nhưng muốn cập nhật:
dotnet tool update --global dotnet-ef
```

Đảm bảo MySQL đang chạy (container `shopeemini-mysql`):
```bash
docker compose up -d   # chạy ở thư mục gốc repo, nơi có docker-compose.yml
```

**Quan trọng:** khi bạn đang đứng sẵn trong thư mục `ShopeeMini.API`, EF Tools tự mặc định lấy **project ở thư mục hiện tại** làm `--startup-project` — nên có thể **bỏ qua** `--startup-project` trong mọi lệnh bên dưới, chỉ cần truyền `--project` trỏ tới `ShopeeMini.Infrastructure` (nơi chứa `AppDbContext`).

## 1. Tạo migration mới

Sau khi sửa entity trong `ShopeeMini.Domain` hoặc cấu hình trong `ShopeeMini.Infrastructure/Persistence/Configurations`, tạo migration mới:

```bash
dotnet ef migrations add <TenMigration> \
  --project ../ShopeeMini.Infrastructure/ShopeeMini.Infrastructure.csproj \
  --output-dir Persistence/Migrations
```

Ví dụ:
```bash
dotnet ef migrations add AddProductRatingColumn \
  --project ../ShopeeMini.Infrastructure/ShopeeMini.Infrastructure.csproj \
  --output-dir Persistence/Migrations
```

Quy ước đặt tên: `PascalCase`, mô tả ngắn gọn thay đổi (`AddXxx`, `RenameXxx`, `RemoveXxx`...). `--output-dir` bắt buộc phải giữ là `Persistence/Migrations` để migration mới nằm cùng chỗ với các migration cũ.

## 2. Xóa migration (chưa apply / vừa tạo nhầm)

Xóa **migration cuối cùng** (chỉ hoạt động nếu migration đó **chưa** được apply vào bất kỳ database nào — nếu đã apply, EF sẽ báo lỗi và gợi ý dùng `--force`):

```bash
dotnet ef migrations remove \
  --project ../ShopeeMini.Infrastructure/ShopeeMini.Infrastructure.csproj
```

Ép xóa dù đã apply vào DB đang kết nối (migration sẽ bị revert khỏi DB đó trước khi xóa file — **cẩn thận** với dữ liệu):
```bash
dotnet ef migrations remove \
  --project ../ShopeeMini.Infrastructure/ShopeeMini.Infrastructure.csproj \
  --force
```

## 3. Xem danh sách migration

```bash
dotnet ef migrations list \
  --project ../ShopeeMini.Infrastructure/ShopeeMini.Infrastructure.csproj
```

## 4. Áp dụng migration vào database

Áp dụng **tất cả** migration chưa chạy (thường dùng nhất):
```bash
dotnet ef database update \
  --project ../ShopeeMini.Infrastructure/ShopeeMini.Infrastructure.csproj
```

Áp dụng tới **một migration cụ thể** (rollback về đúng version đó, các migration sau sẽ bị revert):
```bash
dotnet ef database update <TenMigration> \
  --project ../ShopeeMini.Infrastructure/ShopeeMini.Infrastructure.csproj
```

Rollback **về trạng thái ban đầu** (gỡ hết migration, xóa hết bảng do EF quản lý):
```bash
dotnet ef database update 0 \
  --project ../ShopeeMini.Infrastructure/ShopeeMini.Infrastructure.csproj
```

## 5. Xuất migration ra file SQL (dùng cho production — review trước khi chạy tay)

```bash
dotnet ef migrations script \
  --project ../ShopeeMini.Infrastructure/ShopeeMini.Infrastructure.csproj \
  --idempotent \
  --output migration.sql
```
`--idempotent` sinh script có kiểm tra migration nào đã chạy để không áp dụng trùng — an toàn khi chạy nhiều lần trên nhiều môi trường.

## 6. Xóa toàn bộ database (chỉ dùng khi dev, KHÔNG dùng trên production)

```bash
dotnet ef database drop \
  --project ../ShopeeMini.Infrastructure/ShopeeMini.Infrastructure.csproj \
  --force
```

## Ghi chú

- Nếu chạy lệnh từ **thư mục gốc repo** thay vì từ `ShopeeMini.API`, thêm `--startup-project ShopeeMiniBackEnd/ShopeeMini.API/ShopeeMini.API.csproj` và sửa đường dẫn `--project` thành `ShopeeMiniBackEnd/ShopeeMini.Infrastructure/ShopeeMini.Infrastructure.csproj` (xem thêm ở [README.md](README.md)).
- Migration đọc connection string từ `appsettings.json` trong `ShopeeMini.API` (mục `ConnectionStrings:Default`) — đổi connection string ở đó nếu muốn migrate vào DB khác (staging, production...).
- Luôn commit file migration (`*.cs` + `*.Designer.cs` + `AppDbContextModelSnapshot.cs`) vào git cùng lúc với thay đổi entity liên quan, để người khác pull code về là có ngay migration tương ứng.
