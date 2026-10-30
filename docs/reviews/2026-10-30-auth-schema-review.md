# Review schema Auth — 2026-10-30

## Phạm vi và phiên bản bàn giao

- Reviewer: Codex; Claude Code phụ trách xử lý phát hiện.
- Nhánh: `feat/auth`.
- Base `main`: `422d68b1f2278da9eba7660178a403137e2cb4a8`.
- Commit được review: `11b3bc56e9d4382f9993f813b5630cedca3e6958` — `feat(auth): add user, session and refresh token schema`; đây là commit duy nhất trong `git log main..feat/auth` tại thời điểm review.
- Đã đọc `AGENTS.md`, nhật ký “Ngày 1, phần 4” trong `docs/PROGRESS.md` và `docs/DESIGN.md`, đặc biệt §2.
- Phạm vi: `Features/Auth/AppUser.cs`, `Session.cs`, `RefreshToken.cs`, `AppDbContext`, migration `20261029211522_InitialAuthSchema` cùng designer/snapshot, `AuthSchemaTests`, `DayloomApiFactory` và cấu hình liên quan.
- Working tree ban đầu sạch và khớp commit. Không review hoặc yêu cầu triển khai endpoint register/login/refresh/logout trong lần này.

## Kết luận

`cd backend && dotnet test`: **PASS — 9 passed, 0 failed, 0 skipped**, exit 0; test duration báo bởi VSTest là 13 giây. Restore/build thành công, không cần sửa code để chạy test.

Schema hiện tại **có đủ cột và index để triển khai luồng §2**: tìm token theo hash, khóa Session, nhận diện token đã dùng, giữ lịch sử token và thu hồi cả family qua Session. Không thấy thiếu cột hoặc index bắt buộc làm chặn các luồng này.

Có **3 phát hiện mức Trung bình (P2)** về tính chính xác của test và ràng buộc dữ liệu. Không xác nhận lỗ hổng endpoint hoặc lỗi tranh chấp refresh vì các phần đó chưa tồn tại.

## Phát hiện

### AUTH-SCHEMA-01 — Test email trùng có thể pass nhờ index username

- **Mức độ:** Trung bình (P2).
- **Vị trí:** `backend/tests/Dayloom.Api.Tests/Features/Auth/AuthSchemaTests.cs:25–30`, helper `NewUser` tại dòng 89–95.
- **Vấn đề:** hai user đều được tạo bằng `NewUser("same@example.com")`, nên cùng trùng cả `NormalizedEmail` và `NormalizedUserName`. Test chỉ đòi một `DbUpdateException`; unique index username cũng đáp ứng assertion. Vì vậy test không bảo vệ được thay đổi quan trọng là unique index email: bỏ nhầm index này khỏi migration vẫn không nhất thiết làm test đỏ.
- **Bằng chứng thực tế:** trên DB review riêng đã áp SQL sinh từ migration hiện tại, tạm `DROP INDEX ix_users_normalized_email` trong transaction rồi chèn hai hàng có cùng dữ liệu username/email như test. PostgreSQL vẫn từ chối bằng SQLSTATE `23505`, constraint `ix_users_normalized_user_name`. Sau rollback, thử hai username khác nhau nhưng cùng normalized email bị từ chối đúng bởi `ix_users_normalized_email`. Index email hiện tại đúng; vấn đề nằm ở khả năng phát hiện regression của test.
- **Đề xuất:** dùng hai username/normalized username khác nhau nhưng cùng normalized email. Assert `DbUpdateException.InnerException` là `PostgresException`, `SqlState == PostgresErrorCodes.UniqueViolation` và `ConstraintName == "ix_users_normalized_email"`. Các test hash/CHECK cũng nên định danh lỗi dự kiến để FK hoặc lỗi không liên quan không khiến test pass. Dùng dữ liệu hợp lệ làm control.
- **Kiểm chứng sau sửa:** test email phải fail nếu unique index email bị thiếu, dù unique username còn nguyên. Bổ sung test cho CHECK `expires_at > created_at` của refresh token với mốc bằng nhau/trước nhau; hiện có CHECK nhưng chưa có case tự động cho nó.
- **Cơ sở:** [Npgsql PostgresException: thông tin SqlState/ConstraintName](https://www.npgsql.org/doc/api/Npgsql.PostgresException.html).

### AUTH-SCHEMA-02 — Email bắt buộc của MVP chưa được ràng buộc NOT NULL

- **Mức độ:** Trung bình (P2).
- **Vị trí:** `backend/src/Dayloom.Api/Features/Auth/AppUser.cs:24–30`; migration `InitialAuthSchema.cs:22–25`.
- **Vấn đề:** `email` và `normalized_email` vẫn nullable theo mặc định Identity, trong khi MVP chỉ có tài khoản đăng ký bằng email/mật khẩu. Unique index hiện tại chỉ bảo vệ các normalized email có giá trị; PostgreSQL cho phép nhiều NULL. Một đường ghi thiếu chuẩn hóa có thể lưu hai hàng cùng email với `NormalizedEmail = NULL`, hoặc lưu user không có email. Đây là thiếu bảo vệ tại DB, chưa phải bằng chứng luồng đăng ký tương lai có lỗi.
- **Bằng chứng thực tế:** trên migration thật, chèn hai user có username khác nhau, cùng `email = 'same@example.com'`, `normalized_email = NULL` thành công; truy vấn đếm được 2 hàng. Chèn thêm một user có cả email/normalized email NULL cũng thành công. Các phép thử đã rollback.
- **Đề xuất:** với phạm vi tài khoản email hiện tại, cấu hình `Email` và `NormalizedEmail` là required, cập nhật migration/snapshot bằng EF và thêm test DB từ chối NULL cho từng trường. Giữ unique index trên normalized email. Việc chuẩn hóa đúng giá trị vẫn cần được thực hiện qua Identity ở lớp ứng dụng; NOT NULL không thay thế normalizer. Nếu muốn giữ khả năng tài khoản không email, cần ghi rõ ngoại lệ đó thay vì coi schema đang bảo đảm yêu cầu email bắt buộc.
- **Kiểm chứng sau sửa:** các hàng thiếu email hoặc thiếu normalized email bị từ chối đúng cột/SQLSTATE `23502`; hai tài khoản hợp lệ khác email vẫn lưu được, email trùng bị đúng unique index từ chối.
- **Cơ sở:** [PostgreSQL: unique index mặc định cho phép nhiều NULL](https://www.postgresql.org/docs/current/ddl-constraints.html#DDL-CONSTRAINTS-UNIQUE-CONSTRAINTS).

### AUTH-SCHEMA-03 — CHECK Session chỉ bảo đảm thời hạn dương, chưa bảo đảm giới hạn 14 ngày

- **Mức độ:** Trung bình (P2).
- **Vị trí:** `backend/src/Dayloom.Api/Features/Auth/Session.cs:19–20,36–38`; migration `InitialAuthSchema.cs:55`.
- **Vấn đề:** entity ghi “Hard limit (login + 14 days)” và DESIGN §2 chốt thời hạn tuyệt đối 14 ngày, nhưng CHECK chỉ có `absolute_expires_at > created_at`. DB chấp nhận thời hạn 15 ngày hoặc dài hơn. Nếu một đường ghi tính sai hạn Session, kiểm tra Session lúc refresh sau này sẽ tin vào mốc quá dài đã lưu. Đây là khoảng trống toàn vẹn dữ liệu, không phải kết luận hiện có cơ chế gia hạn phiên sai.
- **Bằng chứng thực tế:** chèn Session từ `2026-11-02T02:00:00Z` đến `2026-11-17T02:00:00Z` thành công; DB trả `accepted_session_lifetime_days = 15`. Phép thử đã rollback.
- **Đề xuất:** bổ sung ràng buộc tại hàng Session cho giới hạn đã chốt: `absolute_expires_at <= created_at + interval '336 hours'` cùng CHECK thời hạn dương hiện tại; nếu muốn DB bắt buộc đúng 14 ngày thì dùng phép bằng. Dùng số giờ để biểu diễn 14×24 giờ tuyệt đối. Thêm test mốc 14 ngày hợp lệ và vượt giới hạn bị từ chối; không cần thêm endpoint để kiểm thử ràng buộc này.
- **Kiểm chứng sau sửa:** Session 14 ngày được lưu; 14 ngày cộng một đơn vị thời gian được DB hỗ trợ và 15 ngày bị từ chối đúng constraint. Test Session hết hạn trước/ngay khi tạo vẫn phải bị từ chối.

## Đối chiếu schema với luồng refresh/reuse/logout

| Nhu cầu trong DESIGN §2 | Schema hiện tại | Đánh giá trong phạm vi schema |
|---|---|---|
| Tra token từ hash cookie | `token_hash bytea NOT NULL`, unique index `ix_refresh_tokens_token_hash`, CHECK đúng 32 byte | Đủ; lookup không cần scan theo user hoặc lưu raw token. |
| Tìm và khóa family | `RefreshToken.SessionId` NOT NULL + FK; `sessions.id` là PK UUID | Đủ cho `SELECT ... FROM sessions WHERE id = @sid FOR UPDATE`. Không cần cột RowVersion để dùng khóa hàng theo thiết kế. |
| Phát hiện dùng lại | `used_at` nullable, hash cũ vẫn nằm trong bảng và liên kết với Session | Đủ; không bắt buộc có `ReplacedByTokenId` hoặc `FamilyId` riêng khi family chính là Session. |
| Thu hồi tất cả token | `sessions.revoked_at` nullable, mọi token có FK Session | Đủ để tra trạng thái Session trước khi phát token; schema tự nó không thực thi logic thu hồi khi chỉ đọc một token. |
| Logout bằng token tổ tiên | Token cũ giữ `SessionId` và hash | Đủ nếu lớp ứng dụng giữ lịch sử như DESIGN; không cần đánh dấu revoked riêng từng token. |
| Tra token của Session/user, cascade | Index `refresh_tokens.session_id`, `sessions.user_id`; FK user → Session → token đều cascade | Đủ cho phạm vi hiện tại; cascade đã được test bằng `ExecuteDeleteAsync`, không chỉ dựa vào EF xóa entity đang track. |
| Timestamp UTC | `DateTimeOffset`, migration dùng `timestamp with time zone` | Phù hợp; giá trị ghi qua driver vẫn phải tuân theo quy ước UTC. |
| Thời hạn tuyệt đối | Có đầy đủ `CreatedAt`, `AbsoluteExpiresAt`, `ExpiresAt` | Đủ cột; giới hạn Session tại DB còn thiếu như AUTH-SCHEMA-03. |

Không đề xuất thêm index dọn dẹp theo ngày hết hạn hoặc ràng buộc “một token chưa dùng mỗi Session” làm điều kiện bắt buộc của commit này. Thuật toán khóa hàng Session và kiểm tra UsedAt có thể giữ quy tắc rotation mà không cần những bổ sung đó. Tính đúng của transaction/tranh chấp phải được kiểm chứng ở bước triển khai logic, chưa thể suy ra từ 9 test schema/startup/health.

Một giới hạn đã kiểm chứng thêm: DB cho lưu token có `ExpiresAt` sau `Session.AbsoluteExpiresAt`. Việc này không tự kéo dài phiên nếu luôn kiểm tra hạn Session như DESIGN. Quy tắc token con hết hạn cùng Session cần được giữ khi tạo token từ Session đã khóa; hiện schema không bảo đảm quan hệ thời hạn xuyên hai bảng. Không đề xuất dùng CHECK có subquery sang bảng Session vì [PostgreSQL không hỗ trợ CHECK tham chiếu hàng/bảng khác theo cách đó](https://www.postgresql.org/docs/current/ddl-constraints.html#DDL-CONSTRAINTS-CHECK-CONSTRAINTS). Đây là giới hạn cần biết khi bàn giao, không được tính thêm thành một lỗi endpoint hoặc yêu cầu viết endpoint trong commit schema.

## Kiểm thử đã chạy

| Lệnh / phép kiểm tra | Kết quả thực tế |
|---|---|
| `git log main..feat/auth`, `git rev-parse HEAD main feat/auth` | Xác định đúng commit/base nêu ở đầu báo cáo. |
| `cd backend && dotnet test` | PASS, 9/9: 6 AuthSchemaTests, 2 StartupTests và 1 HealthEndpointTests; build/restore thành công. Chạy với quyền truy cập NuGet/Docker cần thiết. |
| `dotnet ef migrations script --no-build --project backend/src/Dayloom.Api` | PASS; sinh SQL từ migration hiện tại, không tạo/sửa file migration. |
| Áp SQL vào PostgreSQL `postgres:18-alpine` review riêng | PASS, tạo các bảng/index và ghi migration history thành công. |
| Bỏ index email trong transaction rồi thử dữ liệu test email trùng | Vẫn nhận unique violation từ index username: xác nhận AUTH-SCHEMA-01. Đã rollback. |
| Email trùng, username khác nhau trên schema nguyên bản | Nhận đúng constraint `ix_users_normalized_email`: control xác nhận index đang có hoạt động. Đã rollback. |
| Cùng email, normalized email NULL; user không có email | Đều được DB chấp nhận: xác nhận AUTH-SCHEMA-02. Đã rollback. |
| Session 15 ngày | Được DB chấp nhận: xác nhận AUTH-SCHEMA-03. Đã rollback. |
| Token hết hạn sau Session | Được DB chấp nhận; ghi nhận giới hạn quan hệ thời hạn, không suy diễn thành lỗi vượt hạn Session ở endpoint. Đã rollback. |
| `git diff --exit-code HEAD` trước tạo báo cáo | PASS, working tree không có sửa đổi file đã track. |

Các phép SQL bổ sung chạy qua stdin bằng `psql -X -v ON_ERROR_STOP=1` trong container `dayloom-schema-review-11b3bc56`, tạo bằng image có sẵn với `--pull=never --network none --rm`, không publish cổng và không gắn volume dev. Không truy vấn/sửa dữ liệu DB dev hoặc đọc/in user-secrets. Container review đã được xác minh nhãn rồi dừng để tự dọn; không tạo file helper hoặc test trong repository.

## Điểm còn thiếu / đã biết khi bàn giao

- `Migrations_are_in_sync_with_the_model` kiểm tra model với snapshot; factory dùng `MigrateAsync` trên DB thật nên đồng thời kiểm tra migration có áp được. Hai việc này không tự chứng minh mọi index/CHECK đúng ý nghĩa; cần các test định danh constraint như AUTH-SCHEMA-01.
- `DayloomApiFactory` cấp context tới DB container, chạy migration trước test; không thấy lỗi chặn phạm vi schema hiện tại. Các test hiện tại dùng dữ liệu hợp lệ hoặc transaction SaveChanges thất bại được rollback, có dữ liệu riêng theo GUID; chưa thấy phụ thuộc thứ tự test trong suite đã chạy.
- Chưa kiểm thử endpoint, cookie, JWT, khóa hàng giữa hai request hoặc refresh/logout cạnh tranh vì chưa có logic đó. Không tính các phần này là thiếu tính năng của commit schema; không yêu cầu triển khai chúng để đóng review này.
- Không kiểm tra lại CI GitHub vì repo private và nhật ký đã ghi agent không truy cập được; kết quả pass ở đây là chạy local với Docker.
- Chỉ tạo `docs/reviews/2026-10-30-auth-schema-review.md`; không sửa source, migration, test hay tài liệu khác; không commit/push. Claude Code xử lý phát hiện và cập nhật `PROGRESS.md` theo phân vai.
