# Review chuyển đổi Clean Architecture — 2026-10-30

## Phạm vi và phiên bản bàn giao

- Reviewer: Codex; Claude Code phụ trách xử lý phát hiện.
- Nhánh: `refactor/clean-architecture`.
- Baseline schema: `aba1859c51129b685bbc90b596ac820d08a41fac`.
- Phiên bản được review: `aba1859..e406392836a217737667e3b3907e2ff89ac5979b`, gồm commit code `a221ac49924101ea519837fc210be5026acec0cb` và commit tài liệu `e406392836a217737667e3b3907e2ff89ac5979b`.
- `git log main..refactor/clean-architecture` còn liệt kê hai commit schema Auth trước đó; review này tập trung phần chuyển đổi sau baseline `aba1859`.
- Đã đọc `AGENTS.md`, `docs/ARCHITECTURE.md`, `docs/DESIGN.md` và nhật ký “Ngày 1, phần 6” trong `docs/PROGRESS.md`.
- Phạm vi: bốn project cùng `.csproj`, Domain `Session`/`RefreshToken`, `IAppDbContext`, DI, EF configuration/migration/snapshot, unit/integration tests và `ArchitectureTests`.
- Working tree trước review sạch, HEAD khớp nhánh được giao. Không yêu cầu endpoint Auth, không review style.

## Kết luận

`cd backend && dotnet test`: **PASS 44/44** — 24 unit tests và 20 integration tests; 0 failed, 0 skipped. Build/restore thành công.

Hướng phụ thuộc hiện tại đúng quy tắc đã chốt; Domain chưa rò rỉ framework. EF Core ánh xạ và đọc lại private setter/backing field đúng trong probe độc lập. Phần thân migration tạo/xóa schema giống baseline sau khi chuẩn hóa namespace, nhưng **migration ID đã đổi**, làm hỏng đường nâng cấp DB đã áp bản cũ.

Có **5 phát hiện: 1 Cao (P1), 4 Trung bình (P2)**. Các probe bổ sung tái hiện được các vấn đề dưới đây dù suite hiện có vẫn xanh.

## Phát hiện

### CA-01 — Đổi migration ID khiến DB đã áp baseline không nâng cấp được

- **Mức độ:** Cao (P1).
- **Vị trí:** `backend/src/Dayloom.Infrastructure/Persistence/Migrations/20261030070339_InitialAuthSchema.Designer.cs:15`; việc đổi tên/di chuyển migration cũ `20261029220014_InitialAuthSchema`.
- **Vấn đề:** baseline có `[Migration("20261029220014_InitialAuthSchema")]`, bản mới dùng `20261030070339_InitialAuthSchema`. EF theo dõi migration bằng history, không nhận diện hai migration là một chỉ vì DDL giống nhau. DB đã chạy baseline sẽ coi bản mới là chưa áp và chạy lại `CREATE TABLE users`. Testcontainers khởi tạo DB trắng nên các test hiện tại không thấy regression này.
- **Bằng chứng:** nội dung migration chính của hai phiên bản, mỗi bản 211 dòng, giống nhau hoàn toàn sau khi thay duy nhất dòng namespace bằng giá trị chung. Trên DB review riêng có schema đó, đổi history sang ID baseline để mô phỏng DB cũ: `GetPendingMigrations()` trả `20261030070339_InitialAuthSchema`; gọi `Migrate()` nhận PostgreSQL **`42P07: relation "users" already exists`**. Không thao tác history hoặc dữ liệu DB dev.
- **Đề xuất:** giữ migration ID gốc khi di chuyển migration sang Infrastructure; cập nhật namespace/DbContext attribute/snapshot theo project mới. Nếu đã có DB áp ID mới, xác định rõ các trạng thái history và phương án tương thích trước khi đổi tiếp; không dùng xóa DB làm cách sửa mặc định cho một đợt refactor.
- **Kiểm chứng sau sửa:** thêm regression test bắt đầu với schema + history của `aba1859`, có dữ liệu mẫu; bản mới migrate thành công, không tạo lại bảng và giữ dữ liệu. Giữ cả test tạo DB trắng.
- **Cơ sở:** [EF Core lưu các migration đã áp trong migration history](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/history-table).

### CA-02 — Hash token vẫn sửa được qua tham chiếu đầu vào và public getter

- **Mức độ:** Trung bình (P2).
- **Vị trí:** `backend/src/Dayloom.Domain/Auth/RefreshToken.cs:16,37–55`.
- **Vấn đề:** `private set` chỉ chặn gán lại property. `Create` giữ nguyên mảng `tokenHash` của caller, còn getter trả chính mảng đó. Sau khi entity được tạo hợp lệ, code bên ngoài vẫn sửa được danh tính hash mà không đi qua phương thức Domain. Ví dụ cả `inputHash[0] = ...` và `token.TokenHash[1] = ...` đều đổi dữ liệu bên trong token. Kiểm tra độ dài không bảo vệ tính bất biến của nội dung.
- **Bằng chứng:** probe gọi public API trên DLL hiện tại ghi nhận `INPUT_ALIAS_MUTATES_HASH=true` và `PUBLIC_GETTER_MUTATES_HASH=true`; không dùng reflection hoặc EF Entry để sửa entity.
- **Đề xuất:** sao chép mảng khi nhận vào và không trả mảng nội bộ có thể ghi. Có thể dùng private backing field cùng getter trả defensive copy, cấu hình EF truy cập field để vẫn giữ cột `bytea` và khả năng query hash. Không cần đưa EF vào Domain hoặc đổi schema cho cách này.
- **Kiểm chứng sau sửa:** sửa mảng đầu vào và dữ liệu nhận từ getter không đổi hash entity; bổ sung EF round-trip/lookup theo hash để bảo đảm cách bọc dữ liệu không làm hỏng persistence.

### CA-03 — Collection “read-only” trả thẳng List, cho phép xóa lịch sử token

- **Mức độ:** Trung bình (P2).
- **Vị trí:** `backend/src/Dayloom.Domain/Auth/Session.cs:11,25`; quan hệ required/cascade trong `RefreshTokenConfiguration.cs:13–16`.
- **Vấn đề:** property khai báo `IReadOnlyCollection<RefreshToken>` nhưng runtime object vẫn là `_refreshTokens` kiểu `List<RefreshToken>`. Caller cast sang `ICollection<RefreshToken>` hoặc `List<RefreshToken>` rồi Add/Remove/Clear được. Điều này lách các phương thức Domain; với entity đang được EF track, xóa khỏi navigation còn có thể xóa hàng token khỏi DB. Giữ lịch sử token đã dùng là điều kiện của reuse detection trong DESIGN §2.
- **Bằng chứng:** probe EF lưu Session với token gốc đã dùng và token con, clear change tracker rồi Include navigation. Cast collection, `Remove(root)` và `SaveChanges()` thành công: `ROOT_EXISTS=false`, `CHILD_EXISTS=true`. Đây là thay đổi hàng DB qua public collection, không dùng SQL xóa trực tiếp hay setter riêng tư. Probe này xác nhận cơ chế xóa lịch sử; chưa phải test một cuộc tấn công qua HTTP.
- **Đề xuất:** trả wrapper thực sự không sửa được, ví dụ `_refreshTokens.AsReadOnly()`, trong khi EF vẫn dùng backing field `_refreshTokens`. Chốt field access trong mapping nếu cần để getter bảo vệ Domain không cản materialization. Không cung cấp thao tác xóa lịch sử token qua API Domain thông thường trong thời gian giữ lịch sử.
- **Kiểm chứng sau sửa:** mutation qua giá trị public không thay đổi aggregate; đọc/lưu token qua EF vẫn đúng và token tổ tiên vẫn còn sau các thay đổi được phép. Thêm regression test Include → thử sửa collection → SaveChanges → kiểm tra lịch sử.
- **Cơ sở:** [EF collection navigation/backing field và defensive copy](https://learn.microsoft.com/en-us/ef/core/modeling/relationships/navigations), [xóa orphan khi cắt quan hệ required](https://learn.microsoft.com/en-us/ef/core/change-tracking/relationship-changes#orphan-delete-timing).

### CA-04 — Phương thức Domain cho phép trạng thái thời gian trái thứ tự

- **Mức độ:** Trung bình (P2).
- **Vị trí:** `backend/src/Dayloom.Domain/Auth/Session.cs:44–60`; `backend/src/Dayloom.Domain/Auth/RefreshToken.cs:27–34`.
- **Vấn đề:** `IsActive` chỉ xét revoked/expiry, không xét `now >= CreatedAt`; `Revoke` không kiểm tra mốc tạo; `MarkUsed` chỉ kiểm tra đã dùng, không kiểm tra mốc tạo/hết hạn. Bằng public API hiện tại có thể tạo token trước khi Session tồn tại, ghi revocation trước ngày tạo và đánh dấu sử dụng token trước ngày tạo hoặc ngay khi token đã hết hạn. Lớp Domain chưa bảo đảm trạng thái hợp lệ như ARCHITECTURE.md mô tả.
- **Bằng chứng:** với mốc tạo `2026-11-02T02:00:00Z`, probe gọi `IssueRefreshToken(..., now - 1 phút)`, `Revoke(now - 1 phút)`, `MarkUsed(CreatedAt - 1 phút)` và `MarkUsed(ExpiresAt)`; tất cả được chấp nhận. Probe xác nhận cả bốn quan hệ thời gian sai. Không kết luận endpoint sẽ chấp nhận token hết hạn vì endpoint chưa có.
- **Đề xuất:** kiểm tra biên thời gian tại các phương thức thay đổi trạng thái: issue/use không trước CreatedAt, use phải trước ExpiresAt, revocation mới không trước CreatedAt. `IsActive` cần phản ánh cả đầu khoảng sống của Session. Giữ chính sách Revoke idempotent đã chốt. Nếu một phương thức cố ý chỉ ghi dấu sau khi use case đã kiểm tra, thu hẹp/ghi rõ hợp đồng đó thay vì coi public Domain API đã tự bảo vệ đầy đủ.
- **Kiểm chứng sau sửa:** unit tests tại `CreatedAt - 1`, đúng CreatedAt, ngay trước expiry và đúng expiry; xác nhận input sai không làm thay đổi state. Không cần endpoint hoặc Docker cho các test này.

### CA-05 — ArchitectureTests bỏ lọt framework ngoài danh sách prefix

- **Mức độ:** Trung bình (P2).
- **Vị trí:** `backend/tests/Dayloom.UnitTests/ArchitectureTests.cs:17–23,39–46`.
- **Vấn đề:** test Domain chỉ cấm sáu prefix cố định. Một phụ thuộc `Microsoft.Extensions.Logging.Abstractions`, DI hoặc một framework/package khác không trùng prefix vẫn pass, dù AGENTS quy định Domain không tham chiếu framework. `GetReferencedAssemblies()` cũng không kiểm tra toàn bộ các item ProjectReference/PackageReference/FrameworkReference đã khai báo trong `.csproj`; một reference chưa được compiler phát ra thành assembly reference có thể không xuất hiện. Các test hiện tại chặn được một số vi phạm, chưa bảo đảm quy tắc tổng quát được tài liệu hứa hẹn.
- **Bằng chứng:** dùng Roslyn có sẵn trong SDK compile một assembly giả trong bộ nhớ, có field `Microsoft.Extensions.Logging.ILogger`; đưa assembly này qua chính helper `AssertNoReference` hiện tại cho cả sáu prefix Domain: tất cả pass. Assembly thực sự có reference `Microsoft.Extensions.Logging.Abstractions`. Control với field `DbContext` bị chính helper từ chối đúng như dự kiến. Không thêm dependency hoặc sửa DLL/project Domain thật.
- **Đề xuất:** kiểm tra `.csproj` Domain không có ProjectReference/PackageReference/FrameworkReference trái quy tắc, kết hợp allowlist runtime/BCL phù hợp cho assembly dependency; kiểm tra các cạnh project của bốn tầng theo bảng ARCHITECTURE.md. Giữ phép kiểm tra assembly cho dependency thực sự được dùng. Thêm control test cố tình đưa một framework ngoài blacklist hiện tại vào đầu vào checker và yêu cầu checker từ chối.
- **Kiểm chứng sau sửa:** checker từ chối cả EF và logging/framework khác, cùng một ProjectReference/PackageReference bị cấm dù chưa dùng trong source; graph hiện tại vẫn pass. Không cần cài thư viện architecture test mới để làm các kiểm tra này.
- **Cơ sở:** [Assembly.GetReferencedAssemblies phản ánh assembly references](https://learn.microsoft.com/en-us/dotnet/api/system.reflection.assembly.getreferencedassemblies?view=net-10.0).

## Những điểm đã kiểm chứng đúng

| Hạng mục | Kết quả |
|---|---|
| Project references | Domain không có ProjectReference/PackageReference; Application → Domain; Infrastructure → Application; Api → Application + Infrastructure. Không thấy cạnh ngược trong các `.csproj` hiện tại. |
| Domain và framework | Hai entity Domain chỉ dùng kiểu BCL; `AppUser : IdentityUser<Guid>` và EF configuration nằm ở Infrastructure. Chưa thấy framework leak thật trong Domain hiện tại; CA-05 là lỗ hổng của hàng rào test. |
| EF trong Application | `IAppDbContext` dùng DbSet và Application tham chiếu EF Core đúng đánh đổi đã chốt trong ARCHITECTURE.md; không coi đây là vi phạm cần sửa. Không thấy Application dùng Npgsql, ASP.NET hoặc Infrastructure. |
| Composition root | Program gọi `AddApplication`/`AddInfrastructure`; Infrastructure đăng ký cùng scoped AppDbContext cho `IAppDbContext`. Health dùng cơ chế health check thay cho API truy cập thẳng concrete DbContext. |
| Thời hạn Session/token hợp lệ | `Session.Start` tính 14 ngày, chuẩn hóa UTC; `IssueRefreshToken` lấy expiry từ Session; chặn Session revoked/expired. Các biên còn thiếu nằm ở CA-04. |
| Private setter và backing field | Probe lưu rồi clear tracker, query `Include("RefreshTokens")`: Id, UserId, CreatedAt, AbsoluteExpiresAt, RevokedAt, UsedAt, SessionId và nội dung hash đều đúng. Metadata navigation nhận field `_refreshTokens`; không cần public setter để EF đọc dữ liệu. |
| Schema tạo bảng/index/CHECK/FK | Hai file migration chính giống nhau sau chuẩn hóa namespace, gồm cả Up/Down. Model/snapshot hiện tại không có pending model changes theo integration test. Không thấy thay đổi physical schema do việc tách project; vấn đề nâng cấp là migration identity tại CA-01. |

## Kiểm thử đã chạy và kết quả thật

| Lệnh / phép kiểm chứng | Kết quả |
|---|---|
| `cd backend && dotnet test` | Exit 0, unit **24 passed / 0 failed / 0 skipped** (124 ms); integration **20 passed / 0 failed / 0 skipped** (13 giây). Tổng **44/44**; duration là số VSTest báo, không phải tổng thời gian restore/build. |
| Đối chiếu file migration tại `aba1859` bằng `git show` với file Infrastructure hiện tại | `MigrationEqualExceptNamespace=true`, mỗi bản 211 dòng. Designer chỉ ra hai migration ID khác nhau nêu ở CA-01. |
| Probe EF round-trip bằng DLL đã build và DayloomApiFactory | `EF_ROUND_TRIP_PRIVATE_SETTERS_AND_NAVIGATION=true; FIELD=_refreshTokens`. |
| Probe mutation collection trên entity được load/tracked | `PUBLIC_COLLECTION_REMOVES_PERSISTED_HISTORY=true; ROOT_EXISTS=false; CHILD_EXISTS=true`. |
| Probe nâng cấp: cùng schema, history mang ID baseline | Pending migration là ID mới; Migrate thất bại có kiểm soát với `42P07`, `relation "users" already exists`. |
| Probe hash và thời gian qua public Domain API | Sửa hash qua input/getter thành công; token có thể có CreatedAt trước Session; revocation/use trước CreatedAt và use đúng ExpiresAt được chấp nhận. |
| Probe ArchitectureTests với assembly tổng hợp trong RAM | Logging leak pass cả sáu check Domain; control EF leak bị reject. Harness cuối exit 0. |
| `git diff --exit-code HEAD` trước tạo báo cáo | Exit 0; không có sửa đổi file đã track. |

Các probe dùng F# Interactive/Roslyn có sẵn trong SDK, nhận code qua stdin; không tạo file helper hoặc thêm test/dependency vào repo. Probe persistence dùng factory tạo PostgreSQL Testcontainers riêng, không DB dev. Vì DDL đã đối chiếu giống baseline, việc đặt migration history về ID cũ trong DB review mô phỏng đúng trường hợp nâng cấp schema baseline; không sửa history trên DB có sẵn của người chủ. Container PostgreSQL review được dispose trong `finally`, log xác nhận xóa container. Các lần chuẩn bị harness chưa chạy hoàn chỉnh đã được sửa tham chiếu/tên cột history và chạy lại; kết quả ở bảng là các phép kiểm chứng hoàn chỉnh, không phải lỗi build/test của repository.

## Điểm còn thiếu / đã biết khi bàn giao

- Suite hiện có chủ yếu kiểm tra tạo/lưu entity và các ràng buộc DB; chưa có regression test reload đầy đủ private setter/navigation hoặc chặn các alias công khai nêu trên. Probe xác nhận mapping hiện tại đúng, nhưng nên đưa read-back test vào suite khi xử lý CA-02/CA-03.
- Fresh-database tests và `HasPendingModelChanges()` không bảo vệ khả năng nâng cấp migration history; cần trường hợp baseline → bản mới của CA-01.
- `IAppDbContext` hiện cung cấp DbSet/SaveChanges, chưa có hợp đồng transaction/khóa Session. Khi triển khai use case Auth cần cung cấp cơ chế phù hợp ở ranh giới Application/Infrastructure để giữ transaction trong DESIGN §2; không yêu cầu viết endpoint trong review này và không tính phần chưa triển khai thành một phát hiện riêng.
- Không kiểm thử register/login/refresh/logout, cookie, JWT hoặc HTTP authorization; các tính năng đó chưa thuộc đợt chuyển đổi. Không yêu cầu MediatR, repository cho từng entity hoặc đổi quyết định EF trong Application.
- Không xác nhận CI GitHub của repo private; các kết quả trên là chạy local với Docker.
- Chỉ tạo `docs/reviews/2026-10-30-clean-architecture-review.md`; không sửa code, project, migration, test, AGENTS hay PROGRESS; không commit/push. Claude Code xử lý phát hiện và cập nhật PROGRESS theo phân vai.
