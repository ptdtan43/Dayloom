# Thiết kế kỹ thuật Dayloom

Tài liệu này chốt những quyết định phải có **trước khi viết schema và code**. Thay đổi ở đây cần ghi vào mục Quyết định của `PROGRESS.md`.
Mã trong ngoặc vuông (ví dụ `[AUTH-02]`) trỏ tới phát hiện tương ứng trong `docs/reviews/2026-10-30-design-review.md`.

## 0. Mô hình triển khai

**Một origin duy nhất.** Khi deploy, ASP.NET Core vừa phục vụ API (`/api/*`) vừa phục vụ file tĩnh của React đã build (`wwwroot`, fallback về `index.html`). Tất cả nằm trong một container Docker, một domain HTTPS. `[AUTH-01]`

- Khi dev, Vite dev server proxy `/api` sang backend, nên trình duyệt cũng chỉ thấy một origin.
- Hệ quả: cookie `SameSite=Strict` hoạt động, không cần CORS, rủi ro CSRF thấp.
- Nếu sau này buộc phải tách domain: đây là thay đổi thiết kế, phải viết lại §2.

## 1. Phạm vi MVP

### Trong MVP
- Đăng ký, đăng nhập, đăng xuất, tự khôi phục phiên khi tải lại trang.
- CRUD việc cần làm, sự kiện lịch, ghi chú. Dữ liệu cá nhân, mỗi user chỉ thấy dữ liệu của mình.
- Giao diện tiếng Việt, responsive.
- PWA cài đặt được (manifest, icon, service worker cache app shell). Không sửa dữ liệu khi offline.

### Ngoài MVP
Sửa offline + đồng bộ, push notification / nhắc việc, sự kiện lặp lại, kéo-thả, chia sẻ, rich text editor, ảnh trong ghi chú, đổi mật khẩu / quên mật khẩu, trợ lý AI, thống kê, realtime.

### Quy tắc nghiệp vụ `[AC-01]`

**Auth**
| Hạng mục | Quy tắc |
|---|---|
| Email | Identity chuẩn hoá (`NormalizedEmail`), unique index ở DB. Trùng → 409, UI: "Email này đã được đăng ký." |
| Mật khẩu | Tối thiểu 8 ký tự, tối đa 128. Không bắt buộc ký tự đặc biệt (theo hướng dẫn NIST: độ dài quan trọng hơn độ phức tạp) |
| Lockout | Sai 5 lần liên tiếp → khoá **đăng nhập** 15 phút. Đăng nhập thành công thì reset bộ đếm. Đang khoá → 423, UI báo thời gian còn lại. Lockout chỉ chặn đăng nhập mới: **các Session đã có vẫn refresh bình thường** (khác với tài khoản bị vô hiệu hoá, xem §2 Đăng xuất) |
| Lỗi đăng nhập | Sai email hoặc sai mật khẩu đều trả cùng một thông báo: "Email hoặc mật khẩu không đúng." |
| Khôi phục phiên | Tải trang → gọi `/api/auth/refresh`. 401 → màn đăng nhập. Lỗi mạng / 5xx → màn lỗi có nút "Thử lại", **không** đăng xuất |

**Việc cần làm (Tasks)**
| Hạng mục | Quy tắc |
|---|---|
| Tiêu đề | Bắt buộc, trim khoảng trắng, 1–200 ký tự |
| Mô tả | Không bắt buộc, tối đa 2.000 ký tự |
| Ưu tiên | `Low` / `Medium` / `High` (UI: Thấp / Trung bình / Cao), mặc định `Medium` |
| Hạn chót | `DateOnly`, không bắt buộc |
| Quá hạn | `DueDate < Hôm nay` **và** chưa hoàn thành. Task không có hạn không bao giờ quá hạn |
| Hôm nay | `DueDate = Hôm nay`, gồm cả task đã xong; kết hợp được với bộ lọc trạng thái |
| "Hôm nay" là ngày nào | Tính theo múi giờ hồ sơ của user (§3) |
| Sắp xếp mặc định | Chưa xong trước → hạn chót tăng dần, không có hạn ở cuối → ưu tiên Cao→Thấp → `CreatedAt` tăng dần |
| Sắp xếp theo ưu tiên | Ưu tiên Cao→Thấp → hạn chót tăng dần (không hạn ở cuối) → `CreatedAt` |

**Lịch (Events)**
| Hạng mục | Quy tắc |
|---|---|
| Tiêu đề | Bắt buộc, 1–200 ký tự. Mô tả tối đa 2.000 |
| Loại | Có giờ **hoặc** cả ngày. Được đổi loại khi sửa; trường của loại kia bị xoá |
| Có giờ | `EndAt > StartAt`, tối đa 14 ngày |
| Cả ngày | `EndDate > StartDate` (EndDate không bao gồm), tối đa 366 ngày |
| Xem | Theo ngày và theo tuần, tuần bắt đầu thứ Hai |
| Qua nửa đêm | 23:00–01:00 hiện ở cả hai ngày; 23:00–00:00 chỉ hiện ngày đầu |

**Ghi chú (Notes)**
| Hạng mục | Quy tắc |
|---|---|
| Nội dung | Markdown source, tối đa 50.000 ký tự. Phải có tiêu đề hoặc nội dung (không được rỗng cả hai). Tiêu đề tối đa 200 |
| Hiển thị | Xem §4 (render an toàn) |
| Tìm kiếm | Chuỗi con, không phân biệt hoa thường, không phân biệt dấu (`unaccent` của PostgreSQL, `đ` ↔ `d`). Chuẩn hoá NFC trước khi lưu và trước khi tìm. Query rỗng/chỉ khoảng trắng = không lọc. Tối đa 100 ký tự |
| Thứ tự | Ghim trước (kể cả khi đang tìm) → `UpdatedAt` giảm dần |

## 2. Auth và quyền sở hữu dữ liệu

### Hợp đồng access token (JWT) `[AUTH-06]`
| Thành phần | Quyết định |
|---|---|
| Thuật toán | HS256, chỉ chấp nhận HS256 |
| Claims | `iss=dayloom`, `aud=dayloom-api`, `sub`=userId (GUID), `sid`=sessionId, `iat`, `exp`, `jti` |
| Thời hạn | 15 phút, clock skew cho phép 30 giây |
| Khoá ký | Ngẫu nhiên ≥ 256 bit, chỉ ở backend (user-secrets khi dev, biến môi trường khi deploy). **Thiếu khoá hoặc khoá ngắn → app không khởi động** |
| Xác thực | Middleware `JwtBearer` chuẩn: kiểm tra chữ ký, thuật toán, issuer, audience, lifetime. Tắt claim mapping mặc định (`MapInboundClaims = false`) để `sub` giữ nguyên tên |
| Lưu ở frontend | Trong bộ nhớ (biến JS), không localStorage |

### Phiên đăng nhập (Session) và refresh token
Mỗi lần đăng nhập tạo một **Session** (chính là "family" của các refresh token). `[AUTH-04]`

```
Session:      Id, UserId, CreatedAt, AbsoluteExpiresAt (= CreatedAt + 14 ngày), RevokedAt
RefreshToken: Id, SessionId, TokenHash (SHA-256), CreatedAt, ExpiresAt, UsedAt
```

| Quy tắc | Chi tiết |
|---|---|
| Sinh token | 256 bit từ `RandomNumberGenerator` (CSPRNG). DB chỉ lưu hash |
| Thời hạn | **Tuyệt đối 14 ngày tính từ lúc đăng nhập.** Rotation không gia hạn. Token con và cookie hết hạn cùng lúc với Session |
| Cookie | `__Host-dayloom_rt`, `HttpOnly; Secure; SameSite=Strict; Path=/`, `Expires` = `AbsoluteExpiresAt`. Tiền tố `__Host-` bắt buộc host-only + Secure |
| Giữ lịch sử | Token đã dùng được giữ đến khi Session hết hạn, để phát hiện dùng lại. Job dọn dẹp xoá Session hết hạn quá 7 ngày (sau MVP) |

### Refresh nguyên tử `[AUTH-02]`
**Nguyên tắc:** mọi thao tác thay đổi một Session (refresh, phát hiện dùng lại, logout) đều **khoá hàng Session trước** bằng `SELECT ... FROM Sessions WHERE Id = @sid FOR UPDATE` và giữ khoá đến khi commit. Nhờ vậy các thao tác trên cùng Session chạy tuần tự: refresh không thể kiểm tra Session "còn sống" rồi commit sau khi logout đã thu hồi nó.

Refresh, trong **một transaction**:
1. Tìm token theo hash (không khoá) để lấy `SessionId`. Không thấy → 401.
2. **Khoá hàng Session** (`FOR UPDATE`). Session đã thu hồi hoặc hết hạn → commit, 401.
3. Token **đã dùng** (`UsedAt IS NOT NULL`) → đây là dùng lại: `UPDATE Sessions SET RevokedAt = now()`, **commit**, rồi trả 401. (Commit trước khi trả lỗi để việc thu hồi không bị rollback.)
4. Token hết hạn → commit, 401.
5. Đánh dấu `UsedAt = now()`, tạo đúng **một** token con, commit, rồi mới set cookie và trả access token mới.

Hai request đồng thời với cùng token: request thứ hai chờ khoá, đến lượt thì thấy token đã dùng và rơi vào bước 3.

### Refresh phía frontend `[AUTH-03]`
- **Trong một tab:** mọi request nhận 401 dùng chung một promise refresh (single-flight). Mỗi request chỉ thử lại tối đa một lần.
- **Giữa các tab:** bọc refresh bằng Web Locks API (`navigator.locks.request('dayloom-refresh', ...)`). Tab thứ hai chờ tab đầu xong; vì trình duyệt dùng chung cookie, tab thứ hai gửi token mới, không bị coi là dùng lại.
- `/api/auth/login`, `/refresh`, `/logout` **không** đi qua interceptor refresh.
- Lỗi mạng / 5xx ≠ token hỏng: không đăng xuất, cho thử lại.
- **Đánh đổi được chấp nhận:** nếu server đã rotate nhưng response bị mất trên đường về, lần refresh sau sẽ bị coi là dùng lại và user phải đăng nhập lại. Hiếm gặp, đổi lại quy tắc chống dùng lại được giữ nghiêm ngặt.

### Đăng xuất `[AUTH-04]`
- `POST /api/auth/logout`: tìm Session từ refresh token trong cookie (kể cả token đã dùng), **khoá hàng Session** (`FOR UPDATE`, như refresh), thu hồi **cả Session**, commit, xoá cookie (cùng tên, cùng Path). Không cần access token. Gọi lặp lại vẫn trả 204 (idempotent).
- Frontend: xoá access token, xoá toàn bộ cache dữ liệu (TanStack Query), tăng "thế hệ phiên" để bỏ qua kết quả refresh đang bay từ trước logout.
- **Đánh đổi được chấp nhận:** access token đã phát vẫn dùng được tối đa **15 phút 30 giây** sau logout (thời hạn 15 phút + clock skew 30 giây). Không kiểm tra Session ở mỗi request để giữ API đơn giản.
- Tài khoản bị **vô hiệu hoá** (khác lockout tạm thời ở §1): refresh từ chối phát token mới. MVP chưa có tính năng vô hiệu hoá tài khoản; quy tắc ghi sẵn cho sau này.

### Chống CSRF `[AUTH-05]`
Áp dụng cho mọi endpoint thay đổi trạng thái, đặc biệt `/api/auth/*`:
1. Chỉ chấp nhận `Content-Type: application/json` (form từ site khác không gửi được JSON mà không qua preflight, và không có CORS nên preflight thất bại).
2. Kiểm tra header `Origin` phải đúng origin của app. Thiếu `Origin` hoặc `Origin: null` trên request POST/PUT/PATCH/DELETE → 403.
3. Không thay đổi trạng thái qua GET.
4. Cookie `SameSite=Strict` + `__Host-` là lớp bảo vệ bổ sung.

### Quyền sở hữu
- Mọi bảng dữ liệu người dùng (`Tasks`, `Events`, `Notes`) có cột `UserId` (FK, có index).
- `UserId` **luôn lấy từ claim `sub`**, không bao giờ nhận từ body / query.
- Truy vấn luôn lọc theo user hiện tại. Tài nguyên của người khác trả **404**.
- Bắt buộc có integration test: user A không đọc / sửa / xoá được dữ liệu user B.

## 3. Quy ước thời gian

### Kiểu dữ liệu
| Loại | C# | PostgreSQL | JSON |
|---|---|---|---|
| Thời điểm (sự kiện có giờ, `CreatedAt`...) | `DateTimeOffset`, offset = 0 | `timestamptz` | `"2026-11-02T02:00:00Z"`, **bắt buộc có `Z` hoặc offset**; thiếu offset → 400 |
| Ngày (cả ngày, hạn task) | `DateOnly` | `date` | `"2026-11-02"`; chuỗi có giờ → 400 |
| Múi giờ user | `string` IANA | `text` | `"Asia/Ho_Chi_Minh"` |

### Múi giờ hồ sơ là chuẩn duy nhất `[TIME-01]`
- Khi đăng ký, frontend gửi `Intl.DateTimeFormat().resolvedOptions().timeZone`. Backend kiểm tra ID hợp lệ (`TimeZoneInfo.TryFindSystemTimeZoneById`); không hợp lệ hoặc thiếu → `Asia/Ho_Chi_Minh`.
- **Mọi thứ** (nhập giờ, hiển thị, nhãn ngày, "hôm nay", ranh giới truy vấn) dùng múi giờ hồ sơ, **không** dùng múi giờ của thiết bị.
- Đổi múi giờ hồ sơ: sự kiện có giờ giữ nguyên thời điểm UTC (chỉ cách hiển thị thay đổi); sự kiện cả ngày và hạn task không đổi.

### Frontend `[TIME-02]`
- Ngày không có giờ giữ nguyên dạng chuỗi `YYYY-MM-DD` từ API đến UI. **Cấm** `new Date("YYYY-MM-DD")` và `toISOString().slice(0, 10)` cho kiểu ngày.
- Thao tác múi giờ dùng `date-fns` + `@date-fns/tz` (`TZDate`) với múi giờ hồ sơ. Lý do: nhẹ, phổ biến, hỗ trợ IANA.
- Có lint rule / helper riêng (`lib/dates.ts`) để mọi chỗ đi qua cùng một đường.

### Truy vấn theo khoảng `[TIME-03]`
API: `GET /api/events?from=2026-11-02&to=2026-11-09` (ngày lịch, `from < to`, tối đa 42 ngày; sai → 400).

Backend:
1. Tính `fromUtc` = nửa đêm đầu ngày `from` theo múi giờ hồ sơ, đổi sang UTC. Tương tự `toUtc`. **Tính từng đầu riêng**, không cộng 24h / 168h (sai vào ngày đổi DST).
2. Lấy sự kiện thoả **một trong hai** nhánh, luôn kèm `UserId = currentUser`:
   - Có giờ: `StartAt < toUtc AND EndAt > fromUtc`
   - Cả ngày: `StartDate < to AND EndDate > from`

### Ràng buộc DB `[TIME-03]`
```sql
CHECK (
  (IsAllDay = false AND StartAt IS NOT NULL AND EndAt IS NOT NULL AND EndAt > StartAt
                    AND StartDate IS NULL AND EndDate IS NULL)
  OR
  (IsAllDay = true  AND StartDate IS NOT NULL AND EndDate IS NOT NULL AND EndDate > StartDate
                    AND StartAt IS NULL AND EndAt IS NULL)
)
```
Cột `IsAllDay` khai báo `boolean NOT NULL`, và CHECK viết đủ `IS NOT NULL` cho từng cột thời gian. Lý do: nếu một biểu thức trong CHECK là NULL thì CHECK trả UNKNOWN, và PostgreSQL **cho qua** hàng đó. Test: chèn hàng `IsAllDay = NULL` hoặc thiếu một đầu thời gian phải bị DB từ chối.

### DST
Việt Nam không đổi giờ mùa hè, nhưng code phải đúng với mọi múi giờ. MVP: frontend chỉ cho chọn giờ theo bước 15 phút và chuyển sang UTC bằng `TZDate`. Giờ không tồn tại (khoảng nhảy DST) được thư viện đẩy tới giờ hợp lệ kế tiếp; giờ bị lặp lấy lần xuất hiện đầu. Ghi rõ đây là hành vi đã chấp nhận.

### Test bắt buộc
Sự kiện kết thúc đúng `from` / bắt đầu đúng `to` bị loại; 23:00–00:00 chỉ ở ngày đầu; sự kiện bao trùm cả tuần vẫn có; ranh giới tháng, năm, 29/02; ngày DST 23h và 25h (dùng `America/New_York`); round-trip `"2026-11-02"` không đổi khi múi giờ thiết bị là New York; hàng DB thiếu một đầu bị CHECK chặn. Test dùng **đồng hồ giả** (`TimeProvider` ở backend, `vi.setSystemTime` ở frontend), không phụ thuộc giờ máy.

## 4. Render Markdown an toàn `[AC-02]`
- Lưu Markdown source nguyên văn (chỉ chuẩn hoá NFC). Soạn bằng `textarea`, xem bằng chế độ preview / đọc.
- Render bằng `react-markdown` **không** bật `rehype-raw`: raw HTML hiển thị dạng chữ, không thực thi. Không dùng `dangerouslySetInnerHTML`.
- Cú pháp hỗ trợ: CommonMark + GFM (bảng, checklist, gạch ngang).
- Link: chỉ `http:`, `https:`, `mailto:`. Mở tab mới với `rel="noopener noreferrer"`. Scheme khác (`javascript:`, `data:`) bị bỏ.
- Ảnh: **không** render trong MVP (tránh tải ảnh ngoài để theo dõi người dùng).
- Thêm header `Content-Security-Policy` chặn inline script làm lớp phòng thủ thứ hai.
- Lưu ý: access token trong bộ nhớ và cookie HttpOnly **không** chống được XSS đang chạy, vì script độc vẫn gọi được API. Chống XSS là việc của phần render ở trên và của CSP.

## 5. PWA và trạng thái offline `[AC-03]`
| Hạng mục | Quy tắc |
|---|---|
| Trình duyệt đích | Chrome / Edge desktop, Chrome Android (tự động test). Safari iOS kiểm tra thủ công khi đã có bản deploy HTTPS |
| Viewport nghiệm thu | Mobile 390×844, desktop 1366×768. Không có thanh cuộn ngang, mọi thao tác chính dùng được |
| Service worker cache | Chỉ asset tĩnh đã build (allowlist do `vite-plugin-pwa` sinh). **Không** cache `/api/*` |
| Offline | Sau lần tải online đầu tiên, offline vẫn mở được app shell và hiện banner "Bạn đang offline". Nút tạo/sửa/xoá/hoàn thành bị vô hiệu |
| Mất mạng giữa chừng | Không báo thành công, không tự retry. Hiện lỗi; khi có mạng lại thì tải lại dữ liệu từ server |
| Đăng xuất / đổi tài khoản | Xoá toàn bộ cache dữ liệu ở frontend; user sau không thấy dữ liệu user trước |
| Kiểm tra | Playwright: app shell offline, chặn mutation khi offline, logout → login user khác. Cài đặt thật: kiểm tra thủ công |
