# Đặc tả API — ShopeeMini Backend

Đặc tả mọi endpoint REST của backend ShopeeMini, viết từ source code hiện tại (controller, DTO, validator, service). Kiến trúc tổng thể xem ở [architecture.md](architecture.md), cấu trúc dữ liệu ở [database-schema.md](database-schema.md).

---

## 1. Quy ước chung

### 1.1. Base URL

| Môi trường | URL |
|---|---|
| Local (profile `http`) | `http://localhost:5019` |
| Local (profile `https`) | `https://localhost:7031` |
| Swagger UI (chỉ Development) | `{base}/swagger` |

Mọi endpoint bắt đầu bằng `/api`. Body request và response đều là `application/json`.

### 1.2. Xác thực

Endpoint cần đăng nhập yêu cầu header:

```
Authorization: Bearer <accessToken>
```

`accessToken` lấy từ `POST /api/auth/login`, `register` hoặc `refresh`. Access token sống 15 phút, refresh token 7 ngày (cấu hình được). Hết hạn access token thì gọi `POST /api/auth/refresh`.

Ký hiệu quyền dùng trong tài liệu:

| Ký hiệu | Ý nghĩa |
|---|---|
| 🌐 Public | Không cần token |
| 🔑 Auth | Cần token, role bất kỳ |
| 👤 User | Cần token role `User` (Admin gọi sẽ bị 403) |
| 🛠 Admin | Cần token role `Admin` (User gọi sẽ bị 403) |

### 1.3. Header khác

| Header | Hướng | Mô tả |
|---|---|---|
| `X-Correlation-Id` | Request (tùy chọn) / Response (luôn có) | ID để truy vết log. Client gửi lên thì server dùng lại, không gửi thì server tự sinh (GUID 32 ký tự hex) |
| `Idempotency-Key` | Request | **Bắt buộc** cho `POST /api/orders/checkout`. Tối đa 128 ký tự, khuyến nghị UUID v4 |

### 1.4. Envelope response

Mọi response thành công, và mọi lỗi do ứng dụng ném ra, đều có dạng:

```json
{
  "success": true,
  "data": { },
  "message": "Thao tác thành công",
  "errors": null
}
```

| Trường | Kiểu | Mô tả |
|---|---|---|
| `success` | boolean | `true` nếu thành công |
| `data` | object \| array \| null | Dữ liệu trả về; `null` với các thao tác không trả dữ liệu và với lỗi |
| `message` | string | Thông báo tiếng Việt |
| `errors` | object \| null | Chỉ có khi lỗi validate: `{ "<TênTrường>": ["lỗi 1", "lỗi 2"] }` |

Trong tài liệu, phần **Response** chỉ mô tả nội dung của `data`.

Ví dụ lỗi validate (FluentValidation):

```json
{
  "success": false,
  "data": null,
  "message": "Dữ liệu đầu vào không hợp lệ.",
  "errors": {
    "Email": ["'Email' is not a valid email address."],
    "Skus[0].Price": ["'Price' must be greater than '0'."]
  }
}
```

Key trong `errors` giữ nguyên PascalCase theo tên property C# (không đổi sang camelCase). Nội dung lỗi là message mặc định của FluentValidation, ngôn ngữ phụ thuộc culture của server, trừ message tùy biến `"Sản phẩm phải có ít nhất 1 SKU."`.

### 1.5. Mã lỗi

| HTTP | Khi nào | `message` |
|---|---|---|
| 400 | Dữ liệu không hợp lệ (FluentValidation) | `Dữ liệu đầu vào không hợp lệ.` (kèm `errors`) |
| 400 | Lỗi nghiệp vụ (`BadRequestException`) | Theo từng endpoint |
| 401 | Sai thông tin đăng nhập, refresh token không hợp lệ | Theo từng endpoint |
| 403 | Không sở hữu tài nguyên, tài khoản bị khóa | Theo từng endpoint |
| 404 | Không tìm thấy, hoặc tài nguyên không thuộc về user | `{Thực thể} với id '{id}' không tồn tại.` |
| 409 | Xung đột: email trùng, hết hàng, danh mục còn sản phẩm | Theo từng endpoint |
| 429 | Vượt rate limit | `Bạn đã gửi quá nhiều yêu cầu. Vui lòng thử lại sau ít phút.` |
| 500 | Lỗi không lường trước | `Đã xảy ra lỗi hệ thống. Vui lòng thử lại sau.` |

### 1.6. Các response không theo envelope

Một số response do framework tạo ra trước khi vào controller, nên **không** có dạng envelope ở mục 1.4. Client cần xử lý riêng:

| Tình huống | HTTP | Body |
|---|---|---|
| Thiếu, sai hoặc hết hạn access token | 401 | Rỗng (có header `WWW-Authenticate: Bearer ...`) |
| Sai role (ví dụ User gọi `/api/admin/*`) | 403 | Rỗng |
| JSON sai cú pháp, hoặc thiếu/rỗng một trường `string` bắt buộc (không nullable) trong DTO | 400 | `ValidationProblemDetails` của ASP.NET Core: `{ "type", "title", "status", "errors", "traceId" }` |
| Vượt rate limit | 429 | Envelope nhưng key viết **PascalCase**: `{ "Success": false, "Data": null, "Message": "...", "Errors": null }` |

> Trường `string` không nullable (ví dụ `email`, `password`, `name`) được ASP.NET Core coi là bắt buộc **trước khi** FluentValidation chạy. Vì vậy thiếu hoặc để rỗng các trường này sẽ trả `ValidationProblemDetails`. Các lỗi còn lại (độ dài, định dạng email, giá trị số) mới trả envelope ở mục 1.4.

### 1.7. Phân trang

Endpoint có phân trang nhận query string:

| Tham số | Kiểu | Mặc định | Ghi chú |
|---|---|---|---|
| `page` | int | 1 | Bắt đầu từ 1. Giá trị < 1 được xử lý như 1 khi truy vấn, nhưng `page` trong response giữ nguyên giá trị gửi lên |
| `pageSize` | int | 20 | ≤ 0 thành 20; > 100 thành 100 |
| `fromDate` | datetime (ISO 8601) | — | Lọc `CreatedAt >= fromDate` |
| `toDate` | datetime (ISO 8601) | — | Lọc `CreatedAt <= toDate` |

`data` có dạng `PagedResult<T>`:

```json
{
  "items": [ ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 57,
  "totalPages": 3
}
```

Sắp xếp mặc định: `CreatedAt` giảm dần (mới nhất trước).

### 1.8. Kiểu dữ liệu

- **Thời gian:** mọi giá trị lưu và trả về theo **UTC**. Client nên gửi chuỗi ISO 8601 có hậu tố `Z` (ví dụ `2026-10-07T00:00:00Z`) cho `fromDate`, `toDate`, `startAt`, `endAt`.
- **Tiền:** `decimal`, 2 chữ số thập phân, đơn vị VNĐ.
- **Enum:** trả về dạng chuỗi (`"Admin"`, `"Paid"`, `"OrderPlacedSuccess"`...).
- **ID:** số nguyên 64-bit.

### 1.9. Rate limiting

Fixed window theo IP: mặc định **100 request / 60 giây / IP**, không có hàng đợi. Áp dụng cho mọi endpoint.

---

## 2. Danh sách endpoint

| # | Method | Path | Quyền | Mô tả |
|---|---|---|---|---|
| 1 | POST | `/api/auth/register` | 🌐 | Đăng ký tài khoản User |
| 2 | POST | `/api/auth/login` | 🌐 | Đăng nhập |
| 3 | POST | `/api/auth/refresh` | 🌐 | Cấp lại token (rotation) |
| 4 | POST | `/api/auth/logout` | 🌐 | Thu hồi refresh token |
| 5 | GET | `/api/users/me` | 🔑 | Hồ sơ của tôi |
| 6 | PUT | `/api/users/me/password` | 🔑 | Đổi mật khẩu |
| 7 | GET | `/api/categories` | 🌐 | Danh sách danh mục |
| 8 | POST | `/api/admin/categories` | 🛠 | Tạo danh mục |
| 9 | PUT | `/api/admin/categories/{id}` | 🛠 | Sửa danh mục |
| 10 | DELETE | `/api/admin/categories/{id}` | 🛠 | Xóa danh mục |
| 11 | GET | `/api/products` | 🌐 | Danh sách sản phẩm public |
| 12 | GET | `/api/products/{id}` | 🌐 | Chi tiết sản phẩm public |
| 13 | GET | `/api/admin/products` | 🛠 | Danh sách sản phẩm của shop tôi |
| 14 | GET | `/api/admin/products/{id}` | 🛠 | Chi tiết sản phẩm của shop tôi |
| 15 | POST | `/api/admin/products` | 🛠 | Tạo sản phẩm (SPU + SKU) |
| 16 | PUT | `/api/admin/products/{id}` | 🛠 | Sửa thông tin SPU |
| 17 | PUT | `/api/admin/products/{id}/status` | 🛠 | Ẩn/hiện sản phẩm |
| 18 | POST | `/api/admin/products/{id}/skus` | 🛠 | Thêm SKU |
| 19 | PUT | `/api/admin/products/skus/{skuId}/stock` | 🛠 | Cập nhật tồn kho SKU |
| 20 | PUT | `/api/admin/products/skus/{skuId}/price` | 🛠 | Cập nhật giá SKU |
| 21 | GET | `/api/admin/discounts` | 🛠 | Danh sách khuyến mãi của shop tôi |
| 22 | POST | `/api/admin/discounts` | 🛠 | Tạo khuyến mãi cho SKU |
| 23 | DELETE | `/api/admin/discounts/{id}` | 🛠 | Hủy (deactivate) khuyến mãi |
| 24 | GET | `/api/cart` | 👤 | Xem giỏ hàng |
| 25 | POST | `/api/cart/items` | 👤 | Thêm SKU vào giỏ |
| 26 | PUT | `/api/cart/items/{cartItemId}` | 👤 | Sửa số lượng |
| 27 | DELETE | `/api/cart/items/{cartItemId}` | 👤 | Xóa khỏi giỏ |
| 28 | POST | `/api/orders/checkout` | 👤 | Thanh toán giỏ hàng (cần `Idempotency-Key`) |
| 29 | POST | `/api/orders/checkout-no-idempotency` | 👤 | ⚠️ Chỉ để demo: checkout không có idempotency |
| 30 | GET | `/api/orders` | 👤 | Đơn hàng của tôi |
| 31 | GET | `/api/orders/{id}` | 👤 | Chi tiết đơn hàng |
| 32 | GET | `/api/admin/orders` | 🛠 | Dòng đơn hàng thuộc shop tôi |
| 33 | GET | `/api/notifications` | 🔑 | Thông báo của tôi |
| 34 | PUT | `/api/notifications/{id}/read` | 🔑 | Đánh dấu đã đọc |
| 35 | DELETE | `/api/notifications/{id}` | 🔑 | Xóa thông báo |

Tham số `{id}`, `{skuId}`, `{cartItemId}` có ràng buộc route `:long`. Giá trị không phải số sẽ không khớp route và trả 404 (body rỗng).

---

## 3. Auth — `/api/auth`

### 3.1. `POST /api/auth/register` 🌐

Đăng ký tài khoản mới với role `User` và trả token luôn (không cần login lại).

**Body** — `RegisterRequestDto`

| Trường | Kiểu | Bắt buộc | Ràng buộc |
|---|---|---|---|
| `email` | string | ✅ | Email hợp lệ, ≤ 256 ký tự, chưa được dùng |
| `password` | string | ✅ | 6–100 ký tự |
| `fullName` | string | ✅ | ≤ 200 ký tự |
| `phone` | string \| null | | ≤ 20 ký tự |

```json
{ "email": "buyer@example.com", "password": "secret123", "fullName": "Nguyễn Văn A", "phone": "0901234567" }
```

**Response 201** `"Đăng ký thành công"` — `AuthResultDto`

```json
{
  "userId": 12,
  "email": "buyer@example.com",
  "fullName": "Nguyễn Văn A",
  "role": "User",
  "accessToken": "eyJhbGciOiJIUzI1NiIs...",
  "accessTokenExpiresAt": "2026-10-07T03:15:00Z",
  "refreshToken": "q1Vb0n...==",
  "refreshTokenExpiresAt": "2026-10-14T03:00:00Z"
}
```

**Lỗi**

| HTTP | message |
|---|---|
| 400 | Validate |
| 409 | `Email đã được sử dụng.` |

---

### 3.2. `POST /api/auth/login` 🌐

**Body** — `LoginRequestDto`

| Trường | Kiểu | Bắt buộc | Ràng buộc |
|---|---|---|---|
| `email` | string | ✅ | Email hợp lệ |
| `password` | string | ✅ | Không rỗng |

**Response 200** `"Đăng nhập thành công"` — `AuthResultDto` (như 3.1)

**Lỗi**

| HTTP | message |
|---|---|
| 400 | Validate |
| 401 | `Email hoặc mật khẩu không đúng.` |
| 403 | `Tài khoản đã bị khóa.` (`IsActive = false`; chỉ báo khi mật khẩu đúng) |

Mỗi lần login tạo thêm một refresh token mới; các token cũ vẫn hợp lệ tới khi hết hạn hoặc bị thu hồi.

---

### 3.3. `POST /api/auth/refresh` 🌐

Đổi refresh token lấy cặp token mới. Token cũ bị thu hồi ngay (`RevokedAt`, `ReplacedByToken`), nên **mỗi refresh token chỉ dùng được 1 lần**.

**Body** — `RefreshTokenRequestDto`

| Trường | Kiểu | Bắt buộc |
|---|---|---|
| `refreshToken` | string | ✅ |

**Response 200** `"Làm mới token thành công"` — `AuthResultDto`

**Lỗi**

| HTTP | message |
|---|---|
| 400 | Validate |
| 401 | `Refresh token không hợp lệ hoặc đã hết hạn.` |

---

### 3.4. `POST /api/auth/logout` 🌐

Thu hồi refresh token được gửi lên. Access token hiện tại **không** bị vô hiệu hóa (vẫn dùng được tới khi hết hạn), nên client cần tự xóa.

**Body** — `RefreshTokenRequestDto` (như 3.3)

**Response 200** `"Đăng xuất thành công"`, `data: null`. Luôn trả 200 kể cả khi token không tồn tại hoặc đã bị thu hồi.

---

## 4. Users — `/api/users`

### 4.1. `GET /api/users/me` 🔑

**Response 200** — `UserProfileDto`

```json
{
  "id": 12,
  "email": "buyer@example.com",
  "fullName": "Nguyễn Văn A",
  "phone": "0901234567",
  "role": "User",
  "createdAt": "2026-10-01T08:00:00Z"
}
```

**Lỗi:** 404 `Người dùng với id '{id}' không tồn tại.`

---

### 4.2. `PUT /api/users/me/password` 🔑

**Body** — `ChangePasswordRequestDto`

| Trường | Kiểu | Bắt buộc | Ràng buộc |
|---|---|---|---|
| `currentPassword` | string | ✅ | Không rỗng |
| `newPassword` | string | ✅ | 6–100 ký tự |

**Response 200** `"Đổi mật khẩu thành công"`, `data: null`

**Lỗi**

| HTTP | message |
|---|---|
| 400 | Validate |
| 400 | `Mật khẩu hiện tại không đúng.` |
| 404 | `Người dùng với id '{id}' không tồn tại.` |

Đổi mật khẩu **không** thu hồi các refresh token đang có.

---

## 5. Categories

### 5.1. `GET /api/categories` 🌐

Trả toàn bộ danh mục (danh sách phẳng, **không phân trang**), sắp theo `name` tăng dần. Client tự dựng cây dựa vào `parentCategoryId`.

**Response 200** — `CategoryDto[]`

```json
[
  { "id": 1, "name": "Điện thoại & Phụ kiện", "parentCategoryId": null, "createdAt": "2026-09-30T03:30:00Z" },
  { "id": 5, "name": "Ốp lưng", "parentCategoryId": 1, "createdAt": "2026-10-01T02:00:00Z" }
]
```

---

### 5.2. `POST /api/admin/categories` 🛠

**Body** — `UpsertCategoryDto`

| Trường | Kiểu | Bắt buộc | Ràng buộc |
|---|---|---|---|
| `name` | string | ✅ | ≤ 150 ký tự |
| `parentCategoryId` | long \| null | | Phải tồn tại nếu có |

**Response 201** `"Tạo danh mục thành công"` — `CategoryDto`

**Lỗi**

| HTTP | message |
|---|---|
| 400 | Validate |
| 404 | `Danh mục cha với id '{id}' không tồn tại.` |

Danh mục là dữ liệu dùng chung của toàn hệ thống, không thuộc riêng shop nào.

---

### 5.3. `PUT /api/admin/categories/{id}` 🛠

**Body** — `UpsertCategoryDto` (như 5.2)

**Response 200** `"Cập nhật danh mục thành công"` — `CategoryDto`

**Lỗi**

| HTTP | message |
|---|---|
| 400 | Validate |
| 404 | `Danh mục với id '{id}' không tồn tại.` |
| 500 | `parentCategoryId` không tồn tại (lỗi FK; endpoint này chưa kiểm tra trước) |

---

### 5.4. `DELETE /api/admin/categories/{id}` 🛠

**Response 200** `"Xóa danh mục thành công"`, `data: null`

**Lỗi**

| HTTP | message |
|---|---|
| 404 | `Danh mục với id '{id}' không tồn tại.` |
| 409 | `Không thể xóa danh mục đang có sản phẩm.` |
| 500 | Danh mục còn danh mục con (FK `Restrict`) |

---

## 6. Products (public) — `/api/products`

### 6.1. `GET /api/products` 🌐

Danh sách sản phẩm đang public (`IsPublic = true`), có phân trang.

**Query** — `ProductQuery`: các tham số phân trang ở mục 1.7, thêm:

| Tham số | Kiểu | Mô tả |
|---|---|---|
| `categoryId` | long | Lọc đúng danh mục này (không gồm danh mục con) |
| `search` | string | Tìm theo tên sản phẩm (`LIKE %search%`) |

Ví dụ: `GET /api/products?categoryId=1&search=iphone&page=1&pageSize=12`

**Response 200** — `PagedResult<ProductListItemDto>`

| Trường | Kiểu | Mô tả |
|---|---|---|
| `id` | long | |
| `name` | string | |
| `categoryName` | string | |
| `isPublic` | bool | Luôn `true` ở endpoint này |
| `minPrice` | decimal | Giá gốc thấp nhất trong các SKU |
| `maxEffectivePrice` | decimal | Giá sau giảm cao nhất trong các SKU |
| `minEffectivePrice` | decimal | Giá sau giảm thấp nhất trong các SKU |
| `hasActiveDiscount` | bool | Có ít nhất 1 SKU đang được giảm giá |
| `createdAt` | datetime | |

Giá sau giảm ở danh sách được MySQL tính trực tiếp, không làm tròn ở app, nên có thể lệch vài đồng lẻ so với `effectivePrice` ở trang chi tiết.

---

### 6.2. `GET /api/products/{id}` 🌐

Chi tiết sản phẩm kèm toàn bộ SKU và giá sau giảm hiện tại.

**Response 200** — `ProductDetailDto`

```json
{
  "id": 7,
  "shopId": 1,
  "shopName": "ShopeeMini Official Store",
  "categoryId": 2,
  "categoryName": "Thời trang",
  "name": "Áo thun basic",
  "description": "Cotton 100%",
  "isPublic": true,
  "createdAt": "2026-09-30T05:00:00Z",
  "skus": [
    { "id": 15, "skuCode": "TEE-WHITE-M", "variantName": "Trắng / M", "price": 150000.00, "effectivePrice": 120000.00, "activeDiscountPercent": 20.00, "stockQuantity": 42 },
    { "id": 16, "skuCode": "TEE-BLACK-L", "variantName": "Đen / L", "price": 150000.00, "effectivePrice": 150000.00, "activeDiscountPercent": 0, "stockQuantity": 0 }
  ]
}
```

`SkuDto`:

| Trường | Kiểu | Mô tả |
|---|---|---|
| `id` | long | |
| `skuCode` | string | Mã SKU, duy nhất toàn hệ thống |
| `variantName` | string | Tên biến thể |
| `price` | decimal | Giá gốc |
| `effectivePrice` | decimal | Giá sau giảm, làm tròn 2 chữ số |
| `activeDiscountPercent` | decimal | % giảm đang áp dụng (lớn nhất nếu có nhiều), 0 nếu không có |
| `stockQuantity` | int | Tồn kho hiện tại |

**Lỗi:** 404 `Sản phẩm với id '{id}' không tồn tại.` (trả cả khi sản phẩm tồn tại nhưng đang ẩn)

---

## 7. Admin — Products — `/api/admin/products`

Mọi endpoint chỉ thao tác trên sản phẩm thuộc **shop của admin đang đăng nhập**. Nếu admin chưa có shop, `GET /api/admin/products` và `POST /api/admin/products` sẽ tự tạo shop tên `"{FullName}'s Shop"`.

Lỗi quyền sở hữu dùng chung cho mục này:

| HTTP | message |
|---|---|
| 403 | `Sản phẩm không thuộc shop của bạn.` |
| 403 | `SKU không thuộc shop của bạn.` |
| 404 | `Sản phẩm với id '{id}' không tồn tại.` |
| 404 | `SKU với id '{id}' không tồn tại.` |

### 7.1. `GET /api/admin/products` 🛠

Như 6.1 nhưng chỉ lấy sản phẩm của shop mình, **gồm cả sản phẩm đang ẩn**. Cùng tham số query (`categoryId`, `search`, phân trang). `isPublic` trong mỗi item phản ánh trạng thái thật. Sản phẩm không có SKU trả giá 0.

**Response 200** — `PagedResult<ProductListItemDto>`

---

### 7.2. `GET /api/admin/products/{id}` 🛠

**Response 200** — `ProductDetailDto` (như 6.2, kể cả sản phẩm đang ẩn)

---

### 7.3. `POST /api/admin/products` 🛠

Tạo SPU cùng danh sách SKU. Sản phẩm mới luôn ở trạng thái **ẩn** (`isPublic = false`); gọi 7.5 để hiển thị.

**Body** — `CreateProductDto`

| Trường | Kiểu | Bắt buộc | Ràng buộc |
|---|---|---|---|
| `categoryId` | long | ✅ | > 0, phải tồn tại |
| `name` | string | ✅ | ≤ 250 ký tự |
| `description` | string \| null | | ≤ 4000 ký tự (giới hạn ở DB) |
| `skus` | `CreateSkuDto[]` | ✅ | Ít nhất 1 phần tử |

`CreateSkuDto`:

| Trường | Kiểu | Bắt buộc | Ràng buộc |
|---|---|---|---|
| `skuCode` | string | ✅ | ≤ 64 ký tự, **duy nhất toàn hệ thống** |
| `variantName` | string | ✅ | ≤ 150 ký tự |
| `price` | decimal | ✅ | > 0 |
| `stockQuantity` | int | ✅ | ≥ 0 |

```json
{
  "categoryId": 2,
  "name": "Áo thun basic",
  "description": "Cotton 100%",
  "skus": [
    { "skuCode": "TEE-WHITE-M", "variantName": "Trắng / M", "price": 150000, "stockQuantity": 50 },
    { "skuCode": "TEE-BLACK-L", "variantName": "Đen / L", "price": 150000, "stockQuantity": 30 }
  ]
}
```

**Response 201** `"Tạo sản phẩm thành công"` — `ProductDetailDto`

**Lỗi**

| HTTP | message |
|---|---|
| 400 | Validate (`Sản phẩm phải có ít nhất 1 SKU.` nếu `skus` rỗng) |
| 404 | `Danh mục với id '{id}' không tồn tại.` |
| 500 | `skuCode` bị trùng (unique index; chưa map sang 409) |

---

### 7.4. `PUT /api/admin/products/{id}` 🛠

Sửa thông tin SPU. Không sửa SKU ở đây.

**Body** — `UpdateProductDto`

| Trường | Kiểu | Bắt buộc | Ràng buộc |
|---|---|---|---|
| `categoryId` | long | ✅ | > 0, phải tồn tại |
| `name` | string | ✅ | ≤ 250 ký tự |
| `description` | string \| null | | ≤ 4000 ký tự |

**Response 200** `"Cập nhật sản phẩm thành công"` — `ProductDetailDto`

**Lỗi:** 400, 403, 404 (sản phẩm hoặc danh mục)

---

### 7.5. `PUT /api/admin/products/{id}/status` 🛠

Ẩn hoặc hiện sản phẩm.

**Body** — `UpdateProductStatusDto`

| Trường | Kiểu | Bắt buộc |
|---|---|---|
| `isPublic` | bool | ✅ |

> ⚠️ DTO không có validator. Nếu thiếu `isPublic`, giá trị mặc định là `false`, tức là sản phẩm sẽ bị **ẩn**.

**Khi ẩn (`isPublic: false`)**, trong cùng một transaction:
- Xóa mọi cart item chứa SKU của sản phẩm này, ở giỏ hàng của **mọi** user.
- Mỗi user bị ảnh hưởng nhận 1 thông báo `ProductHiddenRemovedFromCart`.

Nếu trạng thái không đổi, endpoint không làm gì nhưng vẫn trả 200.

**Response 200** `"Đã hiển thị sản phẩm"` hoặc `"Đã ẩn sản phẩm"`, `data: null`

**Lỗi:** 403, 404

---

### 7.6. `POST /api/admin/products/{id}/skus` 🛠

**Body** — `CreateSkuDto` (như 7.3)

**Response 201** `"Thêm SKU thành công"` — `SkuDto` (`effectivePrice = price`, `activeDiscountPercent = 0`)

**Lỗi:** 400, 403, 404, 500 (`skuCode` trùng)

---

### 7.7. `PUT /api/admin/products/skus/{skuId}/stock` 🛠

Gán tồn kho bằng một giá trị tuyệt đối (không cộng dồn, nên gọi lại nhiều lần vẫn an toàn).

**Body** — `UpdateStockDto`

| Trường | Kiểu | Bắt buộc | Ràng buộc |
|---|---|---|---|
| `stockQuantity` | int | ✅ | ≥ 0 |

**Response 200** `"Cập nhật tồn kho thành công"` — `SkuDto`

**Lỗi:** 400, 403, 404

---

### 7.8. `PUT /api/admin/products/skus/{skuId}/price` 🛠

**Body** — `UpdateSkuPriceDto`

| Trường | Kiểu | Bắt buộc | Ràng buộc |
|---|---|---|---|
| `price` | decimal | ✅ | > 0 |

**Response 200** `"Cập nhật giá thành công"` — `SkuDto` (đã tính lại `effectivePrice`)

Đơn hàng cũ không bị ảnh hưởng vì `OrderItems` lưu snapshot giá.

**Lỗi:** 400, 403, 404

---

## 8. Admin — Discounts — `/api/admin/discounts`

### 8.1. `GET /api/admin/discounts` 🛠

Mọi khuyến mãi (kể cả đã hủy hoặc hết hạn) của các SKU thuộc shop mình, sắp theo `createdAt` giảm dần. **Không phân trang.** Như 7.1, endpoint này tự tạo shop nếu admin chưa có.

**Response 200** — `DiscountDto[]`

| Trường | Kiểu | Mô tả |
|---|---|---|
| `id` | long | |
| `productSkuId` | long | |
| `skuVariantName` | string | Tên biến thể của SKU |
| `discountPercent` | decimal | |
| `startAt` | datetime | |
| `endAt` | datetime | |
| `isActive` | bool | `false` nếu đã bị hủy. Không phản ánh hết hạn; client tự so `endAt` |

---

### 8.2. `POST /api/admin/discounts` 🛠

Tạo khuyến mãi theo % cho một SKU trong một khoảng thời gian. Được phép chồng thời gian với khuyến mãi khác; khi đó hệ thống áp **% lớn nhất**, không cộng dồn.

**Body** — `CreateDiscountDto`

| Trường | Kiểu | Bắt buộc | Ràng buộc |
|---|---|---|---|
| `productSkuId` | long | ✅ | > 0, SKU thuộc shop mình |
| `discountPercent` | decimal | ✅ | > 0 và ≤ 90 |
| `startAt` | datetime | ✅ | UTC |
| `endAt` | datetime | ✅ | > `startAt` |

```json
{ "productSkuId": 15, "discountPercent": 20, "startAt": "2026-10-07T00:00:00Z", "endAt": "2026-10-14T23:59:59Z" }
```

**Response 201** `"Tạo khuyến mãi thành công"` — `DiscountDto`

**Lỗi:** 400, 403 (`SKU không thuộc shop của bạn.`), 404 (`SKU với id ...`)

---

### 8.3. `DELETE /api/admin/discounts/{id}` 🛠

Hủy mềm: đặt `isActive = false`, không xóa bản ghi.

**Response 200** `"Đã hủy khuyến mãi"`, `data: null`

**Lỗi**

| HTTP | message |
|---|---|
| 403 | `Khuyến mãi không thuộc shop của bạn.` |
| 404 | `Khuyến mãi với id '{id}' không tồn tại.` |

---

## 9. Cart — `/api/cart` 👤

Mọi endpoint giỏ hàng (trừ GET) đều trả lại **toàn bộ giỏ hàng sau khi thay đổi**, nên client không cần gọi lại `GET /api/cart`.

`CartSummaryDto`:

```json
{
  "items": [
    {
      "id": 31,
      "productSkuId": 15,
      "productId": 7,
      "productName": "Áo thun basic",
      "variantName": "Trắng / M",
      "unitPrice": 150000.00,
      "effectivePrice": 120000.00,
      "discountPercent": 20.00,
      "quantity": 2,
      "lineTotal": 240000.00,
      "availableStock": 42
    }
  ],
  "totalAmount": 240000.00
}
```

| Trường (`items[]`) | Mô tả |
|---|---|
| `id` | ID cart item (dùng cho PUT/DELETE) |
| `unitPrice` | Giá gốc SKU |
| `effectivePrice` | Giá sau giảm tại thời điểm gọi |
| `lineTotal` | `effectivePrice × quantity` |
| `availableStock` | Tồn kho hiện tại của SKU, để client cảnh báo trước khi checkout |

Item sắp xếp theo thời điểm thêm, mới nhất trước. Giá trong giỏ chỉ để hiển thị; giá thật được tính lại lúc checkout.

### 9.1. `GET /api/cart`

**Response 200** — `CartSummaryDto`

---

### 9.2. `POST /api/cart/items`

Thêm SKU vào giỏ. Nếu SKU đã có trong giỏ thì **cộng dồn** số lượng.

**Body** — `AddCartItemDto`

| Trường | Kiểu | Bắt buộc | Ràng buộc |
|---|---|---|---|
| `productSkuId` | long | ✅ | > 0 |
| `quantity` | int | ✅ | 1–1000 |

**Response 201** `"Đã thêm vào giỏ hàng"` — `CartSummaryDto`

**Lỗi**

| HTTP | message |
|---|---|
| 400 | Validate |
| 400 | `Sản phẩm hiện không khả dụng.` (sản phẩm đang ẩn) |
| 404 | `SKU với id '{id}' không tồn tại.` |

Endpoint **không** kiểm tra tồn kho; thiếu hàng sẽ báo khi checkout.

---

### 9.3. `PUT /api/cart/items/{cartItemId}`

Gán số lượng mới (giá trị tuyệt đối).

**Body** — `UpdateCartItemDto`

| Trường | Kiểu | Bắt buộc | Ràng buộc |
|---|---|---|---|
| `quantity` | int | ✅ | 1–1000 |

**Response 200** `"Cập nhật giỏ hàng thành công"` — `CartSummaryDto`

**Lỗi:** 400, 404 `Sản phẩm trong giỏ hàng với id '{id}' không tồn tại.`

---

### 9.4. `DELETE /api/cart/items/{cartItemId}`

**Response 200** `"Đã xóa khỏi giỏ hàng"` — `CartSummaryDto`

**Lỗi:** 404 `Sản phẩm trong giỏ hàng với id '{id}' không tồn tại.`

---

## 10. Orders — `/api/orders` 👤

`OrderDto`:

```json
{
  "id": 88,
  "orderCode": "ORD202610070315221234",
  "status": "Paid",
  "totalAmount": 240000.00,
  "createdAt": "2026-10-07T03:15:22Z",
  "invoiceJsonPath": "App_Data/invoices/ORD202610070315221234.json",
  "items": [
    {
      "productSkuId": 15,
      "shopId": 1,
      "productName": "Áo thun basic",
      "variantName": "Trắng / M",
      "unitPrice": 150000.00,
      "discountPercentApplied": 20.00,
      "quantity": 2,
      "lineTotal": 240000.00
    }
  ]
}
```

- `orderCode`: `ORD` + `yyyyMMddHHmmss` (UTC) + 4 chữ số ngẫu nhiên.
- `status`: `Paid` (thành công) hoặc `Failed` (hết hàng). `Pending`, `Cancelled` đã khai báo nhưng chưa dùng.
- `invoiceJsonPath`: đường dẫn file hóa đơn phía server (trên Windows dấu phân cách cuối là `\`). `null` với đơn `Failed`. Chưa có endpoint tải file.
- `items[].unitPrice`, `productName`, `variantName`, `discountPercentApplied` là **snapshot** lúc mua.

### 10.1. `POST /api/orders/checkout`

Thanh toán **toàn bộ giỏ hàng** của user. Không có body.

**Header bắt buộc**

```
Authorization: Bearer <accessToken>
Idempotency-Key: 3f1c2a9e-7b4d-4e8a-9c0f-1a2b3c4d5e6f
```

**Xử lý**

1. Nếu đã có response thành công lưu cho `(user, Idempotency-Key, path)`, trả lại y hệt response đó và không chạy lại logic.
2. Trong một DB transaction: trừ kho từng SKU bằng `UPDATE ... WHERE StockQuantity >= quantity`.
3. Đủ hàng: tạo đơn `Paid`, xóa giỏ, gửi thông báo cho người mua và chủ shop, commit, ghi hóa đơn JSON, lưu response vào bảng idempotency.
4. Thiếu hàng ở bất kỳ SKU nào: rollback toàn bộ, tạo đơn `Failed` cùng thông báo thất bại, **giữ nguyên giỏ**, trả 409.

**Response 201** `"Thanh toán thành công"` — `OrderDto`

**Lỗi**

| HTTP | message | Ghi chú |
|---|---|---|
| 400 | `Thiếu header 'Idempotency-Key' bắt buộc cho thao tác này.` | |
| 400 | `Giỏ hàng đang trống.` | |
| 409 | `Sản phẩm '{variantName}' không đủ tồn kho để thanh toán.` | Đã sinh 1 đơn `Failed` + 1 thông báo |
| 500 | | Ví dụ ghi file hóa đơn lỗi (đơn **đã** được commit) |

Response lỗi **không** được lưu idempotency: gửi lại cùng key sau khi sửa giỏ sẽ được xử lý như request mới. Quy tắc sinh và dùng lại key phía client xem [IDEMPOTENCY.md mục 7](IDEMPOTENCY.md#7-cách-gọi-api-từ-phía-client).

> Swagger UI không hiện ô nhập `Idempotency-Key` vì action không khai báo tham số header. Dùng Postman hoặc curl để gọi endpoint này.

---

### 10.2. `POST /api/orders/checkout-no-idempotency` ⚠️

**Chỉ để demo.** Logic giống hệt 10.1 nhưng **không** yêu cầu và không xử lý `Idempotency-Key`. Endpoint này tồn tại để front-end minh họa lỗi khi client retry một checkout bị mất response. Client thật **không được** dùng.

**Response / lỗi:** như 10.1, trừ lỗi thiếu header.

---

### 10.3. `GET /api/orders`

Danh sách đơn hàng của tôi (gồm cả đơn `Failed`).

**Query** — `OrderQuery`: phân trang (mục 1.7) và:

| Tham số | Kiểu | Mô tả |
|---|---|---|
| `status` | string | `Pending` \| `Paid` \| `Failed` \| `Cancelled`, không phân biệt hoa thường. Giá trị không hợp lệ thì bỏ qua bộ lọc |

**Response 200** — `PagedResult<OrderListItemDto>`

| Trường | Kiểu |
|---|---|
| `id` | long |
| `orderCode` | string |
| `status` | string |
| `totalAmount` | decimal |
| `itemCount` | int (số dòng hàng, không phải tổng số lượng) |
| `createdAt` | datetime |

---

### 10.4. `GET /api/orders/{id}`

**Response 200** — `OrderDto`

**Lỗi:** 404 `Đơn hàng với id '{id}' không tồn tại.` (cả khi đơn thuộc user khác)

---

## 11. Admin — Orders — `/api/admin/orders`

### 11.1. `GET /api/admin/orders` 🛠

Danh sách **dòng đơn hàng** (`OrderItem`) có sản phẩm thuộc shop của admin, gồm cả đơn `Failed`. Một đơn có nhiều SKU của shop sẽ hiện thành nhiều dòng. Admin chưa có shop thì nhận danh sách rỗng.

**Query** — `OrderQuery` (như 10.3). `status` lọc theo trạng thái của đơn; `fromDate`/`toDate` lọc theo `CreatedAt` của dòng đơn.

**Response 200** — `PagedResult<AdminOrderLineDto>`

| Trường | Kiểu | Mô tả |
|---|---|---|
| `orderId` | long | |
| `orderCode` | string | |
| `orderStatus` | string | |
| `buyerName` | string | Họ tên người mua |
| `buyerEmail` | string | |
| `productName` | string | Snapshot |
| `variantName` | string | Snapshot |
| `unitPrice` | decimal | Giá gốc lúc mua |
| `discountPercentApplied` | decimal | |
| `quantity` | int | |
| `lineTotal` | decimal | 0 với đơn `Failed` |
| `createdAt` | datetime | |

---

## 12. Notifications — `/api/notifications` 🔑

### 12.1. `GET /api/notifications`

**Query** — `NotificationQuery`: phân trang (mục 1.7) và:

| Tham số | Kiểu | Mô tả |
|---|---|---|
| `isRead` | bool | `true`/`false` để lọc; bỏ trống để lấy tất cả |

**Response 200** — `PagedResult<NotificationDto>`

```json
{
  "items": [
    {
      "id": 140,
      "type": "OrderPlacedSuccess",
      "title": "Đặt hàng thành công",
      "message": "Đơn hàng ORD202610070315221234 đã được thanh toán thành công. Tổng tiền: 240,000đ.",
      "isRead": false,
      "relatedEntityId": 88,
      "createdAt": "2026-10-07T03:15:22Z"
    }
  ],
  "page": 1, "pageSize": 20, "totalCount": 1, "totalPages": 1
}
```

| `type` | Người nhận | `relatedEntityId` trỏ tới |
|---|---|---|
| `OrderPlacedSuccess` | Người mua | `Order.Id` |
| `OrderPlacedFailed` | Người mua | `Order.Id` (đơn Failed) |
| `NewOrderForShop` | Chủ shop | `Order.Id` |
| `ProductHiddenRemovedFromCart` | User có hàng bị xóa khỏi giỏ | `Product.Id` |

Định dạng số tiền trong `message` phụ thuộc culture của server.

---

### 12.2. `PUT /api/notifications/{id}/read`

**Response 200** `"Đã đánh dấu đã đọc"`, `data: null`

**Lỗi:** 404 `Thông báo với id '{id}' không tồn tại.`

---

### 12.3. `DELETE /api/notifications/{id}`

Xóa vĩnh viễn.

**Response 200** `"Đã xóa thông báo"`, `data: null`

**Lỗi:** 404 `Thông báo với id '{id}' không tồn tại.`

---

## 13. Luồng sử dụng điển hình

**Admin đưa sản phẩm lên bán**

```
POST /api/auth/login                          (admin@shopeemini.local / Admin@123)
GET  /api/categories                          → chọn categoryId
POST /api/admin/products                      → tạo SPU + SKU (đang ẩn)
PUT  /api/admin/products/{id}/status          { "isPublic": true }
POST /api/admin/discounts                     (tùy chọn)
```

**User mua hàng**

```
POST /api/auth/register  hoặc  /api/auth/login
GET  /api/products?search=...
GET  /api/products/{id}                       → chọn skuId
POST /api/cart/items                          { "productSkuId": ..., "quantity": ... }
POST /api/orders/checkout                     Idempotency-Key: <uuid mới>
GET  /api/notifications?isRead=false
GET  /api/orders/{id}
```

**Làm mới token**

```
(API trả 401 vì access token hết hạn)
POST /api/auth/refresh  { "refreshToken": "<token hiện tại>" }
→ lưu cả accessToken và refreshToken MỚI (token cũ không dùng lại được)
```
