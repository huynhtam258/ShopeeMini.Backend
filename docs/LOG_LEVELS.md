# Log Level trong ShopeeMini

Tài liệu này giải thích các mức log (log level) mà backend ShopeeMini dùng qua **Serilog** và `ILogger<T>` của ASP.NET Core, kèm cách chọn level cho đúng.

## 1. Log level là gì?

Log level cho biết **mức độ nghiêm trọng** của một dòng log. Nhờ có level, ta có thể:

- **Lọc khi ghi:** chỉ ghi các log từ một mức trở lên. Ví dụ Production chỉ ghi từ `Information` trở lên, bỏ qua `Debug`.
- **Lọc khi đọc:** khi có sự cố, chỉ cần tìm các dòng `[ERR]` hoặc `[WRN]` thay vì đọc hàng nghìn dòng.
- **Cảnh báo:** dựa vào level để quyết định khi nào báo động. Ví dụ `Error` hay `Fatal` thì gửi thông báo cho dev.

Các level xếp theo thứ tự tăng dần. Khi đặt mức tối thiểu là X, mọi log **từ X trở lên** sẽ được ghi:

```
Verbose < Debug < Information < Warning < Error < Fatal
```

## 2. Bảng tên các level

Serilog và `Microsoft.Extensions.Logging` (`ILogger`) gọi tên hơi khác nhau, nhưng tương ứng một-một với nhau:

| Serilog       | ILogger (.NET) | Ký hiệu trong file log | Mức độ |
|---------------|----------------|------------------------|--------|
| `Verbose`     | `Trace`        | `VRB`                  | Thấp nhất, chi tiết nhất |
| `Debug`       | `Debug`        | `DBG`                  | |
| `Information` | `Information`  | `INF`                  | |
| `Warning`     | `Warning`      | `WRN`                  | |
| `Error`       | `Error`        | `ERR`                  | |
| `Fatal`       | `Critical`     | `FTL`                  | Cao nhất |

Trong code ShopeeMini ta dùng `ILogger<T>` (`_logger.LogWarning(...)`, ...). Serilog nhận các log đó và ghi ra Console và file `Logs/log-YYYYMMDD.txt`.

---

## 3. Giải thích từng level

### 3.1. Verbose / Trace (`VRB`)

**Ý nghĩa:** chi tiết nhất. Dùng để theo dõi từng bước nhỏ bên trong một hàm.

**Khi nào dùng:**
- Vào và ra một hàm, giá trị của từng biến trong vòng lặp.
- Dò một lỗi khó tái hiện, cần thấy luồng chạy từng dòng.

**Ví dụ:**
```csharp
_logger.LogTrace("Bắt đầu tính giá SKU {SkuId}, giá gốc {Price}", sku.Id, sku.Price);
```

**Lưu ý:** sinh ra rất nhiều log, làm chậm ứng dụng và có thể lộ dữ liệu. **Không bật trên Production.** Bình thường cũng nên tắt ở môi trường Dev.

---

### 3.2. Debug (`DBG`)

**Ý nghĩa:** thông tin dành cho dev khi phát triển và debug. Ít chi tiết hơn Verbose.

**Khi nào dùng:**
- Kết quả trung gian của một nghiệp vụ, ví dụ giá sau khi áp discount hay số SKU tìm thấy.
- Nhánh điều kiện mà code đã đi vào.

**Ví dụ:**
```csharp
_logger.LogDebug("Giỏ hàng {CartId} có {Count} item, tổng tạm tính {Subtotal}",
    cart.Id, cart.Items.Count, subtotal);
```

**Lưu ý:** bật ở Development và tắt ở Production.

---

### 3.3. Information (`INF`)

**Ý nghĩa:** các sự kiện **bình thường nhưng quan trọng** của hệ thống. Đọc log `INF` sẽ biết hệ thống đã làm gì.

**Khi nào dùng:**
- Ứng dụng khởi động hoặc dừng.
- Mỗi HTTP request đã xử lý xong. Trong project, `UseSerilogRequestLogging()` tự ghi dòng này.
- Sự kiện nghiệp vụ chính: user đăng ký hoặc đăng nhập thành công, đặt đơn thành công, admin đổi trạng thái đơn, admin tạo hoặc sửa sản phẩm.

**Ví dụ:**
```csharp
_logger.LogInformation("User {UserId} checkout thành công đơn {OrderId}, tổng {Total}",
    userId, order.Id, order.TotalAmount);
```

**Lưu ý:** đây là **mức mặc định trên Production**. Chỉ ghi những sự kiện có ý nghĩa, đừng ghi trong vòng lặp.

---

### 3.4. Warning (`WRN`)

**Ý nghĩa:** có điều **bất thường hoặc không mong muốn** xảy ra, nhưng hệ thống **vẫn chạy được** và request vẫn được xử lý, dù có thể trả lỗi cho client.

**Khi nào dùng:**
- Lỗi nghiệp vụ hoặc lỗi do client: dữ liệu không hợp lệ (400), chưa đăng nhập (401), không có quyền (403), không tìm thấy (404), xung đột như hết hàng (409). Trong project, `GlobalExceptionMiddleware` đang ghi các lỗi này ở mức `Warning`.
- Đăng nhập sai mật khẩu nhiều lần, refresh token không hợp lệ.
- Request bị chặn vì rate limit.
- Gặp một request trùng `Idempotency-Key` khi checkout.
- Dùng giá trị mặc định vì thiếu cấu hình.

**Ví dụ:**
```csharp
_logger.LogWarning("Checkout thất bại: SKU {SkuId} không đủ hàng (còn {Stock}, cần {Qty})",
    skuId, sku.Stock, qty);
```

**Lưu ý:** một vài dòng `WRN` là bình thường. Nếu `WRN` tăng đột biến (ví dụ rất nhiều 401) thì nên xem xét, vì có thể đang bị dò mật khẩu hoặc client đang gọi sai API.

---

### 3.5. Error (`ERR`)

**Ý nghĩa:** một **thao tác đã thất bại** do lỗi mà hệ thống không lường trước. Request hoặc tác vụ hiện tại không hoàn thành, nhưng ứng dụng vẫn sống và xử lý được các request khác.

**Khi nào dùng:**
- Exception không xử lý được dẫn tới lỗi 500. Trong project, `GlobalExceptionMiddleware` ghi các lỗi này ở mức `Error` kèm stack trace.
- Mất kết nối database giữa chừng, lỗi khi lưu DB (`DbUpdateException`).
- Không ghi được file hóa đơn vào `InvoiceStorage`.
- Gọi dịch vụ bên ngoài thất bại.

**Ví dụ:**
```csharp
_logger.LogError(ex, "Không thể tạo hóa đơn cho đơn {OrderId}", orderId);
```

**Lưu ý:**
- **Luôn truyền đối tượng `exception`** làm tham số đầu tiên để log có stack trace.
- Mỗi dòng `ERR` nghĩa là có bug hoặc sự cố hạ tầng, **cần được điều tra**. Dùng `CorrelationId` trong dòng log để tìm lại toàn bộ request.

---

### 3.6. Fatal / Critical (`FTL`)

**Ý nghĩa:** lỗi **nghiêm trọng nhất**. Ứng dụng không thể tiếp tục chạy, hoặc một chức năng cốt lõi sập hoàn toàn.

**Khi nào dùng:**
- Ứng dụng crash khi khởi động: sai connection string, thiếu `Jwt:SigningKey`, migration lỗi.
- Hết dung lượng đĩa, hết bộ nhớ.
- Mất hoàn toàn kết nối tới database và không thể phục hồi.

**Ví dụ:**
```csharp
try
{
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Ứng dụng dừng đột ngột");
}
finally
{
    Log.CloseAndFlush();
}
```

**Lưu ý:** rất hiếm gặp. Khi có `FTL` thì cần xử lý **ngay lập tức** và thường phải có cảnh báo tự động.

---

## 4. Cách chọn level nhanh

Trả lời lần lượt các câu hỏi sau:

1. **Ứng dụng có còn chạy được không?** Không → `Fatal`.
2. **Thao tác hiện tại có thất bại vì lỗi ngoài dự kiến (bug, hạ tầng) không?** Có → `Error`.
3. **Có gì bất thường không, dù hệ thống vẫn xử lý đúng?** (input sai, không có quyền, hết hàng, ...) Có → `Warning`.
4. **Đây có phải sự kiện nghiệp vụ quan trọng cần lưu lại không?** Có → `Information`.
5. **Thông tin này chỉ hữu ích khi debug?** → `Debug`. Nếu cần theo dõi chi tiết từng bước → `Verbose`.

## 5. Nguyên tắc chung khi ghi log

- **Dùng structured logging:** viết `"Đơn {OrderId}"` kèm tham số, **không** nối chuỗi kiểu `$"Đơn {orderId}"`. Khi đó Serilog lưu `OrderId` thành một trường riêng, sau này tìm kiếm và lọc được.
- **Không ghi dữ liệu nhạy cảm:** mật khẩu, access token hay refresh token, `Jwt:SigningKey`, thông tin thanh toán.
- **Không ghi một lỗi hai lần:** nếu exception được ném tiếp lên trên thì để `GlobalExceptionMiddleware` ghi, không cần `LogError` rồi `throw` lại.
- **Tận dụng `CorrelationId`:** mọi log trong cùng một request đều có chung ID này (do `CorrelationIdMiddleware` gắn vào), nên không cần tự thêm vào message.

## 6. Cấu hình level trong ShopeeMini

Cấu hình hiện tại ở `ShopeeMiniBackEnd/ShopeeMini.API/Program.cs`:

```csharp
.MinimumLevel.Information()                                              // mặc định: từ INF trở lên
.MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)    // log framework: từ WRN trở lên
.MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
```

Mức khuyến nghị cho từng môi trường:

| Môi trường  | Mức tối thiểu cho code của app | Framework (AspNetCore, EF Core) |
|-------------|--------------------------------|---------------------------------|
| Development | `Debug`                        | `Warning` (nâng lên `Information` khi cần xem SQL do EF sinh ra) |
| Production  | `Information`                  | `Warning`                       |

> **Lưu ý:** hiện tại level đang được **viết cứng trong code**. Hai giá trị `Serilog:MinimumLevel` và `Logging:LogLevel` trong `appsettings.json` **không có tác dụng** vì `Program.cs` không gọi `ReadFrom.Configuration(...)`. Muốn đổi level theo môi trường mà không phải build lại, cần chuyển cấu hình Serilog sang đọc từ `appsettings.{Environment}.json`.
