# Hướng dẫn load test ShopeeMini API bằng k6

Tài liệu này hướng dẫn cách dùng [k6](https://grafana.com/docs/k6/latest/) để đo sức chịu tải của backend ShopeeMini với các script trong folder `testing-api/`.

## Mục lục

1. [Cấu trúc folder](#1-cấu-trúc-folder)
2. [Cài đặt](#2-cài-đặt)
3. [Chuẩn bị backend trước khi test](#3-chuẩn-bị-backend-trước-khi-test)
4. [browse.js: test luồng xem sản phẩm](#4-browsejs-test-luồng-xem-sản-phẩm)
5. [checkout.js: test luồng mua hàng](#5-checkoutjs-test-luồng-mua-hàng)
6. [Đọc kết quả](#6-đọc-kết-quả)
7. [Theo dõi tài nguyên khi test](#7-theo-dõi-tài-nguyên-khi-test)
8. [Tuỳ chỉnh kịch bản](#8-tuỳ-chỉnh-kịch-bản)
9. [Dọn dữ liệu test](#9-dọn-dữ-liệu-test)
10. [Lỗi thường gặp](#10-lỗi-thường-gặp)

---

## 1. Cấu trúc folder

```
testing-api/
├── browse.js           # Test đọc: xem danh sách + chi tiết sản phẩm (không cần đăng nhập)
├── checkout.js         # Test ghi: thêm giỏ hàng + thanh toán (có tạo dữ liệu test)
├── package.json        # Chỉ để VS Code gợi ý code (@types/k6), KHÔNG dùng để chạy test
├── package-lock.json
└── docs/
    └── K6_GUIDE.md     # Tài liệu này
```

| File | Endpoint được gọi | Cần đăng nhập | Tạo dữ liệu trong DB |
|---|---|---|---|
| `browse.js` | `GET /api/products`, `GET /api/products/{id}` | Không | Không |
| `checkout.js` | `POST /api/auth/login`, `GET /api/categories`, `POST /api/admin/products`, `PUT /api/admin/products/{id}/status`, `POST /api/auth/register`, `POST /api/cart/items`, `POST /api/orders/checkout` | Có | **Có** (xem [mục 9](#9-dọn-dữ-liệu-test)) |

> **k6 không phải là thư viện Node.js.** k6 là một chương trình riêng viết bằng Go, nó tự đọc và chạy file `.js`. `package.json` chỉ chứa `@types/k6` để VS Code gợi ý code khi bạn sửa script. Không có lệnh `npm test` hay `node browse.js`.

---

## 2. Cài đặt

### 2.1. Cài k6 (bắt buộc)

```powershell
winget install k6 --source winget
```

Kiểm tra k6 đã cài thành công:

```powershell
& "C:\Program Files\k6\k6.exe" version
```

> Mọi lệnh trong tài liệu này gọi k6 bằng **đường dẫn đầy đủ** `& "C:\Program Files\k6\k6.exe"`, nên chạy được ngay cả khi terminal chưa nhận lệnh `k6`. Dấu `&` là toán tử gọi chương trình của PowerShell, bắt buộc phải có vì đường dẫn chứa dấu cách.
>
> Nếu đã đóng và mở lại VS Code mà lệnh `k6 version` chạy được, bạn có thể gõ ngắn `k6` thay cho `& "C:\Program Files\k6\k6.exe"` trong mọi lệnh bên dưới.

### 2.2. Gợi ý code trong VS Code (không bắt buộc)

```powershell
cd testing-api
npm install
```

### 2.3. Kiểm tra cú pháp script

`k6 inspect` đọc script mà không gửi request nào, dùng để phát hiện lỗi cú pháp:

```powershell
& "C:\Program Files\k6\k6.exe" inspect browse.js
& "C:\Program Files\k6\k6.exe" inspect checkout.js
```

---

## 3. Chuẩn bị backend trước khi test

> ⚠️ **Bước này quan trọng nhất.** Nếu bỏ qua, kết quả test sẽ không có giá trị.

### 3.1. Bật MySQL

Mở Docker Desktop trước, sau đó chạy ở thư mục `back-end/`:

```powershell
docker compose up -d
```

### 3.2. Chạy API ở chế độ Release và tắt rate limit

API mặc định chỉ cho mỗi IP gửi **100 request/phút** (`RateLimiting:PermitLimit` trong `appsettings.json`). k6 gửi mọi request từ cùng một máy, tức là cùng một IP. Nếu không nới giới hạn này, gần như mọi request sẽ nhận lỗi **429 Too Many Requests**.

Chạy **cả 3 dòng trong cùng một cửa sổ PowerShell**, ở thư mục `back-end/`:

```powershell
$env:RateLimiting__PermitLimit = "100000000"
$env:ASPNETCORE_URLS = "http://localhost:5019"
dotnet run -c Release --no-launch-profile --project ShopeeMiniBackEnd\ShopeeMini.API\ShopeeMini.API.csproj
```

Giải thích các tham số:

| Thành phần | Tác dụng |
|---|---|
| `$env:RateLimiting__PermitLimit` | Ghi đè giá trị `RateLimiting:PermitLimit` trong appsettings mà không cần sửa code. Biến chỉ có tác dụng trong **cửa sổ PowerShell này**. |
| `-c Release` | Build tối ưu. Bản Debug chậm hơn nhiều, nên kết quả sẽ không phản ánh thực tế. |
| `--no-launch-profile` | Không dùng `launchSettings.json`, nên không mở trình duyệt và không ép môi trường `Development`. |
| `ASPNETCORE_URLS` | Lắng nghe ở cổng 5019, khớp với biến `BASE` trong các script. |

> Nếu chạy API bằng F5 trong Visual Studio, hoặc đặt biến môi trường ở một cửa sổ khác, giới hạn 100 request/phút **vẫn còn**.

### 3.3. Kiểm tra API đã sẵn sàng

```powershell
curl.exe http://localhost:5019/api/categories
```

Phản hồi phải có `"success":true`.

### 3.4. Lời khuyên để kết quả chính xác hơn

- **Thu nhỏ cửa sổ console của API** khi đang test. Serilog ghi một dòng log ra console cho mỗi request, và console trên Windows ghi rất chậm, có thể làm request bị chậm thêm vài giây khi tải cao.
- Nếu có thể, **chạy k6 trên một máy khác** với máy chạy API và MySQL. Khi chạy chung một máy, k6 tranh CPU với API nên kết quả sẽ thấp hơn thực tế.
- DB càng có nhiều dữ liệu thì kết quả càng sát thực tế. Với DB gần như trống, query nào cũng nhanh.

---

## 4. browse.js: test luồng xem sản phẩm

### 4.1. Kịch bản

Mỗi user ảo (VU) lặp lại các bước sau:

1. Gọi `GET /api/products?page={1..3}&pageSize=20` (trang ngẫu nhiên từ 1 đến 3).
2. Nếu danh sách có sản phẩm, chọn ngẫu nhiên một sản phẩm và gọi `GET /api/products/{id}`.
3. Nghỉ ngẫu nhiên 1–3 giây, mô phỏng người dùng đang xem trang.

> Danh sách chỉ trả về sản phẩm **đang hiển thị** (`IsPublic = true`). Nếu DB chưa có sản phẩm public nào, bước 2 sẽ không bao giờ chạy. Có thể chạy `checkout.js` một lần trước để tạo sẵn sản phẩm.

### 4.2. Số user ảo tăng dần (`stages`)

| Giai đoạn | Thời gian | Số VU |
|---|---|---|
| Khởi động | 30s | 0 → 50 |
| Giữ | 1m | 50 |
| Tăng | 30s | 50 → 200 |
| Giữ | 1m | 200 |
| Tăng | 30s | 200 → 500 |
| Giữ | 1m | 500 |
| Giảm | 30s | 500 → 0 |

Tổng thời gian khoảng **5 phút**. Mục đích là tìm mức VU mà hệ thống bắt đầu chậm hoặc lỗi.

### 4.3. Tiêu chí đạt (`thresholds`)

| Tiêu chí | Ý nghĩa |
|---|---|
| `http_req_failed: rate<0.01` | Tỉ lệ request lỗi dưới 1% |
| `http_req_duration: p(95)<500` | 95% request nhận phản hồi trong vòng 500ms |

### 4.4. Cách chạy

```powershell
cd testing-api

# 1) Smoke test: 5 VU trong 15 giây, để chắc chắn script chạy đúng và không có lỗi 429
& "C:\Program Files\k6\k6.exe" run --vus 5 --duration 15s browse.js

# 2) Test đầy đủ theo stages (khoảng 5 phút)
& "C:\Program Files\k6\k6.exe" run browse.js
```

Smoke test phải có `http_req_failed` gần **0%** thì mới chạy test đầy đủ.

---

## 5. checkout.js: test luồng mua hàng

### 5.1. Giai đoạn `setup()`: chạy một lần trước khi test

1. Đăng nhập admin (`admin@shopeemini.local` / `Admin@123`, được seed sẵn khi API khởi động).
2. Lấy danh mục đầu tiên từ `GET /api/categories`.
3. Tạo sản phẩm `LoadTest {timestamp}` với 1 SKU giá 100.000đ, **tồn kho 10.000.000**, sau đó chuyển sang trạng thái public.
4. Đăng ký **50 tài khoản** `lt_{timestamp}_{i}@test.local` (mật khẩu `Test@123`) và lưu access token của từng tài khoản.

> Bước 4 mất khoảng **15–60 giây**, vì mỗi lần register phải băm mật khẩu bằng BCrypt (workFactor 12, khoảng 250ms CPU mỗi lần). Trong lúc này k6 trông như đang đứng yên, đó là chuyện bình thường. `setupTimeout` được đặt 180 giây.

### 5.2. Giai đoạn test: mỗi VU lặp lại

1. Lấy token của một tài khoản riêng (VU số *n* dùng tài khoản thứ *n*).
2. `POST /api/cart/items` để thêm 1 sản phẩm vào giỏ.
3. `POST /api/orders/checkout` kèm header `Idempotency-Key` mới cho mỗi lần thanh toán. Endpoint này bắt buộc có header đó.
4. Nghỉ 1 giây.

Mọi VU đều mua **cùng một SKU**. Đây là kịch bản **flash sale**, nặng nhất với cơ chế trừ kho: các transaction phải xếp hàng chờ khoá dòng SKU đó trong MySQL.

### 5.3. Số user ảo tăng dần (`stages`)

| Giai đoạn | Thời gian | Số VU |
|---|---|---|
| Khởi động | 30s | 0 → 20 |
| Tăng | 1m | 20 → 50 |
| Giảm | 30s | 50 → 0 |

Số VU tối đa bằng hằng số `USERS = 50`, nên mỗi VU luôn có tài khoản riêng. Nếu tăng số VU, hãy tăng `USERS` theo. Nếu nhiều VU dùng chung một tài khoản, họ sẽ tranh nhau cùng một giỏ hàng và tạo ra lỗi không phản ánh thực tế.

### 5.4. Tiêu chí đạt (`thresholds`)

| Tiêu chí | Ý nghĩa |
|---|---|
| `http_req_failed: rate<0.01` | Tỉ lệ lỗi dưới 1% (tính mọi request, kể cả request trong `setup`) |
| `http_req_duration{name:checkout}: p(95)<1000` | 95% lần **thanh toán** hoàn tất trong vòng 1 giây |

### 5.5. Cách chạy

```powershell
cd testing-api
& "C:\Program Files\k6\k6.exe" run checkout.js
```

### 5.6. Lưu ý

- **Access token chỉ có hiệu lực 15 phút** (`Jwt:AccessTokenMinutes`). Tổng thời gian của setup và test phải ngắn hơn 15 phút. Nếu kéo dài `stages`, sau 15 phút mọi request sẽ nhận lỗi **401**.
- Script **tạo dữ liệu thật** trong DB: sản phẩm, user, đơn hàng, notification, file hoá đơn JSON trong `App_Data/invoices/`. **Không chạy trên DB production.**
- Nếu `setup()` báo lỗi kiểu `cannot read property 'id' of undefined`, nghĩa là một bước trong setup đã thất bại, thường do API chưa chạy, đang bị rate limit, hoặc sai mật khẩu admin. Xem [mục 10](#10-lỗi-thường-gặp).

---

## 6. Đọc kết quả

Khi chạy xong, k6 in ra một bảng tổng kết. Ví dụ rút gọn:

```
█ THRESHOLDS
  http_req_duration
  ✓ 'p(95)<500' p(95)=120ms
  http_req_failed
  ✓ 'rate<0.01' rate=0.00%

█ TOTAL RESULTS
  checks_succeeded...: 100.00%
  ✓ list 200
  ✓ detail 200

  HTTP
  http_req_duration..............: avg=45ms  med=20ms  p(90)=95ms  p(95)=120ms
  http_req_failed................: 0.00%
  http_reqs......................: 52000  173/s

  EXECUTION
  vus_max........................: 500
```

### 6.1. Ý nghĩa các chỉ số chính

| Chỉ số | Ý nghĩa | Nên xem gì |
|---|---|---|
| `THRESHOLDS` ✓ / ✗ | Có đạt tiêu chí đặt trong `options.thresholds` không | Chỉ cần một dòng ✗ là cả lần test được coi là **không đạt**, và k6 kết thúc với exit code khác 0 |
| `checks` | Tỉ lệ các phép `check()` trong script thành công (ví dụ `list 200`) | Nên gần **100%** |
| `http_req_duration` | Thời gian từ lúc gửi request đến lúc nhận đủ phản hồi | Xem **p(95)** quan trọng hơn `avg`, vì trung bình dễ che giấu các request chậm |
| `{ expected_response:true }` | Giống dòng trên nhưng **chỉ tính request thành công** | So sánh với dòng trên để biết request lỗi nhanh hay chậm |
| `http_req_failed` | Tỉ lệ request có mã phản hồi 4xx hoặc 5xx | Nên dưới **1%** |
| `http_reqs` | Tổng số request và số **request/giây (RPS)** | Đây là thông lượng của hệ thống |
| `iteration_duration` | Thời gian một vòng `default function`, bao gồm cả `sleep` | |
| `vus` / `vus_max` | Số user ảo đang chạy / tối đa | |

### 6.2. Cách tìm sức chịu tải

1. Tìm mức VU **cao nhất** mà vẫn giữ được **p(95) < 500ms** và **lỗi < 1%**. Đó là sức chịu tải của hệ thống với kịch bản này.
2. Dấu hiệu đã chạm trần: tăng VU nhưng **RPS không tăng nữa**, còn **p(95) tăng vọt**.
3. Quy đổi sang số người dùng thật:

   ```
   Số user đồng thời ≈ RPS × (thời gian phản hồi + thời gian user nghỉ giữa 2 thao tác)
   ```

   Ví dụ: hệ thống đạt 300 RPS và người dùng thật thao tác khoảng 5 giây một lần, thì phục vụ được khoảng 1.500 người online cùng lúc.

### 6.3. Ví dụ một lần test **không có giá trị**

```
http_req_failed......: 97.92% 23580 out of 24080
✗ list 200  ↳ 1% — ✓ 399 / ✗ 23536
✗ detail 200 ↳ 69% — ✓ 101 / ✗ 44
```

Số request thành công là 399 + 101 = **500** trong 5 phút, tức đúng **100 request/phút**. Đây là dấu hiệu rate limit vẫn còn bật. Đếm mã phản hồi trong log API (xem [mục 7.3](#73-đếm-mã-phản-hồi-trong-log-api)) sẽ thấy hàng chục nghìn dòng `responded 429`. Cần làm lại [mục 3.2](#32-chạy-api-ở-chế-độ-release-và-tắt-rate-limit) rồi chạy test lại.

---

## 7. Theo dõi tài nguyên khi test

Mở thêm các cửa sổ terminal trong lúc k6 đang chạy.

### 7.1. API (.NET)

```powershell
dotnet tool install -g dotnet-counters   # chỉ cần cài một lần
dotnet-counters monitor -n ShopeeMini.API
```

| Counter | Khi nào đáng lo |
|---|---|
| `CPU Usage` | Gần 100%: API là điểm nghẽn |
| `ThreadPool Queue Length` | Tăng liên tục: request phải xếp hàng, thường do đang chờ connection DB |
| `GC Heap Size` | Tăng mãi không giảm khi chạy lâu: có thể bị rò rỉ bộ nhớ |

### 7.2. MySQL

```powershell
docker stats shopeemini-mysql
```

Nếu CPU của MySQL cao trong khi API còn rảnh, điểm nghẽn nằm ở query: thiếu index, hoặc lọc và sắp xếp trên bảng lớn.

### 7.3. Đếm mã phản hồi trong log API

Serilog ghi file vào `Logs/log-YYYYMMDD.txt`, nằm trong **thư mục bạn đứng khi chạy API**. Ví dụ chạy từ `back-end/` thì log nằm ở `back-end/Logs/`, chạy từ Visual Studio thì nằm ở `ShopeeMiniBackEnd/ShopeeMini.API/Logs/`.

```powershell
Select-String -Path Logs\log-*.txt -Pattern "responded (\d{3})" |
  ForEach-Object { $_.Matches[0].Groups[1].Value } |
  Group-Object | Select-Object Name, Count
```

Kết quả cho biết có bao nhiêu request trả về 200, 201, 400, 429, 500…

| Mã | Ý nghĩa khi load test |
|---|---|
| 429 | Rate limit vẫn bật, xem lại mục 3.2 |
| 401 | Token hết hạn (sau 15 phút) hoặc sai token |
| 400 khi checkout | Giỏ hàng trống hoặc hết hàng, xem message trong response |
| 500 | Lỗi server. Tìm dòng `[ERR]` trong log để xem chi tiết |

---

## 8. Tuỳ chỉnh kịch bản

### 8.1. Đổi địa chỉ server

Sửa hằng số `BASE` ở đầu mỗi file, ví dụ:

```javascript
const BASE = 'http://192.168.1.10:5019';
```

### 8.2. Ghi đè số VU và thời gian từ dòng lệnh

Tham số dòng lệnh được ưu tiên hơn `options` trong script:

```powershell
& "C:\Program Files\k6\k6.exe" run --vus 100 --duration 2m browse.js      # 100 VU cố định trong 2 phút, bỏ qua stages
& "C:\Program Files\k6\k6.exe" run --iterations 10 --vus 1 checkout.js    # chạy đúng 10 vòng, dùng để debug
```

### 8.3. Các kiểu test nên làm

Sửa mảng `stages` trong `options` theo mục đích test:

| Kiểu test | Mục đích | Gợi ý `stages` |
|---|---|---|
| **Smoke** | Kiểm tra script chạy đúng | Dùng `--vus 5 --duration 15s` |
| **Load** | Tải bình thường trong thời gian dài | Tăng lên mức mục tiêu, giữ 5–10 phút |
| **Stress** | Tìm điểm gãy | Tăng dần nhiều nấc như `browse.js` |
| **Spike** | Tải tăng đột ngột (mở bán, flash sale) | `{ duration: '5s', target: 500 }, { duration: '1m', target: 500 }, { duration: '5s', target: 0 }` |
| **Soak** | Rò rỉ bộ nhớ, cạn connection pool | Tải vừa phải, giữ 1–2 giờ (chỉ dùng `browse.js`, vì token trong `checkout.js` hết hạn sau 15 phút) |

### 8.4. Xem biểu đồ trực tiếp trên trình duyệt

k6 có sẵn web dashboard:

```powershell
$env:K6_WEB_DASHBOARD = "true"
$env:K6_WEB_DASHBOARD_EXPORT = "report.html"   # lưu báo cáo HTML sau khi test xong
& "C:\Program Files\k6\k6.exe" run browse.js
```

Trong lúc test, mở `http://localhost:5665` để xem biểu đồ RPS, thời gian phản hồi và số VU theo thời gian.

### 8.5. Lưu kết quả ra file

```powershell
& "C:\Program Files\k6\k6.exe" run browse.js | Tee-Object -FilePath result-browse.txt    # lưu bảng tổng kết
& "C:\Program Files\k6\k6.exe" run --out json=result-browse.json browse.js               # lưu chi tiết từng request (file lớn)
```

---

## 9. Dọn dữ liệu test

Mỗi lần chạy `checkout.js` sẽ tạo thêm:

- 1 sản phẩm `LoadTest {timestamp}` của shop admin
- 50 user `lt_{timestamp}_{i}@test.local`
- Đơn hàng, notification, refresh token và idempotency record của các user trên
- File hoá đơn `App_Data/invoices/*.json`

Cách đơn giản nhất là **xoá toàn bộ DB dev và tạo lại**:

```powershell
# ⚠️ XOÁ TOÀN BỘ DỮ LIỆU trong MySQL container, chỉ dùng cho môi trường dev
docker compose down -v
docker compose up -d
```

Khi khởi động lại, API tự chạy migration và seed lại tài khoản admin cùng 4 danh mục gốc. Dữ liệu bạn tự nhập trước đó sẽ mất hết.

Nếu muốn giữ dữ liệu, hãy ẩn sản phẩm `LoadTest…` trong trang admin để nó không còn hiện trên danh sách sản phẩm.

---

## 10. Lỗi thường gặp

| Hiện tượng | Nguyên nhân | Cách xử lý |
|---|---|---|
| `k6 is not recognized…` | Terminal mở từ trước khi cài k6 | Đóng và mở lại VS Code, hoặc dùng `& "C:\Program Files\k6\k6.exe"` |
| `connection refused` / `dial tcp … 5019` | API chưa chạy hoặc chạy sai cổng | Xem mục 3.2 và 3.3 |
| Tỉ lệ lỗi rất cao, thành công đúng khoảng 100 request/phút | Rate limit vẫn bật | Chạy lại API theo mục 3.2, **trong cùng cửa sổ** với lệnh `$env:` |
| `setup()` lỗi `cannot read property 'id' of undefined` | Login admin, tạo sản phẩm hoặc lấy danh mục thất bại | Kiểm tra API, rate limit, tài khoản admin. Chạy `& "C:\Program Files\k6\k6.exe" run --http-debug --iterations 1 --vus 1 checkout.js` để xem chi tiết request và response |
| `setup()` quá 180 giây | Register 50 user chậm vì BCrypt, máy yếu | Giảm `USERS` hoặc tăng `setupTimeout` |
| Lỗi 401 sau khoảng 15 phút | Access token hết hạn | Rút ngắn `stages` của `checkout.js` |
| `detail 200` không bao giờ chạy | Chưa có sản phẩm public | Tạo và hiển thị sản phẩm trong trang admin, hoặc chạy `checkout.js` một lần |
| Server xử lý nhanh (log ghi vài ms) nhưng k6 báo chậm vài giây | Console log chậm, k6 tranh CPU với API | Thu nhỏ console của API, chạy k6 ở máy khác |
| Lỗi 500 khi checkout | Lỗi server, thường do DB hết connection hoặc deadlock khi tải cao | Tìm dòng `[ERR]` trong file log của API |
