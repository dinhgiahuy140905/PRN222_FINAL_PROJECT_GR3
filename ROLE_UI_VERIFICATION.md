# Kiểm tra nút chức năng và quyền — 09/11/2026

Nhánh: dinhgiahuy140905/fix-role-ui-actions.

## Các lỗi đã sửa

- Khung validation có class alert-danger vẫn hiện dù ModelState hợp lệ. CSS giờ
  ẩn validation-summary-valid; validation-summary-errors vẫn hiển thị khi có lỗi.
  Áp dụng cả Owner/Staff, giao diện sáng/tối, form đơn hàng và cài đặt.
- Chuông thông báo trước đây chỉ là nút trang trí. Hiện mở dropdown có số cảnh báo
  và các liên kết đến sản phẩm hết/sắp hết hàng, đơn đang xử lý và đơn hoàn tất
  chưa thanh toán. Không có dữ liệu sẽ hiện trạng thái rỗng, không hiện badge giả.
- Thông báo xác minh membership trong DB và chỉ truy vấn StoreId hiện tại.
  Đây là tình trạng vận hành, cập nhật khi tải trang, không phải hộp thư thông báo
  lưu lịch sử hay tính năng đánh dấu đã đọc.
- Danh sách đơn có bộ lọc thanh toán, giúp liên kết thông báo mở đúng nhóm đơn.
- Nút hỗ trợ mở trang hướng dẫn thực tế; tên/ảnh đại diện mở hồ sơ cá nhân.
- Tên cửa hàng ở header đọc từ membership/DB, tránh dùng tên cũ trong cookie.
- Dashboard cửa hàng trống có nội dung phù hợp với role, không hướng Staff tới
  chức năng mời nhân viên của Owner.

## Ma trận quyền hiện tại

| Chức năng tại cửa hàng | Owner | Staff |
| --- | --- | --- |
| Sản phẩm, danh mục, điều chỉnh kho | Có | Có |
| Khách hàng, đơn, hóa đơn, thanh toán, hoàn tiền, hủy | Có | Có |
| Báo cáo, thông báo cửa hàng | Có | Có |
| Hồ sơ, đổi mật khẩu, tùy chọn cá nhân | Có | Có |
| Thêm/gỡ nhân viên, cấu hình cửa hàng | Có | Không |

Tạo cửa hàng mới phụ thuộc AccountType.Owner. Quyền quản lý tại từng cửa hàng
phụ thuộc StoreRole trong membership, không phụ thuộc AccountType hay role cũ
trong cookie. Các quyền nghiệp vụ sẵn có của Staff được giữ nguyên.

## Kiểm chứng

36/36 test pass, bao gồm SQL Server và 99 kiểm tra Chrome. Trình duyệt thực hiện:

- Đăng nhập/đăng xuất, liên kết hồ sơ/hỗ trợ, chuông và liên kết thông báo.
- Sáng/tối, thu gọn/mở sidebar, menu và chuông trên viewport mobile.
- Chuyển các tab cài đặt, lưu hồ sơ/tùy chọn, đổi mật khẩu; Owner lưu cài đặt cửa hàng.
- Owner thêm và gỡ một nhân viên thử.
- Tạo/sửa sản phẩm, điều chỉnh kho, ngừng/mở bán; tạo/xóa danh mục rỗng.
- Tạo/sửa khách hàng; validation thiếu hàng; thêm/xóa/reindex dòng đơn.
- Tạo/hoàn thành/thanh toán/xem/in hóa đơn/hoàn tiền/hủy đơn; lọc báo cáo.
- Chặn Staff truy cập quản lý, chặn tạo cửa hàng với AccountType.Staff.
- Tài khoản Owner chuyển sang cửa hàng có membership Staff không thấy quyền Owner.
- Cảnh báo rỗng không hiện badge, không lẫn dữ liệu giữa hai cửa hàng.

Test server bổ sung cố tình POST vào endpoint quản lý với tài khoản Staff và
antiforgery token hợp lệ: yêu cầu bị từ chối, dữ liệu được giữ nguyên. Test cũng
thay role membership khi cookie Owner vẫn còn để xác nhận DB là nguồn phân quyền.

```powershell
$env:SALESHUB_TEST_SQLSERVER = '1'
$env:SALESHUB_TEST_BROWSER = '1'
dotnet test
```

Test Chrome cần Node, Chrome và SQL Server LocalDB trên Windows. Có thể dùng
BROWSER_PATH để chọn Chromium khác. Test tạo web/database tạm riêng và tự dọn;
không tạo/sửa tài khoản, sản phẩm, đơn hoặc gửi email bằng database người dùng.
Ảnh và danh sách kiểm tra lưu ở thư mục review-evidence/role-ui bên ngoài repository.
