# Review khung dự án Dayloom — 2026-10-30

## Phạm vi và phiên bản bàn giao

- Reviewer: Codex. Claude Code phụ trách xử lý phát hiện.
- Nhánh: `main`.
- Commit được review: `b51541e65bad8219b7a7d2d5de298b6a752f2a83` — `chore: scaffold ASP.NET Core API, React frontend, dev database and CI`.
- Đã đọc `AGENTS.md`, `docs/DESIGN.md` và nhật ký “Ngày 1, phần 3” trong `docs/PROGRESS.md`.
- Phạm vi code: `Program.cs`, `AppDbContext`, endpoint/test health, `DayloomApiFactory`, `Directory.Build.props`, project/launch settings, `vite.config.ts`, `features/health`, màn hình health, `docker-compose.yml`, `.github/workflows/ci.yml`, `.gitignore` và cấu hình liên quan.
- Working tree ban đầu sạch; code được chạy khớp commit trên. Đây là review khung Hello API, không yêu cầu triển khai Auth, migration nghiệp vụ, PWA hoặc phục vụ frontend production ở bước này.

## Kết luận

Các lệnh kiểm tra trong `AGENTS.md` đều chạy thành công sau khi cấp quyền truy cập cần thiết cho Docker/NuGet. Backend **1/1 test pass**, frontend **4/4 test pass**, lint và production build pass. API trực tiếp và qua Vite proxy hoạt động.

Có **2 phát hiện mức Trung bình (P2)**, không có phát hiện mức Cao trong phạm vi đã kiểm tra. Chưa xác nhận được CI trên GitHub; kết quả local không thay thế kết quả runner Ubuntu.

## Phát hiện

### SCAFFOLD-01 — DB chỉ phục vụ dev nhưng được publish trên mọi interface

- **Mức độ:** Trung bình (P2).
- **Vị trí:** `docker-compose.yml:11–12`.
- **Vấn đề:** mapping `5432:5432` mở cổng trên mọi interface, trong khi API chạy trên máy dev và chỉ cần truy cập DB qua localhost. Máy khác có đường mạng tới máy dev có thể thử kết nối DB nếu firewall cho phép. Khi bổ sung Auth, cùng DB sẽ chứa user/session/token hash, làm tăng tác động của cấu hình này. Đây là phơi bày cổng ngoài nhu cầu, không phải bằng chứng DB đã bị truy cập trái phép hoặc truy cập được từ Internet.
- **Bằng chứng thực tế:** `docker compose ps` trả `0.0.0.0:5432->5432/tcp, [::]:5432->5432/tcp`; container `dayloom-db-1` healthy.
- **Đề xuất:** đổi mapping dev thành `127.0.0.1:5432:5432`; nếu cần IPv6 loopback thì khai báo riêng. Chỉ mở interface khác khi có nhu cầu rõ ràng. Không thay đổi volume hoặc xóa dữ liệu để sửa mapping.
- **Kiểm chứng sau sửa:** Compose chỉ bind loopback; API localhost và integration tests vẫn pass. Testcontainers dùng container/port riêng, không phụ thuộc mapping cố định này.
- **Cơ sở:** [Docker: published ports mặc định trên mọi interface và cách bind localhost](https://docs.docker.com/get-started/docker-concepts/running-containers/publishing-ports/).

### SCAFFOLD-02 — Connection string rỗng vượt qua kiểm tra fail-fast

- **Mức độ:** Trung bình (P2).
- **Vị trí:** `backend/src/Dayloom.Api/Program.cs:8–13`.
- **Vấn đề:** toán tử `?? throw` chỉ chặn `null`, không chặn chuỗi rỗng/whitespace. Một biến môi trường hoặc đối số cấu hình rỗng có thể ghi đè user-secrets nhưng API vẫn khởi động. Lỗi cấu hình chỉ xuất hiện khi gọi DB; điều này trái mục tiêu từ chối khởi động khi thiếu connection string đã ghi trong code/nhật ký.
- **Tái hiện không sửa file:** chạy tại project API: `dotnet run --no-build -- --urls http://localhost:5081 --ConnectionStrings:Default=`. Log xác nhận API vẫn listen cổng 5081. Gọi `/api/health` nhận HTTP 503 với `{"status":"degraded","database":"unreachable"}`.
- **Đề xuất:** kiểm tra `string.IsNullOrWhiteSpace(connectionString)` trước khi đăng ký `AppDbContext`, dùng thông báo lỗi hiện có và không log giá trị secret. Đây là kiểm tra cấu hình bắt buộc; không yêu cầu startup phải kết nối DB thành công hoặc thay thế nhánh health 503 khi DB tạm mất kết nối.
- **Kiểm chứng sau sửa:** cấu hình thiếu/rỗng/whitespace đều dừng startup với thông báo không chứa secret; cấu hình hợp lệ vẫn chạy và health trả 200. Khi DB không truy cập được nhưng connection string không rỗng, health vẫn trả 503 theo hợp đồng.

## Kiểm thử và kiểm tra đã chạy

Môi trường: Windows, .NET SDK `10.0.403`, runtime `10.0.14`, Node `22.20.0`, npm `10.9.3`, Docker Engine `29.5.3`. Dependency frontend đã có sẵn; không chạy `npm ci` hoặc cài thêm thư viện trong phiên review.

| Lệnh / phép kiểm tra | Thư mục / đầu vào | Kết quả thực tế |
|---|---|---|
| `docker info --format '{{.ServerVersion}}'` | Gốc | PASS, Docker `29.5.3`. |
| `docker compose ps` | Gốc | DB đã chạy từ trước phiên review, healthy; ghi nhận bind cổng ở SCAFFOLD-01. |
| `docker compose up -d` | Gốc | PASS, exit 0; `Container dayloom-db-1 Running`. |
| `dotnet test` | `backend` | PASS sau khi chạy với quyền cần thiết; 1 passed, 0 failed, 0 skipped; restore/build thành công với `TreatWarningsAsErrors`. |
| `dotnet run` | `backend/src/Dayloom.Api` | PASS, chạy Development tại `http://localhost:5080` bằng cấu hình hiện có, không in user-secrets. |
| `npm.cmd run dev` | `frontend` | PASS, Vite khởi động tại `http://localhost:5173/`. |
| `npm.cmd run lint` | `frontend` | PASS, exit 0, oxlint không báo lỗi/cảnh báo. |
| `npm.cmd test` | `frontend` | PASS, 1 test file, 4/4 test pass. |
| `npm.cmd run build` | `frontend` | PASS, `tsc -b` và Vite production build thành công. |
| GET `http://localhost:5080/api/health` | HTTP thật | 200, `{"status":"ok","database":"ok"}`. |
| GET `http://localhost:5173/api/health` | HTTP thật qua Vite | 200, cùng JSON; xác nhận đường gọi tương đối `/api` và proxy hoạt động. |
| GET `http://localhost:5173/` | HTTP thật | 200, HTML có tiêu đề Dayloom. Không coi đây là test render UI trong browser. |
| `dotnet test --no-build --environment 'ConnectionStrings__Default=Host=127.0.0.1;Port=1;Database=dayloom;Username=review_invalid;Password=review-placeholder;Timeout=1'` | `backend`; giá trị giả, chỉ áp dụng cho process test | PASS, 1/1. Kiểm chứng bổ sung cho nghi ngờ factory dùng nhầm cấu hình môi trường: connection string cố tình sai không phá test, override của factory vẫn có hiệu lực trong trường hợp này. |
| API với connection string rỗng, cổng 5081 | Lệnh tái hiện SCAFFOLD-02 | API vẫn khởi động; `curl.exe --silent --show-error --include --max-time 10 http://localhost:5081/api/health` trả 503 và JSON degraded. Đây là bằng chứng phát hiện, không phải lỗi của lệnh test suite. |
| `git ls-files --error-unmatch .env` | Gốc | Exit 1 đúng dự kiến: `.env` không được track. |
| `git check-ignore` cho `.env`, env frontend, `appsettings.Development.local.json` | Gốc | Các đường dẫn đều được ignore. |
| Kiểm tra tracked files và tìm mẫu secret | Commit được review | Không thấy secret thật trong phạm vi đọc/đối chiếu; các kết quả về mật khẩu là placeholder ở `.env.example`/README và tham chiếu biến môi trường ở Compose. Không đọc/in nội dung `.env` hay user-secrets. |
| `git diff --exit-code HEAD` | Sau kiểm thử, trước tạo báo cáo | PASS, exit 0; không thay đổi file đã track. |

Các lỗi môi trường đã gặp và xử lý: sandbox ban đầu chặn Docker named pipe và đọc `NuGet.Config`; đã chạy lại bằng quyền phù hợp và lấy kết quả pass ở trên. Lần mở API bằng terminal tương tác bị hệ điều hành chặn tạo process; chạy lại bằng PowerShell hệ thống thành công. Các lỗi này không được tính là lỗi code.

## Nhận xét trong phạm vi và giới hạn bàn giao

- `DayloomApiFactory` khởi tạo PostgreSQL Testcontainers thật; test health pass, kể cả phép kiểm tra override cấu hình môi trường nêu trên. Chưa có bằng chứng factory dùng nhầm DB dev trong trường hợp đã thử. Chưa có schema Auth để kiểm tra migration, dữ liệu giữa các test hoặc tranh chấp refresh; không xem đó là thiếu sót của commit Hello API.
- `Directory.Build.props` áp dụng `TreatWarningsAsErrors` cho backend; build qua `dotnet test` pass. Backend không ghi connection string vào mã nguồn; OpenAPI chỉ map trong Development.
- Vite proxy và đường gọi `/api/health` cùng origin phù hợp hướng Auth đã chốt. Chưa có middleware Auth/CSRF, cookie hoặc JWT để kiểm thử; không yêu cầu thêm vào commit này.
- `.github/workflows/ci.yml`: không thấy lỗi cấu hình rõ ràng trong hai job đã đọc. `defaults.run.working-directory` áp dụng cho bước `run`; `global-json-file: global.json` của action vẫn trỏ file gốc, không phải lỗi thiếu `../`. Cache npm trỏ đúng lock file. Không chạy lại nguyên workflow trên Linux trong phiên này.
- Đã thử đọc CI status của đúng commit bằng GitHub connector; API trả 404. Không thể phân biệt commit chưa có trên remote với giới hạn quyền truy cập chỉ từ kết quả này; **chưa xác nhận CI xanh hoặc đỏ trên GitHub**. Không trigger workflow, commit hoặc push.
- Chưa chạy browser E2E hoặc kiểm thử cookie/Auth; chưa có các tính năng đó. Nhánh backend 503 đã kiểm tra tay bằng cấu hình rỗng, chưa có integration test tự động tương ứng trong commit hiện tại.
- Đã dừng hai API và Vite do phiên review khởi động: Ctrl+C chỉ kết thúc process gọi lệnh, nên đã xác minh PID/đường dẫn của ba process con và dùng `Stop-Process` để dừng đúng chúng. Giữ DB dev đang chạy vì nó đã tồn tại trước phiên review; không chạy `docker compose down`, không xóa volume. Build/test chỉ sinh output được Git ignore.
- File bàn giao duy nhất được thêm: `docs/reviews/2026-10-30-scaffold-review.md`. Không sửa source, cấu hình hay tài liệu khác. Claude Code xử lý hai phát hiện và cập nhật `PROGRESS.md` theo phân vai; người chủ tự commit/push.
