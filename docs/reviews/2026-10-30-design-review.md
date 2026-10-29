# Review thiết kế Dayloom — 2026-10-30

## Phạm vi và phiên bản

- Reviewer: Codex; Claude Code phụ trách xử lý phát hiện.
- Đã đọc `AGENTS.md`, `docs/DESIGN.md` và nhật ký 2026-10-30 trong `docs/PROGRESS.md`.
- Phiên bản được review: nội dung hiện tại của `docs/DESIGN.md`, 82 dòng, 5.605 byte; chưa có Git/commit.
- SHA-256 của `DESIGN.md`: `2BF54867E2A87FAFD8127FA164EB0E956F5B79CE292B99ED25CC3642705947CB`.
- Trọng tâm: §2 auth, §3 thời gian, §1 tiêu chí nghiệm thu. Số dòng bên dưới ứng với phiên bản trên.
- Đây là review tài liệu, chưa có mã nguồn để xác nhận lỗ hổng hoặc chạy test ứng dụng. Các kịch bản là trường hợp cần đặc tả và kiểm thử khi triển khai.

## Đánh giá chung

Năm góp ý trước đã được phản ánh vào tài liệu. Đồng ý với việc giới hạn MVP, dùng hash refresh token, access token trong bộ nhớ, lọc quyền sở hữu ở backend, dùng `DateOnly` và khoảng nửa mở. Chưa nên coi thiết kế auth/thời gian là hoàn tất trước khi chốt các điểm dưới đây.

Có **12 phát hiện: 8 mức Cao, 4 mức Trung bình**. Cao: có thể làm hỏng luồng chính, sai dữ liệu hoặc tạo rủi ro bảo mật nếu triển khai theo một cách hiểu chưa được tài liệu loại trừ. Trung bình: thiếu quy tắc vận hành hoặc điều kiện pass/fail. Đây là mức ưu tiên thiết kế, không phải kết luận đã tồn tại lỗi trong code.

## Auth — §2

### AUTH-01 — SameSite=Strict chưa gắn với mô hình domain khi deploy

- **Mức độ:** Cao.
- **Vị trí:** dòng 46, 53–59.
- **Vấn đề:** proxy Vite chỉ giải quyết môi trường dev. Khác origin và khác site là hai trường hợp khác nhau: `https://app.example.com` và `https://api.example.com` khác origin nhưng cùng site; frontend/API nằm trên hai domain đăng ký khác nhau là cross-site. Trường hợp sau không gửi cookie Strict khi gọi refresh; thêm CORS hoặc `credentials: include` không khắc phục được. Login có thể nhận access token nhưng reload hoặc sau 15 phút sẽ mất phiên.
- **Đề xuất:** ưu tiên deploy frontend và `/api` qua cùng origin bằng reverse proxy. Nếu dùng hai subdomain cùng site, chốt HTTPS, cookie host-only ở API, `credentials: include` cho login/refresh/logout và CORS chỉ cho phép đúng origin frontend, kèm credentials; không dùng wildcard. Nếu buộc phải cross-site, phải thiết kế riêng `SameSite=None; Secure`, CSRF và kiểm tra chính sách chặn cookie bên thứ ba; không coi đây là thay đổi một thuộc tính là xong. `Domain` của cookie không phải cách nối hai domain không liên quan. Ghi cả cấu hình HTTPS dev hoặc ngoại lệ chỉ dành cho dev.
- **Kiểm chứng cần có:** browser E2E trên mô hình origin giống deploy: login → reload → refresh → logout; một ca cross-site Strict phải thất bại đúng dự kiến, không được chỉ test qua Vite proxy.
- **Cơ sở:** [MDN: SameSite](https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Set-Cookie#samesitesamesite-value), [MDN: Fetch credentials và CORS](https://developer.mozilla.org/en-US/docs/Web/API/Fetch_API/Using_Fetch#including_credentials).

### AUTH-02 — Rotation và thu hồi family chưa có bảo đảm nguyên tử

- **Mức độ:** Cao.
- **Vị trí:** dòng 47–49.
- **Vấn đề:** “phát mới rồi thu hồi cũ” chưa ngăn hai request cùng đọc token đang hợp lệ và cùng tạo token con. Thu hồi các hàng hiện có cũng có thể bỏ sót token con đang được tạo đồng thời. Nếu nhánh phát hiện reuse ghi thu hồi rồi ném lỗi khiến transaction rollback, family thực tế vẫn hoạt động.
- **Đề xuất:** mô tả một thao tác DB nguyên tử: kiểm tra family còn hoạt động, token chưa hết hạn/chưa dùng, đánh dấu token đã dùng và tạo đúng một token con. Dùng khóa hàng hoặc cập nhật có điều kiện và kiểm tra số hàng bị ảnh hưởng; chỉ trả cookie sau commit. Family phải liên kết với user và có trạng thái thu hồi bền vững, được kiểm tra trong mọi lần refresh; rotation và revocation phải đồng bộ trên cùng family. Nhánh reuse phải commit thu hồi trước khi trả lỗi. Giữ hash/quan hệ token tổ tiên đủ lâu để phát hiện reuse trong khi family còn hiệu lực, không xóa ngay khi rotate. SHA-256 phù hợp cho token ngẫu nhiên 256-bit nếu token được sinh bằng CSPRNG.
- **Kiểm chứng cần có:** hai refresh đồng thời với cùng token không tạo hai nhánh hợp lệ; replay token tổ tiên sau nhiều lần rotation làm mọi token con bị từ chối; refresh chạy đua với thu hồi không tạo phiên sống sót; lỗi trả về không rollback việc thu hồi.
- **Cơ sở:** [RFC 9700 §4.14.2](https://www.rfc-editor.org/rfc/rfc9700.html#section-4.14.2) nêu rotation giữ quan hệ giữa token và thu hồi token đang hoạt động khi phát hiện replay. Cơ chế transaction/khóa ở trên là đề xuất triển khai cho Dayloom.

### AUTH-03 — Request hợp lệ đồng thời hoặc retry có thể bị coi là đánh cắp token

- **Mức độ:** Trung bình.
- **Vị trí:** dòng 48–55.
- **Vấn đề:** nhiều API cùng nhận 401, hai tab cùng khởi động, hoặc retry sau khi mất response đều có thể gửi lại cùng refresh token. Theo quy tắc hiện tại, request thứ hai có thể thu hồi cả family dù không có kẻ tấn công. Response về sai thứ tự còn có thể ghi đè/xóa cookie mới bằng kết quả cũ. Chỉ chống tranh chấp ở DB không giải quyết trải nghiệm phía trình duyệt.
- **Đề xuất:** dùng một promise refresh chung cho các request trong một tab, phối hợp refresh giữa các tab cùng origin, và giới hạn mỗi API chỉ refresh/thử lại một lần. Không đưa `/login`, `/refresh`, `/logout` vào interceptor refresh đệ quy. Phân biệt lỗi mạng/5xx với token vô hiệu; không tự động logout chỉ vì mất mạng. Với request đã commit nhưng mất response, MVP có thể chọn yêu cầu đăng nhập lại để giữ quy tắc từ chối reuse nghiêm ngặt; phải ghi rõ đánh đổi. Nếu chọn cơ chế retry/idempotency hoặc grace window, cần thiết kế riêng; không cho token cũ tùy ý tạo thêm token con.
- **Kiểm chứng cần có:** nhiều request 401 trong một tab chỉ tạo một refresh; hai tab refresh không vô tình đá phiên trong ca bình thường; mô phỏng mất response, response đảo thứ tự và refresh trả 401 để chứng minh không lặp vô hạn.

### AUTH-04 — Logout có thể bỏ sót token kế tiếp; vòng đời 14 ngày chưa rõ

- **Mức độ:** Cao.
- **Vị trí:** dòng 21–22, 45–50, 56.
- **Vấn đề:** nếu refresh đã đổi T0 thành T1 nhưng logout vẫn mang T0, thu hồi riêng “token hiện tại” có thể bỏ sót T1. Refresh đang bay cũng có thể trả token/cookie sau logout. Ngoài ra, thu hồi refresh token không tự vô hiệu JWT đã phát. “Sống 14 ngày” chưa xác định là 14 ngày từ login hay gia hạn sau mỗi rotation; chưa ghi tuổi thọ cookie.
- **Đề xuất:** định nghĩa mỗi lần login là một family/session; logout thu hồi toàn bộ family xác định được từ token, kể cả token tổ tiên, đồng bộ với rotation như AUTH-02. Logout phải idempotent, không đòi access JWT còn hạn, xóa cookie với đúng tên/Path/Domain đã cấp. Frontend xóa access token và bỏ qua kết quả refresh thuộc thế hệ phiên trước logout. Chốt rõ có chấp nhận JWT cũ dùng được đến `exp` cộng clock skew hay cần vô hiệu ngay; nếu chọn vô hiệu ngay, phải thêm kiểm tra session/revocation phía API. Đề xuất MVP: thời hạn tuyệt đối của family là 14 ngày từ login, token con/cookie không vượt thời hạn đó; ghi `Max-Age`/`Expires` phù hợp. Nếu chọn sliding expiration, ghi rõ giới hạn tuyệt đối. Quy định thêm hành vi khi user bị vô hiệu hóa hoặc đổi mật khẩu, tối thiểu không phát JWT mới qua refresh cho user không còn được phép đăng nhập.
- **Kiểm chứng cần có:** logout với token đã rotate; refresh/logout chạy đồng thời; logout lặp lại; access JWT hết hạn; token hết hạn đúng mốc 14 ngày; kiểm tra hành vi JWT cũ sau logout đúng chính sách đã chọn.

### AUTH-05 — Các endpoint dùng cookie chưa có quy tắc chống CSRF

- **Mức độ:** Cao.
- **Vị trí:** dòng 46, 53–59.
- **Vấn đề:** cookie được browser tự gửi cho refresh/logout. Strict không cô lập origin giữa các subdomain cùng site; đổi sang None để deploy cross-site làm rủi ro rõ hơn. CORS tự nó không ngăn mọi request gây thay đổi trạng thái. Kẻ kiểm soát một subdomain cùng site có thể gây logout/rotation nếu endpoint chấp nhận request đơn giản mà không xác minh nguồn. Đây là điều kiện rủi ro, chưa có bằng chứng endpoint thực tế mắc lỗi.
- **Đề xuất:** chốt CSRF cho login/refresh/logout: xác minh `Origin` theo allowlist origin frontend; quy định rõ fallback `Referer` và cách từ chối nguồn thiếu/`null`. Với browser SPA, yêu cầu JSON và custom header qua preflight, chỉ cho CORS đúng origin; hoặc dùng anti-CSRF token được kiểm tra phía server. Không nhận form/simple content type để bỏ qua kiểm tra. Không thay đổi phiên qua GET. Cookie host-only và SameSite vẫn là lớp bảo vệ bổ sung.
- **Kiểm chứng cần có:** POST bằng form từ site ngoài và subdomain không tin cậy không thay đổi phiên; origin giả mạo/thiếu theo chính sách bị chặn; frontend hợp lệ vẫn login/refresh/logout được.
- **Cơ sở:** [OWASP: CSRF, giới hạn của SameSite và kiểm tra nguồn](https://cheatsheetseries.owasp.org/cheatsheets/Cross-Site_Request_Forgery_Prevention_Cheat_Sheet.html#limitations-of-samesite).

### AUTH-06 — JWT tự phát chưa có hợp đồng xác thực đầy đủ

- **Mức độ:** Cao.
- **Vị trí:** dòng 45, 63.
- **Vấn đề:** tài liệu chỉ nêu HS256, 15 phút và `sub`. Chưa xác định issuer/audience, nguồn khóa ký, thuật toán được chấp nhận, clock skew và ánh xạ claim userId. Hai cách triển khai khác nhau có thể chấp nhận token sai đối tượng hoặc không xác thực được user; không thể nghiệm thu chính xác thời hạn 15 phút nếu để clock skew ngầm định.
- **Đề xuất:** ghi hợp đồng token gồm `iss`, `aud`, `sub`, `iat`, `exp` và định danh phiên nếu cần theo AUTH-04. Backend dùng middleware chuẩn, xác minh chữ ký, thuật toán HS256 cho phép, issuer, audience, lifetime và `sub` hợp lệ. Khóa ký phải ngẫu nhiên đủ mạnh, chỉ nằm ở backend, quản lý bằng secret; thiếu khóa phải fail startup. Chốt clock skew và cách lấy `sub` nhất quán với cấu hình claim mapping. Đây không phải yêu cầu triển khai một OAuth server đầy đủ.
- **Kiểm chứng cần có:** token bị sửa, ký bằng khóa khác, sai issuer/audience/thuật toán, hết hạn, thiếu `sub` đều bị từ chối; token hợp lệ ánh xạ đúng user và không đọc được tài nguyên user khác.
- **Cơ sở:** [Microsoft: JWT bearer validation](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication#implementing-jwt-bearer-token-authentication).

## Thời gian — §3

### TIME-01 — “Giờ địa phương” có thể khác múi giờ user dùng để truy vấn

- **Mức độ:** Cao.
- **Vị trí:** dòng 73, 77, 79, 81.
- **Vấn đề:** frontend đổi sang “giờ địa phương” thường được hiểu là timezone của thiết bị, nhưng ranh giới truy vấn lại theo timezone của user. Nếu hồ sơ là `Asia/Ho_Chi_Minh`, máy đang ở New York, một sự kiện có thể hiển thị ngày khác ngày đã dùng để tải dữ liệu. Chưa có quy tắc nhập giờ khi hai timezone khác nhau hoặc chọn timezone mặc định.
- **Đề xuất:** dùng một timezone hiển thị chính thức của user cho nhập giờ, nhãn ngày và truy vấn. Khi đăng ký có thể lấy IANA ID từ thiết bị làm gợi ý, lưu và kiểm tra ID hợp lệ ở backend; quy định hành vi khi ID thiếu/sai hoặc user đổi timezone. Nếu hỗ trợ nhập ở timezone khác, gửi lựa chọn đó rõ ràng. Với sự kiện một lần, UTC là thời điểm cố định: đổi timezone chỉ thay đổi cách nhìn, không tự sửa thời điểm. Giữ `TimeZoneId` sự kiện chưa dùng là chấp nhận được cho phạm vi không có lịch lặp.
- **Kiểm chứng cần có:** timezone trình duyệt khác hồ sơ; cùng một UTC event được nhập/hiển thị/lọc nhất quán theo hồ sơ; đổi timezone không làm mất sự kiện quanh nửa đêm. Ví dụ `2026-11-01T17:30:00Z` là 00:30 ngày 02/11 ở Việt Nam nhưng 12:30 ngày 01/11 ở New York.

### TIME-02 — DateOnly đúng ở DB nhưng vẫn có thể lệch ngày trên frontend/API

- **Mức độ:** Cao.
- **Vị trí:** dòng 26, 72, 76–77.
- **Vấn đề:** chưa chốt định dạng JSON và đường đi của ngày không có giờ. Nếu date picker ở UTC+7 tạo local midnight rồi dùng `toISOString().slice(0, 10)`, ngày 02/11 thành 01/11. Ngược lại, `new Date('2026-11-02')` được hiểu theo UTC; format ở New York hiển thị 01/11. Quy tắc chuyển UTC của sự kiện có giờ không được áp dụng cho hạn task/sự kiện cả ngày.
- **Đề xuất:** API nhận/trả ngày dạng chuỗi `YYYY-MM-DD`; frontend giữ ngữ nghĩa ngày lịch và không chuyển qua UTC hoặc timezone khi lưu/hiển thị. Từ chối ngày không hợp lệ và đầu vào datetime ở trường DateOnly. Sự kiện có giờ phải gửi datetime có offset/`Z`, không nhận chuỗi thiếu timezone theo giờ server; chuẩn hóa offset về UTC trước khi ghi Npgsql. `DateTimeOffset` gửi vào `timestamptz` cần offset 0 theo hợp đồng driver.
- **Kiểm chứng cần có:** round-trip `2026-11-02` giữ nguyên ở Việt Nam/New York; ngày 29/02 hợp lệ và không hợp lệ; timestamp có offset được chuẩn hóa; timestamp thiếu offset bị từ chối. Hai ví dụ JavaScript phía trên đã được chạy độc lập bằng Node 22.20, cho kết quả lệch ngày như mô tả.
- **Cơ sở:** [MDN: Date parsing và toISOString](https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Global_Objects/Date#date_time_string_format), [Npgsql: timestamps và timezone](https://www.npgsql.org/doc/types/datetime.html#timestamps-and-timezones).

### TIME-03 — Truy vấn mới đặc tả sự kiện có giờ; thiếu biên DST và bất biến khoảng

- **Mức độ:** Trung bình.
- **Vị trí:** dòng 30–33, 76–82.
- **Vấn đề:** `StartAt < to AND EndAt > from` đúng với khoảng hợp lệ của sự kiện có giờ, nhưng không áp dụng trực tiếp cho hàng cả ngày dùng `StartDate`/`EndDate`. Chưa có điều kiện `from < to`, quy tắc tính từng local midnight, hoặc ràng buộc `End > Start` ở DB. Cộng 24 giờ UTC để lấy ngày tiếp theo sai ở ngày đổi DST; giờ nhập trong vùng DST có thể không tồn tại hoặc có hai cách hiểu.
- **Đề xuất:** đặc tả hai nhánh truy vấn: sự kiện có giờ dùng UTC boundaries, sự kiện cả ngày dùng `StartDate < toDate AND EndDate > fromDate`, kèm bộ lọc user cho cả hai. Ngày/tuần lấy local midnight ở hai đầu theo timezone user rồi chuyển từng đầu sang UTC; không cộng cố định 24/168 giờ. Reject khoảng rỗng/đảo chiều. CHECK của từng loại phải bắt buộc đủ hai đầu, loại còn lại NULL, và `EndAt > StartAt` hoặc `EndDate > StartDate`; tránh CHECK trả UNKNOWN vì NULL mà vẫn cho dữ liệu qua. Chốt cách từ chối giờ DST không tồn tại và yêu cầu offset/lựa chọn rõ với giờ bị lặp.
- **Kiểm chứng cần có:** sự kiện kết thúc đúng `from` hoặc bắt đầu đúng `to` bị loại; 23:00–00:00 chỉ hiện ngày đầu; sự kiện bao trọn tuần vẫn được trả; sự kiện cả ngày không dịch theo timezone. Thêm ngày DST 23/25 giờ, đầu tuần thứ Hai, khoảng sai và hàng DB thiếu một đầu. Kiểm tra độc lập bằng Node xác nhận ngày 08/03/2026 tại New York chạy từ `05:00Z` đến `04:00Z` hôm sau, dài 23 giờ.

## Tiêu chí nghiệm thu — §1

### AC-01 — Nhiều hành vi chưa có giá trị kỳ vọng để quyết định pass/fail

- **Mức độ:** Trung bình.
- **Vị trí:** dòng 19–22, 26–27, 37.
- **Vấn đề:** các mục kiểm thử được về ý tưởng, nhưng “nhiều lần”, “tạm”, “quá hạn”, “hôm nay”, “sắp xếp”, “không dấu” chưa xác định đủ để hai bên viết cùng một expected result. Giới hạn/validation đầu vào cũng chưa được ghi.
- **Đề xuất:** bổ sung bảng quy tắc; các giá trị dưới đây là đề xuất để người chủ/Claude chốt, chưa phải yêu cầu đã được phê duyệt:

  | Hạng mục | Quy tắc đề xuất hoặc quyết định còn cần chốt |
  |---|---|
  | Lockout | Ví dụ 5 lần sai → khóa 5 phút; chốt khi nào reset bộ đếm, hành vi login/refresh trong lúc khóa và dùng đồng hồ test điều khiển được. |
  | Email/mật khẩu | Chốt chuẩn hóa email để so trùng, unique ở DB kể cả hai đăng ký đồng thời; chính sách mật khẩu cụ thể; mã lỗi và thông báo UI tiếng Việt. |
  | Khôi phục phiên | Chốt bootstrap refresh lúc reload; hết hạn/revoked → màn đăng nhập; offline/5xx → trạng thái lỗi có thể thử lại. Tuổi thọ cookie và family theo AUTH-04. |
  | Quá hạn | Đề xuất `DueDate < Today(userZone) AND chưa hoàn thành`; task không hạn không thuộc nhóm này. |
  | Hôm nay | Đề xuất `DueDate = Today(userZone)`; xác định có bao gồm task đã hoàn thành và cách kết hợp với bộ lọc trạng thái. |
  | Sắp xếp | Chốt chiều mặc định, thứ tự Cao/Trung bình/Thấp, NULL hạn chót ở cuối và khóa phụ ổn định khi bằng nhau. |
  | Tìm ghi chú | Chốt tìm chuỗi con hay từ, khoảng trắng/query rỗng, Unicode NFC/NFD, quy tắc `d`/`đ`, pinned có luôn đứng trước kết quả tìm kiếm hay không. Test trên PostgreSQL thật. |
  | Validation | Tiêu đề chỉ có khoảng trắng có hợp lệ không; độ dài tối đa của tiêu đề/mô tả/nội dung; note có bắt buộc tiêu đề/nội dung; default priority; quy tắc khi sửa event đổi loại. |

- **Kiểm chứng cần có:** mỗi quyết định có bộ input/expected output; cố định thời điểm test quanh local midnight để kiểm tra `Today`, không dùng trực tiếp ngày hệ điều hành trong assertion.

### AC-02 — “Văn bản thuần, hiển thị Markdown” chưa xác định biên an toàn khi render

- **Mức độ:** Cao.
- **Vị trí:** dòng 35–37; liên quan dòng 59.
- **Vấn đề:** lưu dưới dạng text không bảo đảm HTML sinh từ Markdown an toàn. Chưa quy định raw HTML, URL scheme và tập cú pháp được hỗ trợ. Nếu renderer cho HTML/link nguy hiểm chạy, có rủi ro stored XSS. Chưa có chia sẻ không loại bỏ hoàn toàn rủi ro: người dùng có thể dán nội dung không tin cậy. HttpOnly ngăn đọc refresh cookie nhưng script chạy trong origin ứng dụng vẫn có thể gọi refresh/API và đọc response; access token trong bộ nhớ không chống được XSS đang hoạt động.
- **Đề xuất:** ghi rõ textarea lưu Markdown source và vùng preview/read-only render một tập Markdown đã chốt. MVP tắt raw HTML, chỉ cho URL scheme cần thiết, dùng renderer an toàn; nếu tạo HTML thì sanitize trước khi đưa vào DOM. Chốt ảnh ngoài có được phép không. Điều chỉnh cách diễn đạt bảo vệ token để không coi nó là biện pháp ngăn XSS.
- **Kiểm chứng cần có:** Markdown hợp lệ hiển thị đúng; raw HTML, event handler và link `javascript:` không thực thi ở preview và màn đọc; source được giữ nguyên theo chính sách lưu, không bị biến đổi ngoài dự kiến khi sửa/lưu.
- **Cơ sở:** [OWASP: XSS prevention và HTML sanitization](https://cheatsheetseries.owasp.org/cheatsheets/Cross_Site_Scripting_Prevention_Cheat_Sheet.html#html-sanitization).

### AC-03 — PWA/responsive chưa có môi trường nghiệm thu và hành vi mất mạng cụ thể

- **Mức độ:** Trung bình.
- **Vị trí:** dòng 10–11, 14.
- **Vấn đề:** có manifest/icon/service worker chưa đủ để quyết định “cài đặt được” trên thiết bị nào; responsive chưa có viewport chuẩn. “Không cho sửa offline” chưa nói về tạo/xóa/hoàn thành task, mất mạng ngay lúc bấm lưu, cache dữ liệu riêng tư hay dữ liệu user trước còn ở màn hình sau logout.
- **Đề xuất:** chọn danh sách trình duyệt/thiết bị đích và viewport, ví dụ mobile 390×844 và desktop 1366×768; quy định không tràn ngang và thao tác chính dùng được. Phân biệt kiểm tra cài đặt thủ công trên browser đích với Playwright kiểm tra app shell/service worker. Cache theo allowlist asset tĩnh; không cache `/api/auth`, token hoặc response API chứa dữ liệu cá nhân trong MVP. Sau lần tải online, offline reload phải mở shell và thông báo; chặn mọi mutation khi biết offline. Nếu mất kết nối trong lúc gửi, không báo thành công hoặc tự xếp hàng/retry mutation không có bảo đảm idempotency; khi có mạng cần tải lại để xác nhận trạng thái server. Logout/chuyển tài khoản phải xóa state dữ liệu cá nhân trong frontend.
- **Kiểm chứng cần có:** offline reload sau lần truy cập đầu, offline trước và trong mutation, kết nối lại, logout rồi user khác login; không thấy dữ liệu tài khoản cũ và không có thao tác âm thầm đồng bộ sau đó. Cài đặt thực tế trên browser đích phải được kiểm tra khi có bản deploy HTTPS.

## Bàn giao và kiểm chứng của phiên review

- File được tạo: `docs/reviews/2026-10-30-design-review.md`. Không sửa `AGENTS.md`, `CLAUDE.md`, `docs/DESIGN.md` hoặc `docs/PROGRESS.md`.
- Đã đối chiếu các hành vi cookie, refresh rotation, JWT, JavaScript Date và Markdown với nguồn chính thức được dẫn tại phát hiện tương ứng.
- Đã chạy script Node 22.20 qua stdin, không tạo file phụ, để kiểm tra hai trường hợp lệch DateOnly và một ngày DST dài 23 giờ; kết quả khớp ví dụ trong TIME-02/TIME-03. Đây là kiểm chứng ví dụ nền tảng, không phải test ứng dụng.
- Chưa chạy build, integration test hoặc E2E: chưa có code. CSRF, transaction/reuse và browser cookie khi deploy vẫn cần test thực tế sau triển khai.
- Claude Code xử lý phát hiện bằng cách chốt/sửa thiết kế hoặc ghi rõ quyết định chấp nhận đánh đổi; sau đó cập nhật `PROGRESS.md` theo phân vai. Báo cáo này không tự thay đổi stack hay tiêu chí đã chốt.

## Xác nhận sau khi sửa

Đối chiếu ngày 2026-10-30 với `docs/DESIGN.md` hiện tại (194 dòng, SHA-256: `642EBB72783477A37435545593DF8DA8B4E1EA000019D21B4F60B63047BDD824`) và nhật ký “Ngày 1, phần 2”. Kết quả chỉ xác nhận thiết kế, chưa xác nhận triển khai/test. “CHƯA” nghĩa là đã sửa một phần nhưng còn điểm thuộc phát hiện cũ chưa khép lại.

- **AUTH-01 — ĐÃ GIẢI QUYẾT:** §0 chốt một origin HTTPS cho frontend/API, giữ proxy cùng origin khi dev; tách domain phải thiết kế lại auth.
- **AUTH-02 — CHƯA:** §2, dòng 94–102 đã ngăn hai lần dùng cùng token và commit nhánh reuse, nhưng cập nhật hàng `RefreshTokens` chưa bảo đảm đồng bộ với thu hồi hàng `Sessions`; chỉ kiểm tra Session trong transaction không tự khóa nó. Cần khóa cùng hàng Session trước khi kiểm tra/rotate/revoke/logout, giữ đến commit, hoặc cơ chế tương đương. Nếu không, refresh có thể kiểm tra trước revocation rồi vẫn commit/phát JWT sau revocation; đây là phần tranh chấp family đã nêu ở AUTH-02, suy luận theo [PostgreSQL Read Committed](https://www.postgresql.org/docs/current/transaction-iso.html#XACT-READ-COMMITTED).
- **AUTH-03 — ĐÃ GIẢI QUYẾT:** §2 quy định single-flight trong tab, Web Locks giữa tab, giới hạn thử lại, loại endpoint auth khỏi interceptor và chấp nhận rõ việc phải đăng nhập lại khi mất response sau rotation; thứ tự response cần kiểm chứng bằng test khi có code.
- **AUTH-04 — CHƯA:** Session, token tổ tiên, logout idempotent, thế hệ phiên và thời hạn tuyệt đối 14 ngày đã rõ; còn thiếu đồng bộ logout–refresh như AUTH-02. Ngoài ra, dòng 114 nói JWT còn dùng tối đa 15 phút nhưng dòng 74 cho clock skew 30 giây: cần ghi giới hạn tối đa 15 phút 30 giây hoặc điều chỉnh skew tương ứng.
- **AUTH-05 — ĐÃ GIẢI QUYẾT:** §2 chốt JSON, kiểm tra đúng Origin, từ chối Origin thiếu/null và cấm thay đổi trạng thái qua GET; phù hợp mô hình một origin đã chọn.
- **AUTH-06 — ĐÃ GIẢI QUYẾT:** §2 đã chốt claims, thuật toán, kiểm tra JWT, khóa ký, fail startup khi khóa thiếu/ngắn, clock skew và tắt claim mapping.
- **TIME-01 — ĐÃ GIẢI QUYẾT:** §3 dùng timezone hồ sơ thống nhất cho nhập/hiển thị/truy vấn, có mặc định và quy tắc khi đổi timezone.
- **TIME-02 — ĐÃ GIẢI QUYẾT:** §3 chốt JSON ngày/datetime, offset UTC và đường xử lý ngày dạng chuỗi; cấm hai phép chuyển gây lệch ngày, có helper và test round-trip.
- **TIME-03 — CHƯA:** hai nhánh truy vấn, ranh giới DST, khoảng hợp lệ và chính sách giờ DST đã được chốt; CHECK ở dòng 160–168 vẫn có thể trả UNKNOWN nếu `IsAllDay` là NULL với một cặp thời gian hợp lệ. Cần ghi thêm `IsAllDay NOT NULL` hoặc cách CHECK từ chối trường hợp này và test tương ứng; đây là phần bất biến/NULL của TIME-03 cũ ([PostgreSQL CHECK và NULL](https://www.postgresql.org/docs/current/ddl-constraints.html#DDL-CONSTRAINTS-CHECK-CONSTRAINTS)).
- **AC-01 — CHƯA:** §1 đã cụ thể hóa ngưỡng, bộ lọc, tìm kiếm và validation, nhưng dòng 32 cho refresh phiên cũ hoạt động khi lockout, trong khi dòng 115 từ chối refresh khi user “bị khoá”. Cần phân biệt khóa đăng nhập tạm với vô hiệu hóa tài khoản và chốt một expected result cho refresh lúc lockout.
- **AC-02 — ĐÃ GIẢI QUYẾT:** §4 chốt renderer không raw HTML, giới hạn scheme, không render ảnh, có CSP và diễn đạt đúng giới hạn của HttpOnly/token trong bộ nhớ đối với XSS.
- **AC-03 — ĐÃ GIẢI QUYẾT:** §5 đã xác định browser/viewport, cache asset tĩnh, chặn mutation offline, xử lý mất mạng giữa chừng, xóa cache khi đổi tài khoản và phân biệt kiểm thử tự động/thủ công.

Tổng: **8 ĐÃ GIẢI QUYẾT, 4 CHƯA, 0 GIẢI QUYẾT SAI**. Không ghi nhận phát hiện mới mức Cao do lần sửa này trong phạm vi đối chiếu. Chỉ bổ sung mục xác nhận vào báo cáo; không sửa tài liệu khác, không chạy test ứng dụng vì chưa có code.
