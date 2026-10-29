# Tiến độ dự án Dayloom

> Chỉ Claude Code sửa file này. Codex ghi báo cáo vào `docs/reviews/`. Mới nhất ở trên cùng.

## Đang làm
| Agent | Việc | File / khu vực | Bắt đầu |
|---|---|---|---|
| (trống) | | | |

## Việc tiếp theo
- [ ] Người chủ: commit fix review (lên `main`) + schema auth (nhánh `feat/auth`)
- [ ] Codex review schema auth
- [ ] Người chủ: xác nhận CI xanh trên tab Actions (repo private, agent không xem được)
- [ ] Auth: endpoint register / login / refresh / logout theo `DESIGN.md` §2, kèm test

## Nhật ký
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
| 2026-10-30 | Backend một project, chia thư mục theo tính năng | Dự án nhỏ; ít tầng thì dễ hiểu, tách Clean Architecture khi thật sự cần |
| 2026-10-30 | PostgreSQL 18 (dev, test, CI cùng image `postgres:18-alpine`) | Bản ổn định mới nhất; test và dev giống nhau |
| 2026-10-30 | Giữ nguyên toolchain của template Vite (TS 7, oxlint) + Vitest 5 | Mặc định mới nhất của Vite, nhanh; không tự cấu hình ESLint |
| 2026-10-30 | Connection string dev trong user-secrets, mật khẩu DB trong `.env` | Không commit secret (AGENTS.md) |

## Lộ trình
1. Tuần 1: monorepo, Docker, CI, DB schema, auth
2. Tuần 2: To-do (API + UI)
3. Tuần 3: Lịch (API + UI)
4. Tuần 4: Note + PWA cài đặt được + deploy MVP
5. Tuần 5+: sự kiện lặp lại, nhắc việc, offline sync (AI, thống kê nếu còn hứng)
