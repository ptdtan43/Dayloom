# Dayloom

*Weave your day.* Ứng dụng web + PWA gộp lịch trình hằng ngày, việc cần làm và ghi chú vào một nơi.

> 🚧 Đang phát triển. Xem tiến độ ở [docs/PROGRESS.md](docs/PROGRESS.md) và thiết kế ở [docs/DESIGN.md](docs/DESIGN.md).

## Tech stack
- **Frontend:** React 19, TypeScript, Vite, Tailwind CSS
- **Backend:** ASP.NET Core (.NET 10), Entity Framework Core
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
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=dayloom;Username=dayloom;Password=<POSTGRES_PASSWORD>"
dotnet run                      # http://localhost:5080

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
