# Chạy demo SalesHub

## Trạng thái đã kiểm tra

- Main đã tích hợp các nhánh password sessions, audit fixes và role/UI actions.
- Build thành công, không có warning/error; 45/45 test pass, gồm SQL Server và Chrome.
- Migration được kiểm tra từ database mới và cả hai lịch sử nhánh.
- Trên máy chuẩn bị demo, database hiện có đã được sao lưu bằng SQL Server
  COPY_ONLY/CHECKSUM, xác minh bản sao lưu, rồi cập nhật tới
  `20261111054258_ReconcileBranchMigrations`.
- Đã đối chiếu sau migration: số tài khoản, hash mật khẩu, đơn hàng, số tiền,
  trạng thái đơn và số lượng tồn kho không thay đổi.

## Chọn đúng database

Ứng dụng dùng .NET 8. Cần .NET SDK, SQL Server hoặc LocalDB và công cụ `dotnet-ef` 8.x.
Chạy các lệnh dưới đây tại thư mục chứa `PRN222_SalesManagement.sln`.

Development mặc định dùng `(localdb)\MSSQLLocalDB`. Nếu tài khoản/dữ liệu của bạn
nằm ở SQL Server `localhost`, cấu hình riêng cho máy bằng user-secrets:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Database=SalesManagementDb;Trusted_Connection=True;MultipleActiveResultSets=True;Encrypt=False;TrustServerCertificate=True" --project SalesManagement.Web
```

Máy đã được chuẩn bị demo đang dùng cấu hình `localhost` này. User-secrets nằm
ngoài repository, vì vậy không bị thay đổi khi pull code hoặc chuyển branch.
Máy khác cần cấu hình đúng instance chứa dữ liệu của mình. Tránh chạy nhầm sang
database trống rồi tưởng rằng tài khoản hoặc đơn hàng bị mất.

## Khởi động

Nếu ứng dụng đang chạy, dùng cửa sổ đó hoặc dừng nó trước khi chạy lại để tránh
trùng cổng. Với database có dữ liệu, sao lưu trước khi cập nhật schema.

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet build PRN222_SalesManagement.sln
dotnet ef database update --project SalesManagement.Web --startup-project SalesManagement.Web
dotnet run --no-build --project SalesManagement.Web --launch-profile https
```

Mở <https://localhost:7140>. Profile này cũng mở HTTP tại
<http://localhost:5095>. Dùng HTTPS để khớp địa chỉ mặc định trong liên kết
khôi phục mật khẩu. Nếu trình duyệt chưa tin chứng chỉ Development, chạy
`dotnet dev-certs https --trust` và hoàn tất hộp thoại của Windows.

Đăng nhập bằng tài khoản hiện có. Phiên đăng nhập từ nhánh SessionVersion hoặc
cookie cũ thiếu SecurityStamp cần đăng nhập lại sau nâng cấp. Không cần tạo lại
tài khoản hay đặt lại mật khẩu để chạy bản mới.

## Luồng demo

1. Đăng nhập Owner, xem dashboard và chuyển cửa hàng nếu tài khoản có nhiều nơi.
2. Xem sản phẩm, bộ lọc sắp hết hàng và thông báo của cửa hàng hiện tại.
3. Tạo đơn, thử số lượng vượt tồn để thấy validation; sửa lại rồi tạo đơn hợp lệ.
4. Hoàn thành đơn và ghi nhận thanh toán riêng; kiểm tra bộ lọc thanh toán,
   chi tiết đơn, hóa đơn và báo cáo đúng loại tiền.
5. Mở Settings, đổi định dạng ngày/số/ký hiệu tiền. Các lựa chọn lưu trên trình
   duyệt này; ngôn ngữ hiện chỉ hỗ trợ tiếng Việt.
6. Đăng nhập Staff ở cửa sổ khác để kiểm tra các chức năng theo vai trò.

Thay đổi mật khẩu và khôi phục mật khẩu có tác động thật tới tài khoản đang dùng.
Các phiên khác sẽ bị thu hồi. Nếu muốn trình diễn luồng này, dùng tài khoản thử.

Development mặc định lưu thư khôi phục tại `SalesManagement.Web/.local-mail/*.eml`
khi chưa chọn SMTP. Mở thư mới nhất để lấy liên kết; không có email gửi ra ngoài
trong chế độ này. Liên kết có hiệu lực 30 phút và dùng một lần. Không commit hoặc
chia sẻ file thư chứa token. Cấu hình SMTP được mô tả trong `AUDIT_FIXES.md`.

## Chạy lại kiểm tra khi có thay đổi code

```powershell
$env:SALESHUB_TEST_SQLSERVER = "1"
$env:SALESHUB_TEST_BROWSER = "1"
dotnet test PRN222_SalesManagement.sln
```

Các test SQL/Chrome dùng database tạm riêng. Bộ test cần Windows, LocalDB, Node
và Chrome; kết quả nằm dưới `SalesManagement.Tests/TestResults/` và được gitignore.
Đọc `MERGE_RESOLUTION.md` để xem các quyết định xử lý conflict và bằng chứng kiểm tra.
