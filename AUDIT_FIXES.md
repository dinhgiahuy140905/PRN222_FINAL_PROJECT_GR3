# Sửa lỗi rà soát — 09/11/2026

## Giới hạn dữ liệu

- Hoàn kho và điều chỉnh kho dùng phép tính long để kiểm tra trước khi ghi int.
  Nếu vượt giới hạn, đơn/kho/lịch sử kho được giữ nguyên và giao diện hiện lỗi.
- Migration GuardNonnegativeInventory thêm CHECK Quantity >= 0 trên InventoryItems.
  Nếu DB cũ đã có tồn kho âm, cần kiểm tra và sửa theo số liệu thực trước khi áp dụng;
  migration không tự đặt lại số liệu.
- Tổng tiền hàng không được vượt 9999999999999999.99, phù hợp decimal(18,2).
  Giá/giảm giá không được có quá hai chữ số thập phân.
- Form đọc dấu chấm là phần thập phân, không phụ thuộc ngôn ngữ Windows.

## Tùy chọn hiển thị

Lựa chọn ngày, số và ký hiệu/mã tiền tệ áp dụng trên thiết bị này qua localStorage.
Các màn hình đơn, hóa đơn, dashboard và báo cáo có dữ liệu để định dạng lại an toàn
bằng textContent. Form tạo đơn dùng cùng bộ định dạng, kể cả dòng thêm mới.
Các input giá/ngày và dữ liệu lưu DB không bị thay đổi theo cách hiển thị.
Giao diện hiện chỉ hỗ trợ Tiếng Việt; lựa chọn English chưa có bản dịch đã được bỏ.

## Quên mật khẩu

- Liên kết ký bằng ASP.NET Core Data Protection, hết hạn sau 30 phút.
- Sau khi dùng một lần hoặc đổi mật khẩu, các liên kết cũ mất hiệu lực.
- Reset cập nhật hash, thời điểm đổi và SessionVersion trong một lệnh SQL có điều kiện.
  Các phiên cũ bị thu hồi ở request được bảo vệ tiếp theo.
- Nội dung phản hồi không tiết lộ email có tài khoản hay không; form có antiforgery.
- Giới hạn tổng cộng 10 POST yêu cầu/reset mỗi IP mỗi phút, vượt mức trả 429.
- PublicBaseUrl được cấu hình cố định, không lấy từ Host header của request.

### Chạy local

Development mặc định dùng LocalDB MSSQLLocalDB/SalesManagementDb. Có thể ghi đè
bằng user-secrets hoặc biến môi trường nếu máy dùng instance khác.

Khi chưa cấu hình SMTP, thư thử được lưu dưới SalesManagement.Web/.local-mail/*.eml.
Thư chứa liên kết có hiệu lực thật: chỉ mở trên máy phát triển, không commit/chia sẻ.
Thư mục nằm ngoài wwwroot và đã được gitignore. Có thể mở .eml bằng trình đọc email.
Không có thư nào được gửi ra ngoài ở chế độ Pickup.

### Bật Gmail SMTP

Tên Gmail, From, host smtp.gmail.com và port 587 đã được cấu hình riêng trong
user-secrets trên máy hiện tại. Không lưu mật khẩu vào appsettings hoặc Git.
Sau khi tạo mật khẩu ứng dụng Gmail, tự cấu hình ở terminal local:

```powershell
dotnet user-secrets set "Mail:Password" "MAT_KHAU_UNG_DUNG_GMAIL" --project SalesManagement.Web
dotnet user-secrets set "Mail:Mode" "Smtp" --project SalesManagement.Web
```

Không gửi mật khẩu ứng dụng trong chat. Khởi động lại web sau khi đổi cấu hình.
Với môi trường public, cấu hình thêm PasswordReset:PublicBaseUrl bằng URL HTTPS thật.
Production không dùng chế độ lưu thư thử local.

## Kiểm chứng

```powershell
dotnet test
$env:SALESHUB_TEST_SQLSERVER = '1'
dotnet test
node SalesManagement.Tests/Browser/display-preferences.cjs
node SalesManagement.Tests/Browser/order-create-xss.cjs USD en-US code
```

Test SQL Server dùng database tạm riêng và tự xóa, không thay đổi DB đang sử dụng.
Test email dùng sender giả; không gửi Gmail thật. Cần cấu hình mật khẩu ứng dụng
để tự kiểm tra việc nhận thư qua SMTP thực tế.
