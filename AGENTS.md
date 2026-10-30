# Dayloom — hướng dẫn chung cho mọi AI agent (Claude Code, Codex)

File này là nguồn sự thật duy nhất về cách làm việc. `CLAUDE.md` chỉ trỏ về đây.
Thiết kế kỹ thuật (phạm vi MVP, auth, quy ước thời gian) nằm ở `docs/DESIGN.md`.

## Mục tiêu dự án
**Dayloom** (*Weave your day*) là ứng dụng web + PWA quản lý lịch trình hằng ngày, to-do list và ghi chú. Đây là dự án cá nhân của một sinh viên .NET, dùng để đưa vào CV ứng tuyển vị trí **Fullstack**. Vì vậy:
- Code phải đủ sạch để người chủ giải thích được khi phỏng vấn. Ưu tiên rõ ràng hơn "thông minh".
- Mọi quyết định kỹ thuật quan trọng ghi vào `docs/PROGRESS.md` (mục Quyết định).
- Người chủ vừa làm vừa học (biết React ở mức cơ bản): khi thêm khái niệm mới, giải thích ngắn gọn tại sao.
- **Giao diện người dùng bằng tiếng Việt.** Code, tên biến, commit message, API bằng tiếng Anh.

## Tech stack (đã chốt)
| Lớp | Công nghệ |
|---|---|
| Frontend | React + TypeScript + Vite + Tailwind (PWA) |
| Backend | ASP.NET Core Web API, C#, **.NET 10 (LTS)**, cố định bằng `global.json` |
| ORM | Entity Framework Core + Npgsql |
| DB | PostgreSQL (chạy bằng Docker khi dev) |
| Auth | ASP.NET Identity (lưu user, hash mật khẩu) + JWT tự phát + refresh token. **Không dùng `MapIdentityApi`** vì token của nó không phải JWT chuẩn. Chi tiết: `docs/DESIGN.md` |
| Realtime / Job | SignalR / Hangfire (sau MVP) |
| Hạ tầng | Docker, GitHub Actions (CI có từ tuần 1) |
| Test | xUnit + Testcontainers (PostgreSQL thật) cho BE; Vitest + Playwright cho FE |

Phiên bản thư viện được cố định khi khởi tạo (lock file, `global.json`). Không nâng cấp phiên bản khi chưa ghi lý do.

## Kiến trúc backend: Clean Architecture (cố định)
Backend **bắt buộc** theo Clean Architecture, chi tiết và lý do ở `docs/ARCHITECTURE.md`. Tóm tắt quy tắc khi viết code:
- **Domain:** entity + quy tắc nghiệp vụ. Không tham chiếu project hay framework nào (không EF Core, không ASP.NET). Thuộc tính `private set`, thay đổi qua phương thức có kiểm tra (rich domain model).
- **Application:** use case (mỗi use case một class handler, **không dùng MediatR**) + interface cho thứ cần từ bên ngoài. Truy cập DB qua `IAppDbContext`. Không tham chiếu Infrastructure, ASP.NET, Npgsql.
- **Infrastructure:** cài đặt interface của Application: EF Core, Identity (`AppUser`), JWT, cấu hình bảng, migration.
- **Api:** endpoint HTTP + `Program.cs` ráp các tầng. Không chứa logic nghiệp vụ.
- Bên trong mỗi tầng chia thư mục theo tính năng (`Auth/`, `Tasks/`...).
- Architecture test (`tests/Dayloom.UnitTests/ArchitectureTests.cs`) phải luôn pass.
- **Migration đã push thì không bao giờ đổi tên, sinh lại hay xoá.** EF chỉ nhận ra migration bằng ID; đổi ID là DB đã áp sẽ chạy lại và lỗi. Muốn sửa schema thì thêm migration mới, và thêm ID vào `ShippedMigrations` trong `tests/Dayloom.IntegrationTests/Persistence/MigrationHistoryTests.cs`.

## Cấu trúc thư mục
```
backend/
  Dayloom.slnx
  src/Dayloom.Domain/             # entity + quy tắc nghiệp vụ
  src/Dayloom.Application/        # use case + interface
  src/Dayloom.Infrastructure/     # EF Core, Identity, migration (Persistence/Migrations)
  src/Dayloom.Api/                # endpoint, Program.cs
  tests/Dayloom.UnitTests/        # Domain/Application/architecture, không cần DB
  tests/Dayloom.IntegrationTests/ # API thật + PostgreSQL Testcontainers (DayloomApiFactory)
frontend/                         # React + Vite; code chia theo tính năng: src/features/<tên>/
docs/                             # ARCHITECTURE.md, DESIGN.md, PROGRESS.md, reviews/
docker-compose.yml                # PostgreSQL cho dev (mật khẩu trong .env)
.github/workflows/ci.yml
```

## Lệnh thường dùng
| Việc | Lệnh |
|---|---|
| Bật DB | `docker compose up -d` (ở thư mục gốc) |
| Chạy API | `cd backend/src/Dayloom.Api && dotnet run` → http://localhost:5080 |
| Test backend | `cd backend && dotnet test` (integration test cần Docker) |
| Chỉ unit test | `cd backend && dotnet test tests/Dayloom.UnitTests` (không cần Docker, dưới 1 giây) |
| Tạo migration | `dotnet ef migrations add <Tên> --project backend/src/Dayloom.Infrastructure --startup-project backend/src/Dayloom.Api --output-dir Persistence/Migrations` (ở thư mục gốc; lần đầu chạy `dotnet tool restore`) |
| Áp migration vào DB dev | `dotnet ef database update --project backend/src/Dayloom.Infrastructure --startup-project backend/src/Dayloom.Api` |
| Chạy frontend | `cd frontend && npm run dev` → http://localhost:5173 (proxy `/api` sang 5080) |
| Kiểm tra frontend | `cd frontend && npm run lint && npm test && npm run build` |

Backend bật `TreatWarningsAsErrors`: cảnh báo cũng làm build thất bại.

## Phân vai giữa hai agent
- **Claude Code:** thiết kế kiến trúc, schema, lập kế hoạch; viết tính năng **kèm test của chính tính năng đó**, giải thích để người chủ học. Là agent **duy nhất** sửa `docs/PROGRESS.md`.
- **Codex:** review chéo code do Claude Code viết; viết thêm test độc lập, tìm edge case, kiểm tra bảo mật. **Không sửa `docs/PROGRESS.md`**: báo cáo ghi vào `docs/reviews/YYYY-MM-DD-<chu-de>.md`.
- Việc lặp, đã rõ yêu cầu: bên nào được giao thì làm.
- Hai agent không sửa cùng một file cùng lúc. Trước khi sửa code, kiểm tra bảng "Đang làm" trong `docs/PROGRESS.md`.

## Quy trình bàn giao (handoff)
Mỗi lượt bàn giao giữa hai agent phải ghi đủ:
1. **Phạm vi:** tính năng / file đã thay đổi.
2. **Phiên bản được review:** commit hash hoặc khoảng commit (`abc123..def456`).
3. **Kiểm thử đã chạy:** lệnh và kết quả (pass/fail).
4. **Điểm còn thiếu / đã biết:** những gì chưa làm hoặc còn nghi ngờ.

Phát hiện của Codex do Claude Code xử lý (hoặc người chủ quyết định bỏ qua), rồi ghi kết quả vào `PROGRESS.md`.

## Tiêu chí hoàn thành một tính năng (Definition of Done)
- Build thành công, CI xanh.
- Có test cho logic nghiệp vụ và **test phân quyền** (user A không đọc/sửa/xoá được dữ liệu user B).
- Truy vấn / migration quan trọng được test với PostgreSQL thật (Testcontainers).
- Luồng chính có ít nhất một test Playwright (từ khi có UI).
- Đạt các tiêu chí nghiệm thu của module trong `docs/DESIGN.md`.

## Quy tắc chung
1. **Đầu phiên:** đọc `docs/PROGRESS.md` và `docs/DESIGN.md`.
2. **Git: người chủ tự commit và push.** Agent **không** chạy `git commit` / `git push`. Khi đến lúc commit, agent báo cho người chủ: tên nhánh, các file cần add, commit message theo Conventional Commits (`feat:`, `fix:`, `docs:`, `test:`, `chore:`). Commit nhỏ, mỗi commit một ý. Remote: `https://github.com/ptdtan43/Dayloom.git`.
3. Không commit secret (`.env`, connection string, khoá JWT). Dùng `.env.example` và user-secrets.
4. Không thêm thư viện lớn khi chưa nêu lý do trong `docs/PROGRESS.md`.
