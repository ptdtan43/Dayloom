# Dayloom

*Weave your day.* Ứng dụng web + PWA gộp lịch trình hằng ngày, việc cần làm và ghi chú vào một nơi.

> 🚧 Đang phát triển. Xem tiến độ ở [docs/PROGRESS.md](docs/PROGRESS.md) và thiết kế ở [docs/DESIGN.md](docs/DESIGN.md).

## Tech stack
- **Frontend:** React 19, TypeScript, Vite, Tailwind CSS
- **Backend:** ASP.NET Core (.NET 10), Entity Framework Core, Clean Architecture ([docs/ARCHITECTURE.md](docs/ARCHITECTURE.md))
- **Database:** PostgreSQL 18
- **Test:** xUnit + Testcontainers (PostgreSQL thật), Vitest
- **CI:** GitHub Actions

## Chạy trên máy

Yêu cầu: .NET SDK 10, Node.js 22, Docker Desktop.

```bash
# 1. Database
cp .env.example .env            # rồi đặt POSTGRES_PASSWORD
docker compose up -d

# 2. Backend (lần đầu: lưu connection string vào user-secrets)
cd backend/src/Dayloom.Api
# Dùng 127.0.0.1, không dùng localhost: DB chỉ mở ở IPv4, còn localhost thử IPv6 trước (chậm 2 giây mỗi kết nối trên Windows)
dotnet user-secrets set "ConnectionStrings:Default" "Host=127.0.0.1;Port=5432;Database=dayloom;Username=dayloom;Password=<POSTGRES_PASSWORD>"
cd ../../.. && dotnet tool restore
dotnet ef database update --project backend/src/Dayloom.Infrastructure --startup-project backend/src/Dayloom.Api
cd backend/src/Dayloom.Api
dotnet run                      # http://localhost:5080, thử API tại http://localhost:5080/scalar

# 3. Frontend (terminal khác)
cd frontend
npm install
npm run dev                     # http://localhost:5173
```

## Chạy test

```bash
cd backend && dotnet test       # cần Docker đang chạy
cd frontend && npm test
```
