# ShopeeMini — Backend (.NET 8 + MySQL)

Backend mini "Shopee" gồm 2 vai trò (Admin shop, User) xây dựng theo Clean Architecture, đã build và test thủ công thành công (xem mục "Đã kiểm thử" bên dưới).

## 1. Kiến trúc & cấu trúc thư mục

```
ShopeeMiniBackEnd/
  ShopeeMini.Domain          # Entities, Enums thuần (không phụ thuộc EF Core)
  ShopeeMini.Application     # Use-case services, DTO, FluentValidation, interfaces (IAppDbContext, IJwtService...)
  ShopeeMini.Infrastructure  # EF Core DbContext + Migrations, JWT, BCrypt, ghi file JSON
  ShopeeMini.API             # Controllers, Middleware, Filters, Program.cs
```

Application chỉ phụ thuộc Domain; Infrastructure implement các interface do Application định nghĩa (Dependency Inversion) — Application **không** biết EF Core hay MySQL cụ thể, chỉ biết `IAppDbContext`.

SPU/SKU: `Product` = SPU (shell gom nhóm), `ProductSku` = SKU (biến thể có giá & tồn kho riêng). Discount gắn vào từng SKU, có `StartAt`/`EndAt`, không có giảm giá theo tổng đơn.

## 2. Cài đặt & chạy

**Yêu cầu:** .NET 8 SDK, Docker (cho MySQL) hoặc MySQL 8 cài sẵn.

```bash
# 1. Khởi động MySQL (container "shopeemini-mysql", db "shopee_mini")
docker compose up -d

# 2. Áp dụng migration (tạo toàn bộ bảng)
dotnet ef database update \
  --project ShopeeMiniBackEnd/ShopeeMini.Infrastructure/ShopeeMini.Infrastructure.csproj \
  --startup-project ShopeeMiniBackEnd/ShopeeMini.API/ShopeeMini.API.csproj

# 3. Chạy API
dotnet run --project ShopeeMiniBackEnd/ShopeeMini.API/ShopeeMini.API.csproj
```

Swagger UI: `http://localhost:5080/swagger` (khi `ASPNETCORE_ENVIRONMENT=Development`).

Cấu hình nằm ở `ShopeeMiniBackEnd/ShopeeMini.API/appsettings.json`: connection string MySQL, `Jwt` (issuer/audience/signing key/thời hạn token), `Cors:AllowedOrigins`, `RateLimiting`, `InvoiceStorage:Directory`, `Serilog:FilePath`.

Tài khoản admin được seed sẵn khi ứng dụng khởi động lần đầu (xem `DbSeeder`):
- Email: `admin@shopeemini.local`
- Password: `Admin@123`

Người dùng thường tự đăng ký qua `POST /api/auth/register`.

## 3. API Endpoint Matrix

| Method | Endpoint | Auth | Mô tả |
|---|---|---|---|
| POST | /api/auth/register | Public | Đăng ký user |
| POST | /api/auth/login | Public | Đăng nhập, nhận access + refresh token |
| POST | /api/auth/refresh | Public | Cấp lại access token từ refresh token (rotation) |
| POST | /api/auth/logout | Public | Thu hồi refresh token |
| GET | /api/users/me | User/Admin | Thông tin cá nhân |
| PUT | /api/users/me/password | User/Admin | Đổi mật khẩu |
| GET | /api/categories | Public | Danh sách danh mục |
| POST/PUT/DELETE | /api/admin/categories | Admin | CRUD danh mục |
| GET | /api/products | Public | Danh sách sản phẩm public (paging, filter category/search/ngày) |
| GET | /api/products/{id} | Public | Chi tiết sản phẩm public + giá sau giảm |
| GET | /api/admin/products | Admin | Danh sách sản phẩm của shop mình (paging/filter) |
| GET | /api/admin/products/{id} | Admin | Chi tiết sản phẩm của shop mình |
| POST | /api/admin/products | Admin | Tạo SPU + danh sách SKU |
| PUT | /api/admin/products/{id} | Admin | Cập nhật SPU |
| PUT | /api/admin/products/{id}/status | Admin | Ẩn/Hiện — ẩn sẽ xóa khỏi mọi giỏ hàng đang giữ SKU đó + gửi thông báo |
| POST | /api/admin/products/{id}/skus | Admin | Thêm SKU mới cho SPU |
| PUT | /api/admin/products/skus/{skuId}/stock | Admin | Cập nhật tồn kho SKU |
| PUT | /api/admin/products/skus/{skuId}/price | Admin | Cập nhật giá SKU |
| GET/POST | /api/admin/discounts | Admin | Xem/tạo giảm giá theo SKU trong khoảng thời gian |
| DELETE | /api/admin/discounts/{id} | Admin | Hủy (deactivate) giảm giá |
| GET | /api/cart | User | Xem giỏ hàng (kèm giá đã áp giảm giá) |
| POST | /api/cart/items | User | Thêm SKU vào giỏ |
| PUT/DELETE | /api/cart/items/{id} | User | Sửa số lượng / xóa khỏi giỏ |
| POST | /api/orders/checkout | User (header `Idempotency-Key` bắt buộc) | Thanh toán giỏ hàng — Transaction + Atomic Update tồn kho |
| GET | /api/orders | User | Danh sách đơn của mình (paging/filter theo ngày, trạng thái) |
| GET | /api/orders/{id} | User | Chi tiết đơn (kèm đường dẫn hóa đơn JSON) |
| GET | /api/admin/orders | Admin | Danh sách dòng đơn hàng thuộc shop mình (paging/filter) |
| GET | /api/notifications | User/Admin | Danh sách thông báo (paging, filter isRead/ngày) |
| PUT | /api/notifications/{id}/read | User/Admin | Đánh dấu đã đọc |
| DELETE | /api/notifications/{id} | User/Admin | Xóa thông báo |

Tất cả response đều bọc trong envelope chuẩn:
```json
{ "success": true, "data": { }, "message": "Thao tác thành công", "errors": null }
```
`GlobalExceptionMiddleware` (`ShopeeMiniBackEnd/ShopeeMini.API/Middleware/GlobalExceptionMiddleware.cs`) bắt mọi exception và map: `ValidationException`/`BadRequestException` → 400, `UnauthorizedAppException` → 401, `ForbiddenException` → 403, `NotFoundException` → 404, `ConflictException` → 409, còn lại → 500 (che chi tiết, chỉ log).

## 4. Đã kiểm thử thủ công (xem lịch sử phiên làm việc)

- Build toàn bộ solution thành công, `dotnet ef migrations add` + `database update` chạy được trên MySQL 8 (container Docker), 13 bảng được tạo đúng.
- Đăng ký/đăng nhập admin & user, tạo SPU+2 SKU, publish sản phẩm, tạo giảm giá 20% cho 1 SKU → giá hiển thị đúng (150,000 → 120,000).
- **Race condition**: 2 user cùng thêm SKU còn tồn kho = 1 vào giỏ, gọi `/checkout` đồng thời → đúng 1 request `201 Paid`, request còn lại `409 Conflict`, tồn kho cuối cùng = 0 (không bị bán vượt).
- **Idempotency**: gọi lại `/checkout` với cùng `Idempotency-Key` → trả về y hệt response cũ, **không** tạo đơn hàng thứ 2 (tổng số đơn trong DB không đổi). Gọi thiếu header → 400.
- **Ẩn sản phẩm**: admin ẩn sản phẩm đang nằm trong giỏ của user khác → giỏ hàng tự động bị xóa item đó + user nhận được thông báo `ProductHiddenRemovedFromCart`.
- File JSON hóa đơn được ghi ra `App_Data/invoices/{OrderCode}.json` sau khi transaction commit thành công.
- `GET /api/admin/orders` trả đúng danh sách dòng đơn theo shop, gồm cả đơn `Failed`.

---

## 5. Trả lời các câu hỏi thiết kế hệ thống

### 5.1. Những endpoint/tính năng nên tối ưu Query

| Vị trí | Vì sao cần tối ưu | Cách đã áp dụng |
|---|---|---|
| `GET /api/products` (`PublicCatalogService.GetProductsAsync`) | Endpoint public, gọi nhiều nhất, mỗi sản phẩm phải tính giá min/max sau giảm giá từ SKU + Discount | `.AsNoTracking()`, chỉ `Select` đúng cột list cần (không load full entity), `Skip/Take` đẩy xuống SQL, `LongCountAsync` riêng cho tổng số dòng thay vì load hết rồi đếm ở app |
| `GET /api/admin/orders` (`AdminOrderService`) | Có thể có rất nhiều `OrderItem` theo shop | Query trực tiếp trên `OrderItems` (đã có index `(ShopId, CreatedAt)`) thay vì load `Order` rồi lọc item ở app layer |
| `POST /api/orders/checkout` | Là điểm nóng nhất hệ thống (mọi đơn hàng đều qua đây) | Dùng `ExecuteUpdateAsync` — 1 câu `UPDATE` atomic thay vì SELECT rồi UPDATE (giảm 1 round-trip DB, tránh lost update) |
| `GET /api/cart`, `GET /api/notifications` | Gọi lại liên tục từ client (polling) | Index `(UserId, CreatedAt)`/`(UserId, IsRead, CreatedAt)`, `.AsNoTracking()` |

Quy tắc chung áp dụng toàn bộ read-only query trong project: luôn `.AsNoTracking()` (EF Core không cần theo dõi thay đổi để cập nhật lại DB → giảm bộ nhớ + CPU), luôn có pagination, và ưu tiên index đúng theo điều kiện `WHERE`/`ORDER BY` hay dùng (đã khai báo trong `Persistence/Configurations/*.cs`).

### 5.2. Tính năng thể hiện tính ACID

- **Atomicity + Consistency**: `OrderService.CheckoutAsync` — nếu 1 SKU không đủ tồn kho giữa chừng, toàn bộ các lần trừ kho trước đó trong vòng lặp bị `RollbackAsync`, không có SKU nào bị trừ "mồ côi".
- **Isolation**: MySQL/InnoDB dùng row-lock cho câu `UPDATE ... WHERE StockQuantity >= Quantity` — 2 transaction cùng target 1 row bị tuần tự hoá bởi chính DB engine, không cần lock ở tầng ứng dụng.
- **Durability**: sau `transaction.CommitAsync()`, dữ liệu đơn hàng/tồn kho đã ghi bền vào InnoDB — vì vậy việc ghi file JSON hóa đơn được cố ý đặt **sau** commit (I/O đĩa không rollback được, không được phép nằm trong transaction quyết định tiền).

### 5.3. Transaction là gì? Nơi triển khai

Transaction là một nhóm thao tác DB được coi là **một đơn vị không thể chia nhỏ**: hoặc tất cả thành công (commit) hoặc tất cả bị huỷ (rollback), đảm bảo dữ liệu không bao giờ ở trạng thái "làm dở".

Triển khai tại `OrderService.CheckoutAsync` (`ShopeeMiniBackEnd/ShopeeMini.Application/Features/Orders/OrderService.cs`):
```csharp
await using var transaction = await _db.Database.BeginTransactionAsync(ct);
// ... trừ tồn kho từng SKU bằng ExecuteUpdateAsync
// ... tạo Order + OrderItems + Notification
await transaction.CommitAsync(ct);   // hoặc RollbackAsync(ct) nếu hết hàng
```
Một transaction thứ hai, nhỏ hơn, cũng được dùng ở `AdminProductService.SetProductStatusAsync` khi ẩn sản phẩm: xóa cart item + tạo notification phải cùng thành công hoặc cùng thất bại (không thể xóa giỏ hàng mà quên báo cho user).

### 5.4. Tính năng lưu file JSON kết hợp Database

`IInvoiceJsonWriter` / `InvoiceJsonWriter` (`ShopeeMiniBackEnd/ShopeeMini.Infrastructure/Files/InvoiceJsonWriter.cs`): sau khi transaction checkout commit, hệ thống ghi 1 file `App_Data/invoices/{OrderCode}.json` — bản snapshot bất biến của hóa đơn (không đổi dù sau này admin sửa giá/tên sản phẩm). Đường dẫn file được lưu lại vào cột `Order.InvoiceJsonPath` trong MySQL. => Database là nguồn sự thật (source of truth) để truy vấn/tính toán, file JSON là bản export chỉ-đọc phục vụ audit/hỗ trợ khách hàng, không có ràng buộc transaction với DB (vì disk I/O không rollback được).

### 5.5. Idempotency — nếu không dùng thì sao?

**Vấn đề:** Client gọi `POST /api/orders/checkout`, request tới server, server xử lý xong (trừ kho, tạo đơn) nhưng gói response bị rớt trên đường về (mất mạng, client timeout). Client không biết đã thành công hay chưa nên **tự động retry** — nếu server không có cơ chế chống trùng, request thứ 2 sẽ chạy lại toàn bộ logic checkout: trừ kho lần nữa, tạo đơn hàng thứ 2, tính tiền 2 lần cho cùng 1 giỏ hàng.

**Đã triển khai:** `IdempotencyFilter` (`ShopeeMiniBackEnd/ShopeeMini.API/Filters/IdempotencyFilter.cs`) bắt buộc header `Idempotency-Key` trên endpoint checkout. Lần gọi đầu tiên chạy bình thường và cache lại response (status code + body) vào bảng `IdempotencyRecords`, khoá theo `(UserId, Key, RequestPath)`. Mọi lần gọi lại với cùng key sẽ được trả thẳng response đã cache, **không** chạm vào `OrderService` — đã kiểm thử thực tế ở mục 4.

**Nếu không dùng Idempotency-Key** thì cách duy nhất để giảm rủi ro là dựa vào chính cơ chế Atomic Update tồn kho (mục 5.6) để tránh bán âm — nhưng vẫn **không** tránh được việc tạo 2 Order riêng biệt (nếu còn đủ hàng cho cả 2 lần trừ), tức khách hàng bị trừ tiền/đơn 2 lần dù chỉ bấm mua 1 lần — đây chính là lỗi kinh điển "duplicate charge" trong hệ thống thanh toán.

### 5.6. Bài toán 2 user mua sản phẩm còn 1 chiếc cuối cùng

Giải quyết bằng **Atomic Conditional Update** ngay trong transaction, thay vì "đọc tồn kho rồi kiểm tra rồi ghi" (pattern đó có race condition kinh điển: cả 2 request đều đọc `StockQuantity = 1` trước khi request nào kịp ghi):

```csharp
var affectedRows = await _db.ProductSkus
    .Where(s => s.Id == item.ProductSkuId && s.StockQuantity >= item.Quantity)
    .ExecuteUpdateAsync(setters => setters.SetProperty(s => s.StockQuantity, s => s.StockQuantity - item.Quantity), ct);

if (affectedRows == 0) { /* hết hàng — rollback toàn bộ transaction */ }
```
Đây là **một câu SQL `UPDATE` duy nhất** — điều kiện `StockQuantity >= Quantity` được kiểm tra và cập nhật trong cùng 1 statement mà MySQL/InnoDB tự khóa row đó cho tới khi statement xong. Request thứ 2 phải đợi request thứ 1 hoàn tất; khi nó chạy, tồn kho đã về 0 nên điều kiện `WHERE` không match → `affectedRows == 0` → hiểu là hết hàng, rollback, trả 409. Đã kiểm thử thực tế bằng 2 curl request đồng thời (mục 4): kết quả đúng 1 thành công, 1 thất bại, tồn kho cuối = 0.

### 5.7. Ưu / nhược điểm hệ thống hiện tại & hướng scale, giảm latency

**Ưu điểm:**
- Clean Architecture tách rõ Domain/Application/Infrastructure/API → dễ test, dễ thay MySQL bằng DB khác (chỉ đổi Infrastructure).
- Toàn bộ I/O dùng async/await, decimal cho tiền tệ, `.AsNoTracking()` cho read-query, validate DTO tập trung qua 1 `ValidationFilter` (không rải rác if/else trong controller).
- Idempotency + Atomic Update giải quyết đúng 2 rủi ro thực tế nhất của hệ thống thương mại điện tử (double-charge, oversell) mà không cần lock phân tán (Redis/Redlock).

**Nhược điểm / giới hạn hiện tại:**
- Một MySQL instance duy nhất — vừa là điểm chịu tải chính vừa là SPOF (single point of failure).
- `ExecuteUpdateAsync` từng SKU trong 1 vòng lặp `foreach` — checkout giỏ hàng nhiều SKU sẽ mở nhiều round-trip tuần tự tới DB thay vì 1 batch; với giỏ hàng lớn, độ trễ tăng tuyến tính theo số dòng.
- Danh sách sản phẩm public tính `MIN/MAX` giá + kiểm tra discount bằng subquery lồng trong `Select` — với catalog rất lớn (hàng triệu SKU) sẽ cần bảng tổng hợp giá (materialized) thay vì tính runtime.
- Chưa có cache (Redis) cho `GET /api/products` — mỗi request đều chạm DB dù dữ liệu catalog thay đổi không thường xuyên.
- Rate limiting theo IP ở tầng ứng dụng (in-memory) — không chia sẻ trạng thái giữa nhiều instance nếu scale-out nhiều node.
- File hóa đơn JSON lưu trên local disk của instance chạy API — không phù hợp khi chạy nhiều container (container khác sẽ không thấy file).

**Hướng scale & giảm latency nếu lên production:**
1. **Đọc nhiều hơn ghi** (catalog, danh sách sản phẩm) → thêm Redis cache theo `productId`/`categoryId`, invalidate khi admin sửa giá/tồn kho/ẩn hiện.
2. **Tách read/write DB**: MySQL primary (ghi đơn hàng, tồn kho) + read replica cho các API public (`GET /api/products`, `GET /api/products/{id}`).
3. **Scale ngang API**: chạy nhiều instance sau load balancer → cần chuyển rate limiter từ in-memory sang Redis-backed (`RedisRateLimiter`/`Microsoft.AspNetCore.RateLimiting` + distributed store), và invoice JSON nên chuyển sang object storage dùng chung (S3/MinIO/Azure Blob) thay vì đĩa cục bộ.
4. **Hàng nóng (flash-sale 1 SKU, rất nhiều request cùng lúc)**: atomic UPDATE vẫn đúng nhưng tất cả request dồn vào lock 1 row → có thể thêm hàng đợi (RabbitMQ/Kafka) nhận request checkout, xử lý tuần tự theo SKU để tránh nghẽn kết nối DB, trả kết quả bất đồng bộ qua notification.
5. **Observability**: mỗi request đã có `CorrelationId` (mục 5.8) — khi scale nhiều instance, gom log tập trung (ELK/Seq/Grafana Loki) để trace một request xuyên suốt nhiều service.
6. **CQRS nhẹ**: tách hẳn model đọc (catalog, đơn hàng liệt kê) khỏi model ghi (checkout) nếu lưu lượng đọc vượt trội, cho phép tối ưu độc lập từng phía.

### 5.8. Hệ thống Logger

- **Serilog** cấu hình tại `Program.cs`: ghi đồng thời ra Console (dev) và file (`Logs/log-YYYYMMDD.txt`, rolling theo ngày, giữ 14 ngày).
- **`CorrelationIdMiddleware`** (`ShopeeMiniBackEnd/ShopeeMini.API/Middleware/CorrelationIdMiddleware.cs`): mỗi request được gắn 1 `CorrelationId` (lấy từ header `X-Correlation-Id` nếu client gửi, hoặc tự sinh) và push vào `LogContext` — mọi dòng log trong lúc xử lý request đó (kể cả log lỗi ở tầng service) đều có cùng 1 `CorrelationId`, trả kèm trong response header để debug 2 chiều (client báo lỗi kèm ID → tìm đúng log).
- **`GlobalExceptionMiddleware`** log `Warning` cho lỗi nghiệp vụ (400/401/403/404/409 — không cần điều tra sâu) và `Error` kèm đầy đủ stack trace cho lỗi 500 thật sự (unhandled).
- `Microsoft.AspNetCore`/`Microsoft.EntityFrameworkCore` bị hạ xuống `Warning` để log không bị rác bởi log framework nội bộ.

---

## 6. Việc còn để mở rộng sau (đã ghi nhận theo yêu cầu)

- Upload/quản lý hình ảnh sản phẩm (hiện tại chỉ quản lý text).
- Refresh token hiện lưu ở bảng `RefreshTokens`, chưa giới hạn số thiết bị đăng nhập đồng thời.
