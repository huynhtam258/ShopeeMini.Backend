# Hướng dẫn build, chạy và quản lý môi trường

Tài liệu này hướng dẫn build và chạy API ở môi trường **Development** và **Production**, sau đó chỉ cách thêm một môi trường mới, ví dụ **Staging**.

Mọi lệnh bên dưới đều chạy từ **thư mục gốc repo** (`back-end/`). Lệnh được viết cho PowerShell; phần cuối của mục Production có thêm cách chạy trên Linux.

## 0. Cơ chế môi trường trong .NET

.NET không có script kiểu `npm run start:prod`. Môi trường được quyết định bởi biến **`ASPNETCORE_ENVIRONMENT`**:

| Giá trị | Ý nghĩa |
|---|---|
| `Development` | Dùng khi dev. Bật Swagger và nạp thêm `appsettings.Development.json` |
| `Production` | **Mặc định** khi không đặt biến. Không có Swagger |
| Tên bất kỳ (`Staging`, `Testing`...) | App nạp thêm file `appsettings.{Tên}.json` tương ứng, nếu file đó tồn tại |

Cấu hình được nạp theo thứ tự sau; **nguồn nạp sau ghi đè nguồn nạp trước**:

1. `appsettings.json` (dùng chung cho mọi môi trường)
2. `appsettings.{Environment}.json`
3. User secrets (chỉ khi ở Development)
4. Biến môi trường, ví dụ `ConnectionStrings__Default`. Dấu `__` thay cho dấu `:` của khóa lồng nhau
5. Tham số dòng lệnh

Các file cấu hình hiện có trong `ShopeeMiniBackEnd/ShopeeMini.API/`:

| File | Nội dung |
|---|---|
| `appsettings.json` | Toàn bộ cấu hình: connection string, JWT, CORS, rate limiting, đường dẫn hóa đơn và log |
| `appsettings.Development.json` | Connection string cho PostgreSQL local (ghi đè giá trị trong `appsettings.json`) |
| `Properties/launchSettings.json` | 3 profile `http`, `https` và `IIS Express`. Cả 3 đều đặt `ASPNETCORE_ENVIRONMENT=Development` |

> **Lưu ý:** connection string trong `appsettings.json` hiện là **giá trị tạm, giống dev** (`localhost`, user `postgres`). Nếu chạy Production mà chưa cập nhật hoặc chưa ghi đè bằng biến môi trường, app vẫn khởi động bình thường và kết nối vào PostgreSQL `localhost`.

## 1. Development

```powershell
# 1. Bật PostgreSQL (container shopeemini-postgres, database shopee_mini, cổng 5432)
docker compose up -d

# 2. Build bản Debug
dotnet build ShopeeMini.slnx

# 3. Chạy API
dotnet run --project ShopeeMiniBackEnd/ShopeeMini.API/ShopeeMini.API.csproj
```

- `dotnet run` dùng profile đầu tiên trong `launchSettings.json` (`http`), tức môi trường `Development`, ở địa chỉ `http://localhost:5019`.
- Muốn chạy cả HTTPS (`https://localhost:7031`), thêm `--launch-profile https`.
- Swagger UI: `http://localhost:5019/swagger`.
- File `ShopeeMini.slnx` cần .NET SDK 9.0.200 trở lên; các project vẫn target `net8.0`.
- Khi API đang chạy, file build bị khóa. Hãy dừng API trước khi build lại.

## 2. Production

### 2.1. Build (publish)

```powershell
dotnet publish ShopeeMiniBackEnd/ShopeeMini.API/ShopeeMini.API.csproj -c Release -o ./publish
```

Lệnh này build bản Release và gom mọi thứ cần để chạy vào thư mục `./publish` (thư mục này đã có trong `.gitignore`). Kết quả gồm `ShopeeMini.API.dll`, các thư viện phụ thuộc và **tất cả** file `appsettings*.json`.

### 2.2. Chạy

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Production"
$env:ASPNETCORE_URLS = "http://0.0.0.0:8080"
# Ghi đè connection string nếu chưa sửa appsettings.json (khuyến nghị: đừng lưu mật khẩu thật trong file):
$env:ConnectionStrings__Default = "Host=<host>;Port=5432;Database=shopee_mini;Username=<user>;Password=<pass>"

cd publish
dotnet ShopeeMini.API.dll
```

Trên server Linux:

```bash
cd publish
ASPNETCORE_ENVIRONMENT=Production \
ASPNETCORE_URLS=http://0.0.0.0:8080 \
ConnectionStrings__Default="Host=<host>;Port=5432;Database=shopee_mini;Username=<user>;Password=<pass>" \
dotnet ShopeeMini.API.dll
```

Những điểm cần nhớ:

- **Phải `cd publish` trước khi chạy.** App tìm `appsettings.json` theo thư mục hiện tại. Nếu chạy `dotnet publish/ShopeeMini.API.dll` từ thư mục khác, app không thấy file cấu hình và dừng với lỗi `Missing 'ConnectionStrings:Default' configuration.` Thư mục `Logs/` và `App_Data/invoices/` cũng được tạo trong thư mục hiện tại.
- Nếu không đặt `ASPNETCORE_URLS`, app chỉ nghe ở `http://localhost:5000`.
- Khi khởi động, app tự chạy migration và seed dữ liệu (`DbSeeder`), trong đó có tài khoản admin `admin@shopeemini.local` / `Admin@123`. Hãy đổi mật khẩu tài khoản này sau lần chạy đầu trên DB thật.
- `UseHttpsRedirection` vẫn bật. Nếu chỉ chạy HTTP (ví dụ đặt sau reverse proxy lo phần HTTPS), app ghi một cảnh báo không xác định được cổng HTTPS rồi bỏ qua việc chuyển hướng, không ảnh hưởng hoạt động.
- Biến `$env:...` chỉ có tác dụng trong cửa sổ PowerShell hiện tại.

### 2.3. Chạy thử Production ngay trên máy dev (không cần publish)

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Production"
dotnet run --project ShopeeMiniBackEnd/ShopeeMini.API/ShopeeMini.API.csproj -c Release --no-launch-profile
```

Phải có `--no-launch-profile`, vì mọi profile trong `launchSettings.json` đều đặt `ASPNETCORE_ENVIRONMENT=Development` và sẽ ghi đè biến vừa đặt. Muốn quay lại chạy Dev trong cùng cửa sổ, xóa biến bằng `Remove-Item Env:ASPNETCORE_ENVIRONMENT`.

## 3. Thêm môi trường Staging

Staging là môi trường chạy thử trước khi lên Production, nên cấu hình gần giống Production nhưng dùng DB riêng. .NET đã có sẵn tên `Staging` (kèm hàm `IsStaging()`), nên chỉ cần làm các bước sau.

### Bước 1: Tạo `appsettings.Staging.json`

Tạo file `ShopeeMiniBackEnd/ShopeeMini.API/appsettings.Staging.json`. File này **chỉ chứa những khóa khác với `appsettings.json`**, các khóa còn lại sẽ lấy từ `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "Default": "Host=<staging-host>;Port=5432;Database=shopee_mini_staging;Username=<user>;Password="
  },
  "Cors": {
    "AllowedOrigins": [ "https://staging.<ten-mien-front-end>" ]
  }
}
```

- Không commit mật khẩu thật vào file này. Để trống phần mật khẩu, rồi đặt connection string đầy đủ bằng biến môi trường `ConnectionStrings__Default` trên server staging.
- Tên file phải viết đúng hoa thường là `appsettings.Staging.json`, vì trên Linux tên file phân biệt hoa thường.
- `dotnet publish` tự copy file này vào thư mục publish, không cần cấu hình thêm.

### Bước 2: (Tùy chọn) Chạy Staging trên máy qua `launchSettings.json`

`Properties/launchSettings.json` chứa các **launch profile**. Mỗi profile có mục `environmentVariables` riêng, và giá trị `ASPNETCORE_ENVIRONMENT` trong mục đó quyết định môi trường khi chạy profile. Có 2 cách chỉnh.

#### Cách A (khuyến nghị): thêm profile `staging` riêng

Thêm vào mục `profiles`:

```json
"staging": {
  "commandName": "Project",
  "dotnetRunMessages": true,
  "launchBrowser": false,
  "applicationUrl": "http://localhost:5019",
  "environmentVariables": {
    "ASPNETCORE_ENVIRONMENT": "Staging"
  }
}
```

Chạy bằng:

```powershell
dotnet run --project ShopeeMiniBackEnd/ShopeeMini.API/ShopeeMini.API.csproj --launch-profile staging
```

Lệnh `dotnet run` không có `--launch-profile` vẫn dùng profile `http` (Development), nên việc dev hằng ngày không bị ảnh hưởng.

#### Cách B: sửa trực tiếp profile có sẵn

Đổi giá trị `ASPNETCORE_ENVIRONMENT` trong một profile có sẵn, ví dụ profile `http`:

```json
"http": {
  ...
  "environmentVariables": {
    "ASPNETCORE_ENVIRONMENT": "Staging"
  }
}
```

Cách này có tác dụng, nhưng cần lưu ý:

- `http` là **profile đầu tiên** trong file, nên `dotnet run` không có `--launch-profile` sẽ chạy Staging. Mọi người trong team pull code về cũng sẽ bị đổi theo nếu file được commit.
- Chỉ profile bị sửa mới đổi. Profile `https` và `IIS Express` vẫn là Development.
- Nhớ đổi lại `Development` khi dùng xong.

#### Lưu ý chung cho cả 2 cách

- **Viết đúng `Staging`, chữ S viết hoa.** `IsStaging()` so sánh không phân biệt hoa thường, nhưng tên file thì có thể phân biệt. App tìm file `appsettings.{giá trị}.json`, nên nếu viết `staging` thì trên Linux hoặc Docker app sẽ tìm `appsettings.staging.json`, không thấy `appsettings.Staging.json` và âm thầm bỏ qua cấu hình Staging. Trên Windows vẫn chạy được vì hệ điều hành không phân biệt hoa thường, nên lỗi này dễ bị bỏ sót.
- **Khi chạy Staging, app không nạp `appsettings.Development.json`.** Nếu chưa có `appsettings.Staging.json` (bước 1), app chỉ dùng giá trị trong `appsettings.json`.
- **Swagger bị tắt**, vì `Program.cs` chỉ bật Swagger khi `IsDevelopment()`. Nếu profile có `"launchBrowser": true` và `"launchUrl": "swagger"` như các profile có sẵn, trình duyệt sẽ mở `/swagger` và gặp lỗi 404. Muốn có Swagger ở Staging thì làm bước 3.
- **Giá trị trong profile ghi đè biến môi trường đã đặt trong terminal.** Ví dụ đã chạy `$env:ASPNETCORE_ENVIRONMENT = "Production"` nhưng profile ghi `Staging`, thì app vẫn chạy Staging. Muốn dùng biến của terminal thì chạy với `--no-launch-profile`.
- `launchSettings.json` chỉ được dùng bởi `dotnet run` và IDE (Visual Studio, Rider, VS Code), **không** có trong bản publish. Trên server vẫn phải đặt biến môi trường như bước 4.

### Bước 3: (Tùy chọn) Code riêng cho Staging

Trong code, dùng `app.Environment.IsStaging()` hoặc `app.Environment.IsEnvironment("Staging")`. Ví dụ muốn bật Swagger cả ở Staging, sửa điều kiện trong `ShopeeMini.API/Program.cs`:

```csharp
if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
```

### Bước 4: Build và chạy trên server Staging

Build giống hệt Production (mục 2.1). Bản publish dùng chung cho mọi môi trường; môi trường nào được dùng là do biến môi trường đặt lúc chạy.

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Staging"
$env:ASPNETCORE_URLS = "http://0.0.0.0:8080"
$env:ConnectionStrings__Default = "Host=<staging-host>;Port=5432;Database=shopee_mini_staging;Username=<user>;Password=<pass>"

cd publish
dotnet ShopeeMini.API.dll
```

Lần chạy đầu, app tự tạo schema và seed dữ liệu trên DB staging (xem mục 2.2).

### Bước 5: Chạy migration thủ công cho Staging (nếu cần)

Thường không cần, vì app tự migrate lúc khởi động. Nếu muốn migrate trước khi deploy, đặt biến môi trường rồi chạy `dotnet ef` như trong [MIGRATIONS.md](MIGRATIONS.md):

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Staging"
$env:ConnectionStrings__Default = "<connection string staging>"
dotnet ef database update `
  --project ShopeeMiniBackEnd/ShopeeMini.Infrastructure/ShopeeMini.Infrastructure.csproj `
  --startup-project ShopeeMiniBackEnd/ShopeeMini.API/ShopeeMini.API.csproj
```

### Checklist khi thêm một môi trường mới

- [ ] Tạo `appsettings.{Tên}.json`, chỉ chứa các khóa cần ghi đè và không chứa mật khẩu thật.
- [ ] Đặt `ASPNETCORE_ENVIRONMENT={Tên}` và các secret (`ConnectionStrings__Default`...) trên server.
- [ ] Dùng database riêng, không dùng chung với Production.
- [ ] Cập nhật `Cors:AllowedOrigins` theo domain front-end của môi trường đó.
- [ ] (Tùy chọn) Thêm launch profile và code riêng theo môi trường (`IsStaging()` / `IsEnvironment("...")`).
- [ ] Cập nhật bảng file cấu hình ở mục 0 của tài liệu này.
