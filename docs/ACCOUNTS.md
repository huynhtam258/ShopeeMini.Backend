# Tài khoản trong database ShopeeMini

Mật khẩu được lưu dưới dạng **BCrypt hash** (`$2a$12$...`), là hash một chiều, nên **không thể đọc ngược ra mật khẩu từ DB**. Chỉ biết được mật khẩu của những tài khoản mà code tạo ra với giá trị cố định.

## 1. Tài khoản Admin (seed sẵn)

Do [DbSeeder.cs](ShopeeMiniBackEnd/ShopeeMini.Infrastructure/Persistence/Seed/DbSeeder.cs) tạo khi khởi động app, nếu DB chưa có admin nào.

| Email | Mật khẩu | Role | Ghi chú |
|---|---|---|---|
| `admin@shopeemini.local` | `Admin@123` | Admin | Sở hữu shop "ShopeeMini Official Store" |
| `manhtamthuytrang@gmail.com` | `123456789` | User | User |

## 2. Tài khoản test `@test.local` (Role = User)

Các tài khoản này **không có trong code**. Chúng được tạo bằng cách gọi API `POST /api/auth/register` lúc chạy thử/kiểm thử thủ công (ngày 2026-09-30). Số ở giữa email là timestamp (ms) lúc tạo, dùng để email không bị trùng.

| Pattern email | FullName | Mục đích (theo tên) |
|---|---|---|
| `cdptest<timestamp>@test.local` | CDP Test | Test luồng giỏ hàng / đặt hàng |
| `plaincustomer<timestamp>@test.local` | Plain Test | Khách hàng thường |
| `persist<timestamp>@test.local` | Persist Test | Test lưu dữ liệu / đăng nhập lại |
| `customer<timestamp>@test.local` | Khach Test | Khách hàng thường |

**Mật khẩu:** không xác định được. Nó không nằm trong source code, và không thể giải hash. Quy tắc duy nhất là mật khẩu dài 6–100 ký tự ([AuthValidators.cs](ShopeeMiniBackEnd/ShopeeMini.Application/Features/Auth/Validators/AuthValidators.cs)).

### Cách xử lý

- **Cần account User để test:** tự đăng ký account mới qua `POST /api/auth/register` với mật khẩu bạn chọn, rồi ghi vào bảng ở mục 3.
- **Muốn dùng lại account cũ:** copy cột `PasswordHash` của một account đã biết mật khẩu sang account đó (cùng thuật toán BCrypt nên vẫn hợp lệ). Ví dụ, đặt mật khẩu của tất cả account test thành giống một account bạn vừa đăng ký:
  ```sql
  UPDATE Users
  SET PasswordHash = (SELECT h FROM (SELECT PasswordHash AS h FROM Users WHERE Email = 'tai-khoan-moi@test.local') t)
  WHERE Email LIKE '%@test.local';
  ```
- **Dọn dẹp:** xóa các account test nếu không cần (lưu ý các bảng liên quan như CartItems, Orders, RefreshTokens, Notifications):
  ```sql
  DELETE FROM Users WHERE Email LIKE '%@test.local';
  ```

## 3. Tài khoản test có mật khẩu đã biết

Ghi lại ở đây mỗi khi tạo account mới để test:

| Email | Mật khẩu | Role | Ghi chú |
|---|---|---|---|
| | | | |
