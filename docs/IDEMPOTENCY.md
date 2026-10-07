# Idempotency trong ShopeeMini

Tài liệu này giải thích **Idempotency** là gì, khi nào cần dùng, nếu không dùng thì chuyện gì xảy ra, và cơ chế `Idempotency-Key` đang được cài trong backend ShopeeMini hoạt động như thế nào.

---

## 1. Idempotency là gì?

Một thao tác là **idempotent** nếu **thực hiện 1 lần hay N lần thì trạng thái cuối cùng của hệ thống vẫn như nhau**.

Ví dụ đời thường:

| Thao tác | Idempotent? | Vì sao |
|---|---|---|
| Bấm nút "tầng 5" trong thang máy 3 lần | ✅ Có | Thang máy vẫn chỉ đi tới tầng 5 |
| Đặt số dư tài khoản = 100.000đ | ✅ Có | Gán bao nhiêu lần thì vẫn là 100.000đ |
| Chuyển khoản 100.000đ | ❌ Không | Làm 3 lần là mất 300.000đ |
| Thêm 1 sản phẩm vào giỏ (số lượng `+= 1`) | ❌ Không | Làm 3 lần là có 3 cái |

Lưu ý: idempotent nói về **trạng thái hệ thống (side effect)**, không bắt buộc response phải giống hệt nhau. Ví dụ `DELETE /items/5` lần đầu trả `200`, lần hai trả `404`, nhưng item 5 vẫn chỉ bị xóa một lần, nên vẫn tính là idempotent.

### 1.1. HTTP method và idempotency

| Method | Idempotent theo chuẩn HTTP? | Ghi chú |
|---|---|---|
| `GET`, `HEAD`, `OPTIONS` | ✅ | Chỉ đọc, không đổi dữ liệu |
| `PUT` | ✅ | "Gán" toàn bộ tài nguyên về một giá trị |
| `DELETE` | ✅ | Xóa rồi thì xóa thêm vẫn là "đã xóa" |
| `POST` | ❌ | Thường là "tạo mới" hoặc "thực hiện hành động", mỗi lần gọi là một lần mới |
| `PATCH` | ❌ (tùy cách viết) | `set name = "A"` thì idempotent, `increment counter` thì không |

Chuẩn HTTP chỉ là **cam kết**. Code backend phải viết đúng thì mới thật sự idempotent. Một `PUT` mà bên trong làm `Quantity += x` thì vẫn không idempotent.

---

## 2. Vì sao cần? Bài toán "retry"

Trong mạng thực tế, client **không thể phân biệt** hai tình huống sau:

```
Tình huống A: request chưa tới server
  Client ──── POST /checkout ───X  (mất mạng)        Server: chưa làm gì

Tình huống B: server đã xử lý xong, nhưng response bị mất
  Client ──── POST /checkout ────────────────────▶  Server: trừ kho, tạo đơn, COMMIT ✅
  Client  X◀─── 201 Created ─────────────────────   (timeout / rớt wifi / app bị kill)
```

Ở cả hai trường hợp, client chỉ thấy **"timeout, không biết kết quả"**. Cách xử lý tự nhiên là **gửi lại (retry)**. Retry xảy ra rất thường xuyên:

- Người dùng bấm nút "Thanh toán" 2 lần vì thấy app chậm (double-click).
- Thư viện HTTP hoặc mobile SDK tự retry khi timeout.
- Load balancer hoặc reverse proxy retry khi upstream phản hồi chậm.
- App mobile mất mạng giữa chừng, có mạng lại thì gửi lại request trong hàng đợi.

Nếu endpoint **không idempotent**, mỗi lần retry ở tình huống B sẽ **thực hiện lại side effect**.

---

## 3. Không dùng thì bị gì?

Với một endpoint thanh toán "kiểu chung" (ví dụ "Mua ngay" với body `{ skuId, quantity }`, không phụ thuộc giỏ hàng), nếu bỏ Idempotency thì:

```
Lần 1:  Client ── POST /checkout ──▶ Server
                                     ├─ Trừ kho SKU#12: 10 → 8
                                     ├─ Tạo Order ORD...6569 (Paid, 500.000đ)
                                     └─ COMMIT
        Client  X◀── 201 ────────── (response bị mất)

Lần 2 (retry):
        Client ── POST /checkout ──▶ Server
                                     ├─ Trừ kho SKU#12: 8 → 6       ❌ trừ kho lần 2
                                     ├─ Tạo Order ORD...8247 (Paid) ❌ đơn thứ 2
                                     └─ COMMIT
```

**Riêng `POST /api/orders/checkout` của ShopeeMini** thanh toán *toàn bộ giỏ hàng* và **xóa giỏ trong cùng transaction**. Vì vậy nếu bỏ Idempotency:

- **Retry tuần tự** (lần 1 đã commit): giỏ đã trống, nên lần 2 trả **400 "Giỏ hàng đang trống"**. Không tạo đơn trùng, nhưng client tưởng thanh toán **thất bại** trong khi thật ra đã thành công. User có thể thêm lại hàng vào giỏ rồi mua lần nữa, và lúc đó là double charge thật.
- **Request song song** (double-click, hoặc client retry khi lần 1 còn đang chạy): cả hai cùng đọc thấy giỏ còn hàng, và kết quả phụ thuộc thời điểm (500, hoặc sinh thêm đơn `Failed` giả; xem mục 9.1).
- Chỉ cần sau này đổi logic (ví dụ thêm "Mua ngay", hoặc giữ giỏ hàng sau khi mua) là quay về đúng kịch bản tạo 2 đơn ở trên.

Nói cách khác, việc xóa giỏ hàng chỉ **tình cờ** che bớt lỗi. Idempotency-Key mới là cơ chế **chủ động** bảo đảm "retry trả về đúng kết quả lần đầu".

Hậu quả cụ thể của việc thiếu idempotency trên một endpoint thanh toán:

| Hậu quả | Mức độ |
|---|---|
| **Double charge**: khách bị tính tiền 2 lần cho 1 lần bấm mua | 🔴 Nghiêm trọng: mất tiền, khiếu nại, chargeback |
| **Tồn kho sai**: kho bị trừ 2 lần, sản phẩm báo "hết hàng" dù còn | 🔴 Mất doanh thu |
| Shop nhận 2 thông báo "Có đơn hàng mới", có thể giao hàng 2 lần | 🟠 Chi phí vận hành |
| Ghi 2 file hóa đơn JSON trong `App_Data/invoices/` | 🟡 Dữ liệu rác |
| Client nhận lỗi khó hiểu (500, 409) dù lần đầu đã thành công | 🟡 Trải nghiệm tệ |

> **Atomic Update tồn kho (mục 5.6 trong README) KHÔNG thay thế được Idempotency.**
> Atomic Update chỉ chống **bán âm (oversell)**, tức là 2 người tranh nhau chiếc cuối cùng.
> Nếu kho còn đủ cho cả 2 lần trừ, cả 2 lần đều hợp lệ trong mắt database, và khách vẫn bị tạo 2 đơn.
> Hai cơ chế giải quyết **2 bài toán khác nhau**, và cần dùng cả hai.

---

## 4. Khi nào cần dùng Idempotency-Key?

Cần dùng khi endpoint thỏa **cả 3 điều kiện**:

1. **Có side effect**: ghi DB, trừ tiền, trừ kho, gửi email/SMS, gọi API bên thứ ba.
2. **Không tự nhiên idempotent**: thường là `POST` tạo mới, hoặc thao tác kiểu "cộng dồn" (`+=`).
3. **Hậu quả của việc chạy 2 lần là đáng kể**: liên quan đến tiền, kho, hoặc không thể hoàn tác.

Ví dụ điển hình: thanh toán, đặt hàng, chuyển khoản, tạo hóa đơn, gửi tin nhắn, gọi cổng thanh toán (VNPay, Momo, Stripe...).

**Không cần** khi:

- Endpoint chỉ đọc (`GET`).
- Endpoint đã idempotent theo thiết kế: `PUT` gán giá trị tuyệt đối, `DELETE`.
- Có ràng buộc unique tự nhiên đã chặn trùng (ví dụ email đăng ký là unique), và việc trả lỗi `409` khi retry là chấp nhận được.

### 4.1. Phân tích các endpoint ghi dữ liệu trong ShopeeMini

| Endpoint | Tự nhiên idempotent? | Retry thì sao | Đánh giá |
|---|---|---|---|
| `POST /api/orders/checkout` | ❌ | Tạo 2 đơn, trừ kho 2 lần | 🔴 **Bắt buộc**, **đã cài** `IdempotencyFilter` |
| `POST /api/cart/items` | ❌ (`existing.Quantity += request.Quantity`) | Số lượng trong giỏ bị cộng gấp đôi | 🟠 Nên cân nhắc. User còn thấy giỏ trước khi thanh toán nên rủi ro thấp hơn |
| `POST /api/admin/products`, `.../skus`, discount, `POST /api/categories` | ❌ | Tạo bản ghi trùng | 🟡 Có thể thêm nếu cần (chỉ admin dùng, dễ phát hiện và xóa) |
| `POST /api/auth/register` | ✅ một phần (email unique) | Lần 2 trả lỗi "email đã tồn tại" | 🟢 Chấp nhận được. Không dùng được filter hiện tại vì endpoint là `AllowAnonymous` |
| `PUT /api/cart/items/{id}` | ✅ (`item.Quantity = request.Quantity`) | Gán lại cùng giá trị | 🟢 Không cần |
| `PUT /api/admin/products/skus/{id}/stock` | ✅ (`UpdateStockDto(int StockQuantity)` là giá trị tuyệt đối) | Gán lại cùng giá trị | 🟢 Không cần |
| `PUT .../price`, `PUT .../status`, `PUT /api/notifications/{id}/read` | ✅ | Gán lại cùng giá trị | 🟢 Không cần |
| `DELETE /api/cart/items/{id}`, `DELETE /api/notifications/{id}`, ... | ✅ | Lần 2 trả `404`, dữ liệu không đổi | 🟢 Không cần |

> Bài học thiết kế: cách rẻ nhất để có idempotency là **viết API theo kiểu "gán giá trị" thay vì "cộng dồn"**.
> `PUT stock = 50` an toàn khi retry, còn `POST stock/increase +10` thì không.

---

## 5. Cơ chế Idempotency-Key: ý tưởng chung

Đây là pattern mà Stripe, PayPal, Adyen... đều dùng:

1. Client sinh một **khóa ngẫu nhiên duy nhất** (thường là UUID) cho **mỗi ý định hành động** (mỗi lần "tôi muốn thanh toán giỏ hàng này").
2. Client gửi khóa đó trong header `Idempotency-Key`. **Khi retry, client gửi lại đúng khóa cũ.**
3. Server lưu `khóa → response`. Gặp lại khóa đã có thì **trả lại response cũ, không chạy lại logic.**

```
                       ┌─────────────────────────────┐
 POST /checkout        │ Có record (user, key, path)? │
 Idempotency-Key: abc ─▶                              │
                       └──────────────┬──────────────┘
                          Không có     │       Có
                     ┌────────────────┴───────────────┐
                     ▼                                ▼
           Chạy CheckoutAsync()              Trả lại status + body
           (trừ kho, tạo đơn...)             đã lưu. KHÔNG chạy
                     │                       lại OrderService.
                     ▼
           Thành công (2xx)? ──Có──▶ Lưu (key, status, body)
                     │                vào IdempotencyRecords
                    Không
                     ▼
           Không lưu (lần sau gửi cùng key sẽ chạy lại)
```

---

## 6. Cách cài đặt trong ShopeeMini

Gồm 5 phần, đi từ trong ra ngoài theo Clean Architecture:

```
Domain          IdempotencyRecord              (entity, bảng lưu response)
Infrastructure  IdempotencyRecordConfiguration (cấu hình EF Core, unique index)
Application     IIdempotencyService / IdempotencyService (đọc và ghi record)
API             IdempotencyFilter              (Action Filter bọc quanh endpoint)
API             OrdersController.Checkout      (gắn [ServiceFilter(typeof(IdempotencyFilter))])
```

### 6.1. Entity: `IdempotencyRecord`

File: [ShopeeMini.Domain/Entities/IdempotencyRecord.cs](ShopeeMiniBackEnd/ShopeeMini.Domain/Entities/IdempotencyRecord.cs)

```csharp
public class IdempotencyRecord : BaseEntity   // Id, CreatedAt, UpdatedAt
{
    public long UserId { get; set; }
    public string Key { get; set; } = default!;          // giá trị header Idempotency-Key
    public string RequestPath { get; set; } = default!;  // ví dụ "/api/orders/checkout"
    public int ResponseStatusCode { get; set; }          // ví dụ 201
    public string ResponseBody { get; set; } = default!; // JSON response đã serialize
}
```

**Vì sao khóa gồm 3 thành phần `(UserId, Key, RequestPath)`?**

- `UserId`: user A và user B vô tình (hoặc cố ý) dùng cùng key `"abc"` thì không đụng nhau. User B **không thể** đọc được response checkout của user A bằng cách đoán key.
- `RequestPath`: cùng một key dùng cho 2 endpoint khác nhau không bị trả nhầm response.
- `Key`: phân biệt các lần thanh toán khác nhau của cùng một user.

### 6.2. Cấu hình DB

File: [ShopeeMini.Infrastructure/Persistence/Configurations/CommerceConfigurations.cs](ShopeeMiniBackEnd/ShopeeMini.Infrastructure/Persistence/Configurations/CommerceConfigurations.cs)

```csharp
builder.ToTable("IdempotencyRecords");
builder.Property(r => r.Key).IsRequired().HasMaxLength(128);
builder.Property(r => r.RequestPath).IsRequired().HasMaxLength(256);
builder.Property(r => r.ResponseBody).HasColumnType("longtext");
builder.HasIndex(r => new { r.UserId, r.Key, r.RequestPath }).IsUnique();
```

- **Unique index** trên bộ 3 khóa là **lớp bảo vệ cuối cùng ở tầng DB**: dù code có lỗi thì DB cũng không cho lưu 2 record trùng. Index này cũng giúp câu `SELECT` tra cứu nhanh.
- `Key` tối đa 128 ký tự, đủ cho UUID (36 ký tự). Client gửi key dài hơn thì `SaveAsync` sẽ lỗi.

### 6.3. Service: `IdempotencyService`

File: [ShopeeMini.Application/Common/IdempotencyService.cs](ShopeeMiniBackEnd/ShopeeMini.Application/Common/IdempotencyService.cs)

Có 2 hàm:

- `TryGetAsync(userId, key, path)`: tìm record theo bộ 3 khóa, trả `CachedIdempotentResponse(StatusCode, Body)` hoặc `null`. Dùng `AsNoTracking()` vì chỉ đọc.
- `SaveAsync(userId, key, path, statusCode, body)`: kiểm tra chưa tồn tại thì `INSERT` một record mới.

Service được đăng ký `Scoped` trong [ShopeeMini.Application/DependencyInjection.cs](ShopeeMiniBackEnd/ShopeeMini.Application/DependencyInjection.cs), tức là dùng chung `DbContext` với request hiện tại.

### 6.4. Filter: `IdempotencyFilter` (trái tim của cơ chế)

File: [ShopeeMini.API/Filters/IdempotencyFilter.cs](ShopeeMiniBackEnd/ShopeeMini.API/Filters/IdempotencyFilter.cs)

Đây là một `IAsyncActionFilter`, chạy **bọc quanh** action của controller. Từng bước:

**Bước 1: Bắt buộc có header**

```csharp
if (!context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var keyValues)
    || string.IsNullOrWhiteSpace(keyValues))
{
    throw new BadRequestException($"Thiếu header '{HeaderName}' bắt buộc cho thao tác này.");
}
```

Thiếu header thì ném `BadRequestException`, được `GlobalExceptionMiddleware` đổi thành **HTTP 400**. Header là **bắt buộc** (không phải tùy chọn) để client không thể "quên" gửi rồi vô tình bị double charge.

**Bước 2: Lấy userId và path**

```csharp
var key = keyValues.ToString();
var path = context.HttpContext.Request.Path.ToString();
var userIdRaw = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
if (!long.TryParse(userIdRaw, out var userId)) throw new UnauthorizedAppException();
```

`userId` lấy từ JWT claim. Vì vậy filter này **chỉ dùng được cho endpoint đã `[Authorize]`**.

**Bước 3: Đã có response cũ thì trả lại luôn (short-circuit)**

```csharp
var cached = await _idempotencyService.TryGetAsync(userId, key, path, ct);
if (cached is not null)
{
    context.Result = new ContentResult
    {
        StatusCode = cached.StatusCode,
        Content = cached.Body,
        ContentType = "application/json"
    };
    return;   // KHÔNG gọi next() nên action Checkout() không chạy
}
```

Khi gán `context.Result` và `return` mà không gọi `next()`, ASP.NET Core **bỏ qua action**. `OrderService.CheckoutAsync` không được gọi, nên không trừ kho và không tạo đơn. Client nhận về **đúng status code và body như lần đầu**, ví dụ `201` kèm `orderCode` cũ.

**Bước 4: Chưa có thì chạy action thật**

```csharp
var executedContext = await next();   // chạy OrdersController.Checkout(...)
```

**Bước 5: Chỉ lưu khi thành công (2xx)**

```csharp
if (executedContext.Exception is null
    && executedContext.Result is ObjectResult { StatusCode: null or >= 200 and < 300 } objectResult)
{
    var statusCode = objectResult.StatusCode ?? 200;
    var body = JsonSerializer.Serialize(objectResult.Value,
        new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    await _idempotencyService.SaveAsync(userId, key, path, statusCode, body, ct);
}
```

- `SuccessResponse(...)` trong `ApiControllerBase` trả về `ObjectResult`, nên filter đọc được `Value` và `StatusCode`.
- Serialize bằng **camelCase**, giống format mà ASP.NET Core trả ra bình thường, để response replay **giống hệt** response gốc.
- Lỗi (400 "Giỏ hàng đang trống", 409 "không đủ tồn kho", 500...) thì `executedContext.Exception` khác null, nên **không lưu**. Client sửa giỏ hàng rồi thử lại với cùng key thì request sẽ được xử lý như mới.

### 6.5. Đăng ký và gắn vào endpoint

[ShopeeMini.API/Program.cs](ShopeeMiniBackEnd/ShopeeMini.API/Program.cs):

```csharp
builder.Services.AddScoped<IdempotencyFilter>();
```

[ShopeeMini.API/Controllers/OrdersController.cs](ShopeeMiniBackEnd/ShopeeMini.API/Controllers/OrdersController.cs):

```csharp
[HttpPost("checkout")]
[ServiceFilter(typeof(IdempotencyFilter))]
public async Task<IActionResult> Checkout(CancellationToken ct) { ... }
```

Dùng `[ServiceFilter]` thay cho `[TypeFilter]` hoặc filter global vì:

- Filter cần **DI** (`IIdempotencyService`), và `ServiceFilter` lấy instance từ DI container. Đó là lý do phải `AddScoped<IdempotencyFilter>()`.
- Chỉ gắn vào **những endpoint cần**. Gắn global sẽ bắt mọi request (kể cả `GET`) phải có header.

---

## 7. Cách gọi API từ phía client

### 7.1. Quy tắc sinh và dùng key

| Tình huống | Làm gì với key |
|---|---|
| User bấm "Thanh toán" lần đầu | Sinh key **mới** (UUID v4) và **lưu lại** (state, localStorage...) |
| Timeout, mất mạng, 5xx, không biết kết quả | Retry với **đúng key cũ** |
| User bấm "Thanh toán" 2 lần liên tiếp | Lần 2 dùng **key cũ** (hoặc disable nút khi đang gửi) |
| Đã nhận `201` thành công | **Xóa** key. Lần mua sau phải sinh key mới |
| Nhận `400` hoặc `409` (giỏ trống, hết hàng) và user sửa giỏ | Nên sinh key **mới** cho lần thanh toán mới |

❌ **Sai phổ biến:** sinh key mới **mỗi lần gửi request**, kể cả khi retry. Như vậy thì server thấy mỗi lần là một key khác, coi như không có Idempotency.

❌ **Sai phổ biến:** dùng key cố định (ví dụ `userId`). Như vậy thì lần mua thứ hai (giỏ hàng mới) sẽ nhận lại response của lần mua đầu và **không tạo đơn mới**.

### 7.2. Ví dụ với curl

```bash
KEY=$(uuidgen)   # ví dụ: 3f1c2a9e-7b4d-4e8a-9c0f-1a2b3c4d5e6f

# Lần 1: tạo đơn thật
curl -X POST https://localhost:7xxx/api/orders/checkout \
  -H "Authorization: Bearer <access_token>" \
  -H "Idempotency-Key: $KEY"
# → 201 { "success": true, "data": { "orderCode": "ORD202610031030001234", ... } }

# Lần 2: retry với CÙNG key
curl -X POST https://localhost:7xxx/api/orders/checkout \
  -H "Authorization: Bearer <access_token>" \
  -H "Idempotency-Key: $KEY"
# → 201, body Y HỆT lần 1 (cùng orderCode). Không tạo thêm đơn, không trừ kho.

# Thiếu header
curl -X POST https://localhost:7xxx/api/orders/checkout -H "Authorization: Bearer <access_token>"
# → 400 { "success": false, "message": "Thiếu header 'Idempotency-Key' bắt buộc cho thao tác này." }
```

### 7.3. Ví dụ với front-end (TypeScript / axios)

```ts
let checkoutKey: string | null = null;

async function checkout() {
  // Chỉ sinh key mới khi chưa có (lần bấm đầu tiên)
  checkoutKey ??= crypto.randomUUID();

  for (let attempt = 1; attempt <= 3; attempt++) {
    try {
      const res = await api.post("/api/orders/checkout", null, {
        headers: { "Idempotency-Key": checkoutKey },
        timeout: 10_000,
      });
      checkoutKey = null;              // thành công thì bỏ key
      return res.data;
    } catch (err: any) {
      const status = err.response?.status;
      if (status && status < 500) {    // 4xx: lỗi nghiệp vụ, không retry
        checkoutKey = null;
        throw err;
      }
      // timeout / mất mạng / 5xx: retry với CÙNG key
      await new Promise(r => setTimeout(r, 500 * attempt));
    }
  }
  throw new Error("Thanh toán chưa xác nhận được, vui lòng kiểm tra lịch sử đơn hàng.");
}
```

### 7.4. Kiểm thử trên Swagger

Action `Checkout` không khai báo tham số header, nên Swagger UI **không hiện ô nhập** `Idempotency-Key`, và gọi từ Swagger sẽ bị 400. Hãy dùng Postman hoặc curl. Nếu muốn Swagger hiện ô nhập, có thể thêm tham số vào action (filter vẫn đọc header như cũ):

```csharp
public async Task<IActionResult> Checkout(
    [FromHeader(Name = "Idempotency-Key")] string idempotencyKey, CancellationToken ct)
```

---

## 8. Áp dụng cho endpoint khác

Muốn bảo vệ thêm một endpoint (ví dụ `POST /api/cart/items`), chỉ cần **1 dòng**:

```csharp
[HttpPost("items")]
[ServiceFilter(typeof(IdempotencyFilter))]
public async Task<IActionResult> AddItem(AddCartItemDto request, CancellationToken ct) { ... }
```

Điều kiện để filter hoạt động đúng:

1. Endpoint phải **yêu cầu đăng nhập** (filter cần `ClaimTypes.NameIdentifier`).
2. Action phải trả về `ObjectResult`, tức là dùng `SuccessResponse(...)` như các controller hiện tại. Nếu trả `NoContent()` hoặc `File(...)` thì filter **không lưu** response.
3. Client phải được cập nhật để gửi header, nếu không sẽ bị 400.

> ⚠️ Với endpoint **có body** như `AddItem`, xem thêm mục 9.3 (fingerprint request).

---

## 9. Giới hạn của cách cài hiện tại và hướng cải tiến

Cách cài hiện tại xử lý tốt trường hợp phổ biến nhất: **retry tuần tự** (request 1 xong, response bị mất, client gửi lại). Tuy nhiên có vài điểm cần biết.

### 9.1. Hai request cùng key đến **đồng thời**

Luồng hiện tại là *check → chạy → lưu*, và khoảng giữa "check" với "lưu" không được khóa:

```
t0  Request A: TryGet(key) → null
t1  Request B: TryGet(key) → null          ← A chưa kịp lưu
t2  A và B đều chạy CheckoutAsync()
```

Trong `CheckoutAsync`, lệnh `UPDATE ProductSkus` của B sẽ **bị chặn bởi row lock** của transaction A cho tới khi A commit. Sau đó:

- Nếu kho **còn đủ**: B trừ kho xong, tới bước xóa `CartItems` thì các dòng đã bị A xóa, nên EF Core ném `DbUpdateConcurrencyException`. Transaction của B rollback và B nhận **500**. Kết quả không tạo đơn trùng, nhưng client nhận lỗi khó hiểu thay vì response của A.
- Nếu kho **không đủ** cho lần trừ thứ 2: B rollback và gọi `RecordFailedOrderAsync`, nên hệ thống **tạo thêm một đơn `Failed` cùng thông báo "Thanh toán thất bại"**, dù thật ra đơn của A đã thành công. User sẽ bối rối.

Ngoài ra, ở `SaveAsync`, bước kiểm tra `AnyAsync` rồi `INSERT` cũng có thể đua nhau. Khi đó unique index chặn lại và ném lỗi `Duplicate entry`, **sau khi** đơn đã commit, nên client nhận 500.

**Cách khắc phục (pattern "reserve trước"):** ghi record **trước khi** chạy logic, với trạng thái `Processing`, và để unique index làm khóa:

```csharp
// 1. Cố gắng "giữ chỗ" key: INSERT (UserId, Key, Path, Status = Processing)
//    - Thành công  → mình là request đầu tiên, tiếp tục chạy.
//    - Duplicate   → đã có request khác giữ key:
//         * record.Status == Completed  → replay response đã lưu
//         * record.Status == Processing → trả 409 "Yêu cầu đang được xử lý, vui lòng thử lại sau"
// 2. Chạy action.
// 3. Thành công → UPDATE record: Status = Completed, ResponseStatusCode, ResponseBody
//    Thất bại   → DELETE record (cho phép thử lại với cùng key)
```

Muốn chắc chắn tuyệt đối thì lưu idempotency record **trong cùng DB transaction** với việc tạo `Order`. Khi đó "đơn đã tạo" và "key đã dùng" luôn cùng commit hoặc cùng rollback.

### 9.2. Server crash giữa "commit đơn" và "lưu key"

Nếu process chết ngay sau `transaction.CommitAsync()` mà chưa kịp `SaveAsync`, lần retry sẽ chạy lại `CheckoutAsync`. Trong ShopeeMini việc này **tình cờ an toàn**, vì giỏ hàng đã bị xóa trong transaction trước, nên lần chạy lại trả **400 "Giỏ hàng đang trống"** và không tạo đơn thứ 2. Tuy vậy client nhận lỗi thay vì đơn hàng vừa tạo. Pattern ở 9.1 (lưu key cùng transaction) giải quyết luôn trường hợp này.

### 9.3. Không kiểm tra nội dung request (fingerprint)

Record chỉ khóa theo `(UserId, Key, RequestPath)`. `Checkout` không có body nên không sao. Nhưng nếu áp dụng cho `POST /api/cart/items` mà client **dùng lại key với body khác** (ví dụ SKU khác), server sẽ trả response cũ và **âm thầm bỏ qua** request mới.

**Cải tiến:** lưu thêm `RequestHash = SHA256(body)`. Gặp cùng key mà hash khác thì trả **422 Unprocessable Entity** ("Idempotency-Key đã được dùng cho một request khác"). Stripe làm đúng như vậy.

### 9.4. Không có thời hạn (TTL) và không dọn dẹp

Bảng `IdempotencyRecords` chỉ tăng, không giảm. Nên:

- Đặt TTL, ví dụ 24 giờ. Retry thực tế chỉ diễn ra trong vài giây đến vài phút.
- Thêm background job (`BackgroundService` hoặc MySQL Event) chạy định kỳ:
  ```sql
  DELETE FROM IdempotencyRecords WHERE CreatedAt < UTC_TIMESTAMP() - INTERVAL 1 DAY;
  ```
- Cân nhắc thêm index trên `CreatedAt` để câu xóa chạy nhanh.

### 9.5. Không lưu response lỗi

Đây là **lựa chọn có chủ đích**: lỗi 4xx/5xx không được cache, để user sửa giỏ rồi thử lại. Hệ quả là nếu client retry một request bị `409` (hết hàng) với cùng key, server sẽ chạy lại và **tạo thêm một đơn `Failed`** cùng thông báo. Client nên **không retry khi nhận 4xx** (như ví dụ ở mục 7.3).

### 9.6. Có thể dùng Redis thay cho MySQL không?

Có, và Redis là lựa chọn phổ biến khi hệ thống lớn: `SET key value NX EX 86400` vừa nguyên tử (giải quyết 9.1) vừa có TTL sẵn (giải quyết 9.4). ShopeeMini hiện chỉ có một MySQL instance, nên dùng bảng DB với unique index là đơn giản và đủ dùng.

---

## 10. Tóm tắt

- **Idempotency** = gọi 1 lần hay N lần thì kết quả trên hệ thống vẫn như nhau.
- **Cần** cho endpoint có side effect, không tự nhiên idempotent, và hậu quả lớn (tiền, kho). Trong ShopeeMini đó là `POST /api/orders/checkout`.
- **Không dùng** thì retry do mạng chập chờn hoặc double-click sẽ gây **tính tiền 2 lần, trừ kho 2 lần**. Atomic Update tồn kho không ngăn được chuyện này.
- **ShopeeMini cài** bằng `IdempotencyFilter` (Action Filter) cùng `IdempotencyService` và bảng `IdempotencyRecords` (unique index `UserId + Key + RequestPath`): lần đầu chạy thật và lưu response, các lần sau trả lại response đã lưu.
- **Client** sinh UUID cho mỗi lần thanh toán, retry thì **giữ nguyên key**, thành công thì bỏ key.
- **Hướng cải tiến:** reserve key trước (chống request đồng thời), lưu key cùng transaction, thêm hash body, thêm TTL và job dọn dẹp.
