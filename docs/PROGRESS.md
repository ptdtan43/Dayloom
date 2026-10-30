# Tiến độ dự án Dayloom

> Chỉ Claude Code sửa file này. Codex ghi báo cáo vào `docs/reviews/`. Mới nhất ở trên cùng.

## Đang làm
| Agent | Việc | File / khu vực | Bắt đầu |
|---|---|---|---|
| (trống) | | | |

## Việc tiếp theo
- [ ] Người chủ: commit + push nhánh `refactor/clean-architecture`, xác nhận CI xanh
- [ ] Codex review chuyển đổi Clean Architecture
- [ ] Người chủ: mở Pull Request `refactor/clean-architecture` → `main` (nền móng: Clean Architecture + schema auth), merge sau khi CI xanh và review xong
- [ ] Auth: use case + endpoint register / login / refresh / logout theo `DESIGN.md` §2 (nhánh mới từ `main`)

## Nhật ký
### 2026-10-30 — Ngày 1, phần 6 (Claude Code): chuyển sang Clean Architecture
- Người chủ chọn **Clean Architecture cố định** cho backend (để học và đưa vào CV). Nhánh `refactor/clean-architecture`, tách từ `feat/auth` (`aba1859`).
- Tách 4 project: `Dayloom.Domain` (không tham chiếu gì) ← `Dayloom.Application` ← `Dayloom.Infrastructure` ← `Dayloom.Api`.
  - Domain: `Session`, `RefreshToken` thành rich domain model (`private set`, `Session.Start`, `IsActive`, `Revoke`, `IssueRefreshToken`, `MarkUsed`). Token hết hạn **cùng lúc** với phiên: đóng khoảng trống mà DB không ép được (ghi nhận ở phần 5).
  - Application: `IAppDbContext`, `AddApplication()`.
  - Infrastructure: `AppUser` (Identity), `AppDbContext`, cấu hình bảng, migration (`Persistence/Migrations`), `AddInfrastructure()`, health check `AddDbContextCheck`.
  - Api: `Program.cs` chỉ ráp các tầng; `/api/health` dùng `MapHealthChecks` (giữ nguyên hợp đồng JSON với frontend).
- Migration `InitialAuthSchema` sinh lại trong Infrastructure. So với bản cũ: **chỉ khác dòng namespace**, schema giữ nguyên.
- Test tách 2 project: `Dayloom.UnitTests` (24 test: domain + architecture test, **0.4 giây**, không cần DB) và `Dayloom.IntegrationTests` (đổi tên từ `Dayloom.Api.Tests`, 20 test). Thêm test health trả 503 khi DB không kết nối được. `dotnet test`: **44/44 pass**.
- Sự cố phát hiện khi chạy thật: chuỗi kết nối dev dùng `Host=localhost` → Windows thử IPv6 `::1` trước, mà DB chỉ bind `127.0.0.1` (SCAFFOLD-01) → **mỗi kết nối mất ~2 giây** (đo: localhost 2040 ms, 127.0.0.1 0 ms). Sửa user-secrets và README sang `Host=127.0.0.1`. Health sau khi khởi động: 72–231 ms.
- Viết `docs/ARCHITECTURE.md` (kiến trúc, lý do, câu hỏi phỏng vấn). AGENTS.md cập nhật quy tắc tầng và lệnh migration mới.

## Nhật ký (phần 4–5)
### 2026-10-30 — Ngày 1, phần 5 (Claude Code)
- Xử lý `docs/reviews/2026-10-30-auth-schema-review.md` (commit `11b3bc5`), đồng ý cả 3 phát hiện:
  - AUTH-SCHEMA-01: test email trùng trước đây pass nhờ index username. Viết lại: hai username khác nhau, cùng email. **Mọi test từ chối giờ kiểm tra đúng `SqlState` + tên constraint** (helper `AssertRejectedAsync`). Thêm test CHECK `expires_at > created_at` của refresh token, và test đối chứng với dữ liệu hợp lệ.
  - AUTH-SCHEMA-02: `email`, `normalized_email` → NOT NULL (unique index của PostgreSQL cho qua nhiều NULL).
  - AUTH-SCHEMA-03: thêm CHECK `ck_sessions_max_lifetime`: `absolute_expires_at <= created_at + interval '336 hours'` (dùng giờ, không dùng ngày, để không lệch khi đổi DST). Test: đúng 14 ngày → OK; +1 micro giây, +1 ngày → bị chặn.
- Sinh lại migration `InitialAuthSchema` thay vì thêm migration mới: nhánh chưa merge, chưa có DB nào ngoài dev/test.
- Ghi nhận (không sửa): DB không ép `RefreshToken.ExpiresAt <= Session.AbsoluteExpiresAt` vì CHECK của PostgreSQL không tham chiếu được bảng khác → code tạo token phải đảm bảo, và có test ở bước endpoint.
- `dotnet test`: **18/18 pass**. Migration mới đã áp vào DB dev.

### 2026-10-30 — Ngày 1, phần 4 (Claude Code)
- Xử lý `docs/reviews/2026-10-30-scaffold-review.md`, đồng ý cả 2 phát hiện:
  - SCAFFOLD-01: DB dev chỉ bind `127.0.0.1:5432`. Đã kiểm tra: `docker compose ps` → `127.0.0.1:5432->5432/tcp`.
  - SCAFFOLD-02: connection string rỗng / chỉ khoảng trắng → không khởi động. Thêm `StartupTests` (2 case). `dotnet test`: **3/3 pass**.
- Auth bước 1 (schema): `AppUser` (Identity, thêm `TimeZoneId`, `CreatedAt`), `Session`, `RefreshToken`; `IdentityUserContext` (không có bảng role); tên bảng/cột snake_case (`EFCore.NamingConventions`); migration `InitialAuthSchema`; `dotnet-ef` cài làm local tool (`dotnet-tools.json`).
  - Tự phát hiện thêm: Identity mặc định tạo index email **không unique** → thêm unique index `ix_users_normalized_email`.
  - Ràng buộc DB: hash token unique + đúng 32 byte, `expires > created`, xoá user thì xoá cascade session và token.
  - `AuthSchemaTests` (6 test) + factory tự chạy migration.
- Sự cố: ổ C: đầy (0 GB) giữa phiên → Docker Desktop sập. Xử lý: người chủ chuyển disk image Docker sang `D:\Docker\DockerDesktopWSL`; cache npm chuyển sang `D:\.caches\npm`. Cache NuGet **giữ nguyên trên C:** (biến `NUGET_PACKAGES` đã đặt rồi gỡ) để không ảnh hưởng project C# ở trường khi build offline. Ổ C: còn 7.37 GB.
- Sau sự cố: `dotnet test` **9/9 pass**. Migration `InitialAuthSchema` đã áp vào DB dev (7 bảng).

## Nhật ký (phần 1–3)
### 2026-10-30 — Ngày 1, phần 3 (Claude Code): dựng khung dự án
- Cài .NET SDK 10.0.403, `global.json` cố định phiên bản. `git init` nhánh `main`.
- Backend: `Dayloom.slnx`, `Dayloom.Api` (EF Core + Npgsql 10.0.3), endpoint `GET /api/health` kiểm tra kết nối DB (200 / 503). Thiếu connection string → app không khởi động. `TreatWarningsAsErrors` bật.
- Test backend: `DayloomApiFactory` chạy API thật với PostgreSQL 18 qua Testcontainers 4.16. `dotnet test`: **1/1 pass**.
- Dev DB: `docker-compose.yml` (postgres:18-alpine, project `dayloom`), mật khẩu trong `.env` (git-ignore), connection string trong user-secrets.
- Frontend: Vite 8 + React 19 + TypeScript 7 + Tailwind 4 + Vitest 5 + oxlint (theo template Vite mới nhất). Trang chủ tiếng Việt hiển thị trạng thái kết nối. `npm test`: **4/4 pass**; lint sạch; build OK.
- Kiểm tra tay toàn tuyến: `localhost:5173/api/health` (qua proxy Vite) → `200 {"status":"ok","database":"ok"}`.
- CI: `.github/workflows/ci.yml`, 2 job backend / frontend. **Chưa chạy trên GitHub** vì chưa có remote.
- README hướng dẫn chạy trên máy.

## Nhật ký (cũ)
### 2026-10-30 — Ngày 1, phần 2 (Claude Code)
- Xử lý review thiết kế lần 2 của Codex (`docs/reviews/2026-10-30-design-review.md`, 12 phát hiện). Đồng ý cả 12, tất cả được giải quyết trong `docs/DESIGN.md` (mỗi mục ghi mã phát hiện):
  - AUTH-01, AUTH-05: chốt **một origin duy nhất** khi deploy (ASP.NET Core phục vụ cả API và React build) → §0. CSRF: chỉ nhận JSON + kiểm tra `Origin`.
  - AUTH-02: refresh nguyên tử bằng cập nhật có điều kiện; nhánh dùng lại commit thu hồi trước khi trả 401.
  - AUTH-03: single-flight trong tab + Web Locks giữa các tab. Chấp nhận đánh đổi: mất response sau rotate → đăng nhập lại.
  - AUTH-04: Session = family, thời hạn tuyệt đối 14 ngày; logout thu hồi cả Session. Chấp nhận: JWT cũ còn dùng được tối đa 15 phút.
  - AUTH-06: hợp đồng JWT đầy đủ (iss/aud/sub/sid/jti, skew 30s, thiếu khoá → không khởi động).
  - TIME-01..03: múi giờ hồ sơ là chuẩn duy nhất; ngày giữ dạng chuỗi; hai nhánh truy vấn; CHECK đầy đủ; không cộng 24h.
  - AC-01: bảng quy tắc nghiệp vụ cụ thể. AC-02: render Markdown an toàn (§4). AC-03: PWA/offline (§5).
- Codex xác nhận: 8/12 đã giải quyết. 4 điểm còn lại đã sửa: khoá hàng Session (`FOR UPDATE`) cho refresh/logout (AUTH-02, AUTH-04); JWT tối đa 15 phút 30 giây sau logout (AUTH-04); `IsAllDay NOT NULL` (TIME-03); lockout chỉ chặn đăng nhập mới, khác vô hiệu hoá tài khoản (AC-01).
- Quyết định: dừng review tài liệu ở đây. Rủi ro còn lại cần kiểm chứng bằng code và test, review tiếp theo của Codex là review code.

### 2026-10-30 — Ngày 1 (Claude Code)
- Chốt tên **Dayloom**, giao diện tiếng Việt, PostgreSQL.
- Xử lý review của Codex (5 điểm, đồng ý cả 5):
  1. Chốt phạm vi MVP và tiêu chí nghiệm thu → `docs/DESIGN.md` §1.
  2. Thiết kế auth: JWT tự phát + refresh token rotation, không dùng `MapIdentityApi` → `DESIGN.md` §2.
  3. Quy ước thời gian UTC / `DateOnly` / `[Start, End)` → `DESIGN.md` §3.
  4. CI chuyển lên tuần 1; cố định .NET 10 bằng `global.json`; thêm Definition of Done → `AGENTS.md`.
  5. Quy trình bàn giao; chỉ Claude Code sửa `PROGRESS.md`, Codex ghi vào `docs/reviews/` → `AGENTS.md`.
- Kiểm tra môi trường: .NET SDK 8.0 / 9.0 (chưa có 10), Node 22.20, Git 2.50, Docker 29.5 (daemon chưa chạy).

### 2026-10-29 — Ngày 0 (Claude Code)
- Thống nhất ý tưởng: PWA lịch trình + to-do + note, hướng Fullstack.
- Chốt stack: React + TS + ASP.NET Core + EF Core + PostgreSQL.
- Tạo `AGENTS.md`, `CLAUDE.md`, `docs/PROGRESS.md`.

## Quyết định kỹ thuật
| Ngày | Quyết định | Lý do |
|---|---|---|
| 2026-10-29 | Backend dùng ASP.NET Core thay vì Node/NestJS | Người chủ học .NET ở trường, khớp hướng tuyển dụng |
| 2026-10-29 | Làm PWA thay vì web + app riêng | Một codebase, cài được lên điện thoại |
| 2026-10-29 | Tự viết backend, không dùng BaaS (Supabase) | Mục tiêu CV Fullstack: cần chứng minh thiết kế API, auth, DB |
| 2026-10-30 | Database: PostgreSQL | Miễn phí, phổ biến, chạy tốt với Docker và EF Core (Npgsql) |
| 2026-10-30 | Tên dự án: Dayloom; giao diện tiếng Việt | "Weave your day" khớp ý tưởng gộp lịch, việc, ghi chú |
| 2026-10-30 | .NET 10 LTS, cố định bằng `global.json` | .NET 8 và 9 đều hết hỗ trợ tháng 11/2026 |
| 2026-10-30 | JWT tự phát + refresh token trong cookie HttpOnly, không dùng `MapIdentityApi` | Token của `MapIdentityApi` không phải JWT chuẩn; tự làm thì kiểm soát được rotation/thu hồi và giải thích được khi phỏng vấn (góp ý của Codex) |
| 2026-10-30 | Thời điểm lưu UTC (`timestamptz`), ngày dùng `date`, khoảng `[Start, End)` | Tránh lệch ngày, sẵn sàng cho sự kiện lặp lại (góp ý của Codex) |
| 2026-10-30 | Integration test dùng Testcontainers PostgreSQL | Test truy vấn/migration trên DB thật thay vì in-memory |
| 2026-10-30 | Phân vai: Claude Code xây dựng, Codex review và viết test | Có góc nhìn độc lập để bắt lỗi |
| 2026-10-30 | Deploy một origin: ASP.NET Core phục vụ cả API và frontend | Cookie SameSite=Strict hoạt động, không cần CORS, giảm CSRF, một container dễ deploy (AUTH-01) |
| 2026-10-30 | Refresh token: Session tuyệt đối 14 ngày, rotation nguyên tử, dùng lại → thu hồi cả Session | RFC 9700; chống đánh cắp token (AUTH-02, AUTH-04) |
| 2026-10-30 | Frontend thời gian: `date-fns` + `@date-fns/tz`, ngày giữ dạng chuỗi | Tránh lệch ngày do JS `Date` (TIME-02) |
| 2026-10-30 | Markdown: `react-markdown` không `rehype-raw`, không ảnh, có CSP | Chống stored XSS (AC-02) |
| 2026-10-30 | ~~Backend một project, chia thư mục theo tính năng~~ → thay bằng dòng dưới | |
| 2026-10-30 | **Clean Architecture cố định** (Domain / Application / Infrastructure / Api), vertical slice bên trong | Người chủ muốn học vững; chuyển khi mới ~10 file là rẻ nhất. Chi tiết: `docs/ARCHITECTURE.md` |
| 2026-10-30 | `AppUser` ở Infrastructure; Domain chỉ biết `UserId` | Domain không được phụ thuộc ASP.NET Identity |
| 2026-10-30 | Rich domain model (`private set` + phương thức có kiểm tra) | Không tạo được dữ liệu sai từ code; quy tắc test được không cần DB |
| 2026-10-30 | `IAppDbContext` thay vì repository cho từng entity | Ít code thừa; đánh đổi: Application tham chiếu gói EF Core (chỉ abstraction) |
| 2026-10-30 | Không dùng MediatR; mỗi use case một class handler | MediatR thương mại hoá từ 2025; ít "ma thuật", dễ debug |
| 2026-10-30 | Chuỗi kết nối dev dùng `127.0.0.1`, không dùng `localhost` | DB chỉ bind IPv4; `localhost` thử IPv6 trước, mất 2 giây mỗi kết nối |
| 2026-10-30 | PostgreSQL 18 (dev, test, CI cùng image `postgres:18-alpine`) | Bản ổn định mới nhất; test và dev giống nhau |
| 2026-10-30 | Giữ nguyên toolchain của template Vite (TS 7, oxlint) + Vitest 5 | Mặc định mới nhất của Vite, nhanh; không tự cấu hình ESLint |
| 2026-10-30 | Connection string dev trong user-secrets, mật khẩu DB trong `.env` | Không commit secret (AGENTS.md) |

## Lộ trình
1. Tuần 1: monorepo, Docker, CI, DB schema, auth
2. Tuần 2: To-do (API + UI)
3. Tuần 3: Lịch (API + UI)
4. Tuần 4: Note + PWA cài đặt được + deploy MVP
5. Tuần 5+: sự kiện lặp lại, nhắc việc, offline sync (AI, thống kê nếu còn hứng)
