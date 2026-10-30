# Kiến trúc backend Dayloom

Dayloom dùng **Clean Architecture**, bên trong mỗi tầng chia thư mục theo tính năng (**Vertical Slice**). Đây là quyết định cố định của dự án.

## 1. Bốn tầng và quy tắc phụ thuộc

```
           ┌──────────────────────────┐
           │       Dayloom.Api        │  HTTP endpoint, Program.cs (composition root)
           └──────┬────────────┬──────┘
                  │            │
                  ▼            ▼
┌──────────────────────┐   ┌──────────────────────────┐
│ Dayloom.Application  │◄──│  Dayloom.Infrastructure  │  EF Core, PostgreSQL, Identity, JWT
│ use case + interface │   │  cài đặt các interface    │
└──────────┬───────────┘   └──────────────────────────┘
           ▼
┌──────────────────────┐
│    Dayloom.Domain    │  entity + quy tắc nghiệp vụ, không phụ thuộc gì
└──────────────────────┘
```

**Quy tắc phụ thuộc:** mũi tên chỉ hướng vào trong. Tầng trong không biết tầng ngoài tồn tại.

| Tầng | Được tham chiếu | Chứa gì | Ví dụ trong Dayloom |
|---|---|---|---|
| **Domain** | Không gì cả | Entity, quy tắc nghiệp vụ thuần tuý | `Session.Start()` tự tính hạn 14 ngày; `RefreshToken.MarkUsed()` cấm dùng 2 lần |
| **Application** | Domain (+ gói EF Core, xem §3) | Use case, interface cho thứ nó cần từ bên ngoài | `IAppDbContext` |
| **Infrastructure** | Application | Code thật cho các interface: DB, Identity, gửi email... | `AppDbContext`, `AppUser`, cấu hình bảng, migration |
| **Api** | Application, Infrastructure | Nhận HTTP, gọi use case, ráp các tầng (DI) | `Program.cs`, `HealthEndpoints` |

Điểm mấu chốt (**Dependency Inversion**): `IAppDbContext` nằm ở Application, nhưng `AppDbContext` cài đặt nó lại nằm ở Infrastructure. Application **ra yêu cầu**, Infrastructure **đáp ứng**. Nhờ vậy, đổi PostgreSQL sang DB khác chỉ phải sửa Infrastructure.

Quy tắc được giữ bằng 2 lớp:
1. **Tham chiếu project:** Domain không tham chiếu project nào, nên trình biên dịch báo lỗi nếu Domain dùng EF Core.
2. **Architecture test** (`tests/Dayloom.UnitTests/ArchitectureTests.cs`), dạng **allowlist** (chỉ cho phép những gì được liệt kê, còn lại đều bị từ chối):
   - Đọc file `.csproj`: tham chiếu project đúng chiều; Domain và Application dùng SDK thường (không phải `Sdk.Web`), không `FrameworkReference`, chỉ có gói được phép.
   - Đọc assembly đã build: Domain chỉ dùng thư viện chuẩn `System.*`; Application thêm được Domain, EF Core và DI abstractions.
   - Có một **test đối chứng** đưa vào tên thư viện cấm (ví dụ logging) để chứng minh bộ kiểm tra không "pass tất".

## 2. Cấu trúc thư mục

```
backend/src/
  Dayloom.Domain/
    Auth/                       Session, RefreshToken
  Dayloom.Application/
    Common/Interfaces/          IAppDbContext
    DependencyInjection.cs      AddApplication()
  Dayloom.Infrastructure/
    Identity/                   AppUser (kế thừa IdentityUser)
    Persistence/
      AppDbContext.cs
      Configurations/           cấu hình bảng: index, khoá ngoại, CHECK
      Migrations/
    DependencyInjection.cs      AddInfrastructure(): DB, health check
  Dayloom.Api/
    Features/Health/
    Program.cs
backend/tests/
  Dayloom.UnitTests/            Domain + Application + architecture. Không cần DB, chạy dưới 1 giây
  Dayloom.IntegrationTests/     API thật + PostgreSQL thật (Testcontainers)
```

Khi thêm tính năng mới, ví dụ **Tasks**: `Domain/Tasks/` → `Application/Features/Tasks/` → cấu hình bảng ở `Infrastructure/Persistence/Configurations/` → endpoint ở `Api/Features/Tasks/`.

## 3. Các quyết định thiết kế và lý do

### `AppUser` nằm ở Infrastructure, không ở Domain
`AppUser` kế thừa `IdentityUser` của ASP.NET Identity. Nếu đặt ở Domain, Domain sẽ phụ thuộc framework, vi phạm quy tắc. Domain chỉ biết người dùng qua `UserId` (`Guid`). Đây cũng là cách template Clean Architecture phổ biến nhất (Jason Taylor) làm.

### Rich domain model: entity có hành vi
Entity không phải "túi dữ liệu" với `get; set;` công khai. Thuộc tính là `private set`, và chỉ thay đổi được qua phương thức có kiểm tra:
- `Session.Start(userId, now)`: tự đặt hạn 14 ngày, lưu giờ UTC.
- `session.IssueRefreshToken(hash, now)`: từ chối nếu phiên đã thu hồi hoặc hết hạn; token hết hạn **cùng lúc** với phiên.
- `token.MarkUsed(now)`: từ chối nếu token đã dùng.

Lợi ích: không thể tạo dữ liệu sai từ code, và quy tắc nghiệp vụ test được bằng unit test không cần DB. Constructor `private` không tham số chỉ để EF Core dùng khi đọc dữ liệu lên.

**`private set` chưa đủ để bất biến.** Ba cái bẫy đã gặp trong Dayloom (Codex tìm ra, xem `docs/reviews/2026-10-30-clean-architecture-review.md`):
- **Mảng** (`byte[] TokenHash`): `private set` chỉ chặn gán mảng mới, không chặn sửa từng phần tử. Cách xử lý: sao chép khi nhận vào và khi trả ra; EF Core đọc/ghi thẳng field `_tokenHash`.
- **Collection** (`IReadOnlyCollection<RefreshToken>`): nếu trả thẳng `List<T>`, người gọi chỉ cần ép kiểu về `List<T>` là thêm/xoá được, và EF còn xoá luôn hàng trong DB. Cách xử lý: trả `_refreshTokens.AsReadOnly()`; EF dùng field `_refreshTokens`.
- **Thời gian**: phương thức phải kiểm tra cả hai đầu khoảng thời gian (không dùng token trước lúc tạo, không thu hồi phiên trước lúc bắt đầu), và khi từ chối thì không được thay đổi trạng thái.

**Hai lớp bảo vệ:** Domain chặn dữ liệu sai từ code; ràng buộc DB (CHECK, unique index) chặn dữ liệu sai từ mọi nguồn khác (SQL tay, bug, migration). Test ở `IntegrationTests/Persistence/AuthSchemaTests.cs` cố tình lách qua Domain để chứng minh lớp DB vẫn hoạt động độc lập.

### `IAppDbContext` thay vì Repository cho từng entity
Application truy cập DB qua `IAppDbContext`, có `DbSet<Session>`... thay vì viết `ISessionRepository`, `IRefreshTokenRepository`...

- **Được:** không phải viết hàng chục class repository chỉ để bọc lại EF Core; use case dùng LINQ đầy đủ.
- **Mất:** Application phải tham chiếu gói `Microsoft.EntityFrameworkCore` (chỉ phần abstraction, không có Npgsql hay PostgreSQL). Người theo Clean Architecture "thuần" sẽ không chấp nhận điều này.
- **Vì sao vẫn chọn:** EF Core đã là một Unit of Work và Repository. Bọc thêm một lớp nữa chủ yếu tăng code mà ít lợi ích thật. Template của Jason Taylor cũng chọn như vậy.

### Không dùng MediatR
Từ 2025 MediatR chuyển sang giấy phép thương mại cho doanh nghiệp. Mỗi use case sẽ là một class handler bình thường, đăng ký trong `AddApplication()` và được endpoint gọi trực tiếp. Ít "ma thuật" hơn, dễ debug hơn.

### Health check dùng cơ chế có sẵn
Infrastructure đăng ký `AddDbContextCheck<AppDbContext>()`; Api chỉ quyết định hình dạng JSON trả về. Api không cần gọi thẳng `AppDbContext`.

## 4. Câu hỏi phỏng vấn hay gặp

**"Clean Architecture khác N-Layer thế nào?"**
Cả hai đều chia tầng. N-Layer cho tầng nghiệp vụ phụ thuộc tầng dữ liệu. Clean Architecture đảo ngược điều đó bằng Dependency Inversion: tầng nghiệp vụ định nghĩa interface, tầng hạ tầng cài đặt nó. Trong Dayloom: `IAppDbContext` (Application) ← `AppDbContext` (Infrastructure).

**"Application tham chiếu EF Core, vậy có còn là Clean Architecture không?"**
Có, đây là một đánh đổi có chủ đích (xem §3). Application chỉ dùng abstraction của EF Core, không biết gì về PostgreSQL. Nếu cần tách hẳn, có thể thay `IAppDbContext` bằng repository mà không đụng tới Domain.

**"Vì sao entity dùng `private set`?"**
Để mọi thay đổi trạng thái phải đi qua phương thức có kiểm tra quy tắc. Ví dụ không ai có thể đặt thời hạn phiên 30 ngày, vì chỉ có `Session.Start()` đặt được, và nó luôn đặt 14 ngày.

**"Làm sao đảm bảo không ai phá quy tắc phụ thuộc?"**
Tham chiếu giữa các project, cộng với architecture test chạy trong CI.
