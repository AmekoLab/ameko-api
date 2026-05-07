# AMK Collective API (Ameko Lab)

![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![Docker](https://img.shields.io/badge/Docker-2496ED?style=for-the-badge&logo=docker&logoColor=white)
![MySQL](https://img.shields.io/badge/MySQL-4479A1?style=for-the-badge&logo=mysql&logoColor=white)
![SignalR](https://img.shields.io/badge/SignalR-RealTime-0078D4?style=for-the-badge)
![Clean Architecture](https://img.shields.io/badge/Architecture-Clean_Architecture-4CAF50?style=for-the-badge)

**AMKC (Artisan Mechanical Keyboard Collective)** — nền tảng thương mại điện tử + mạng xã hội, trọng tâm vào **Custom Builder** (lắp ráp sản phẩm tùy chỉnh) và tích hợp AI để hỗ trợ người dùng. Thiết kế theo **Clean Architecture** để đảm bảo mở rộng, dễ bảo trì và phân tách rõ ràng giữa business logic và infrastructure.

## Tính năng nổi bật
- Custom Builder: lắp ráp và kiểm tra tương thích linh kiện.
- E‑commerce core: quản lý sản phẩm, giỏ hàng, checkout, voucher phức tạp.
- Wallet & Payment: Ví nội bộ, VNPay, Stripe, quy trình rút tiền và hoa hồng.
- Social Commerce: bài đăng, bình luận, đánh giá, phản hồi.
- Real‑time: Chat, Notifications, Assembly Tracking qua SignalR.
- AI & Semantic Search: Qdrant (vector DB), Google Embeddings, Tavily, Groq.
- Background Workers: dọn dẹp, cập nhật quality score, giải ngân tự động…

## Kiến trúc giải pháp
Solution theo Clean Architecture, tách thành các project chính:

- FPTU.Capstone.AMKCollective.Domain  
  Entities, Enums, quy tắc domain (không phụ thuộc thư viện ngoài).

- FPTU.Capstone.AMKCollective.Application  
  Business logic, DTOs, Interfaces, Background Workers, IRepository definitions.

- FPTU.Capstone.AMKCollective.Infrastructure  
  EF Core, Repositories, Third‑party Integrations (Stripe, VNPay, Email, Storage), AI services (GoogleEmbedding, Qdrant, Tavily), SignalR Hub.

- FPTU.Capstone.AMKCollective.Api  
  RESTful Controllers, Middlewares (GlobalExceptionMiddleware, PerformanceMiddleware), DI config, Program.cs.

- FPTU.Capstone.AMKCollective.Tests  
  Unit / Integration / E2E tests.

## Công nghệ & nền tảng
- .NET 8.0 (C# 12)  
- MySQL + Entity Framework Core  
- Docker, Docker Compose, GitHub Actions (CI/CD)  
- SignalR (real‑time)  
- Qdrant (vector DB), Google Embeddings, Tavily, Groq (LLM)  
- VNPay, Stripe (payments)  
- Hosted Services (background workers)

## Modules chính
- Tài khoản & Shop: JWT auth, hồ sơ shop, hệ thống reputation/quality score.  
- Sản phẩm & Kho: models, parts, stock checks.  
- Giỏ hàng & Đơn hàng: checkout, voucher stacking, order lifecycle.  
- Builder & Assembly: cấu hình tùy chỉnh, kiểm tra compatibility, assembly tracking.  
- Tài chính: wallet, deposit, withdrawal, commission.  
- Cộng đồng: posts, comments, reactions, moderated reviews.  
- AI: trợ lý chat, gợi ý sản phẩm bằng semantic search.

## Background workers
- AbandonedOrderCleanupWorker  
- OrderCancellationTimeoutWorker  
- QualityScoreRefreshWorker  
- FundsReleaseWorker  
- CommissionReminderWorker  
- AssemblyTrackingTimeoutWorker

## Hướng dẫn cài đặt & chạy (Local)

1. Yêu cầu:
   - .NET SDK 8.0+
   - Docker & Docker Compose
   - Git

2. Clone repo:
```bash
git clone <repo-url>
cd ameko-api
```

3. Cấu hình môi trường:
```bash
cp .env.example .env
# điền CONNECTION STRINGS, Stripe/VNPay keys, Groq/Tavily keys, v.v.
```

4. Khởi chạy services phụ trợ:
```bash
docker-compose up -d
```

5. Chạy EF Core migrations:
```bash
cd FPTU.Capstone.AMKCollective.Infrastructure
dotnet ef database update --startup-project ../FPTU.Capstone.AMKCollective.Api
```

6. Chạy API:
```bash
cd ../FPTU.Capstone.AMKCollective.Api
dotnet run
```
Swagger UI: https://localhost:<port>/swagger

## Kiểm thử
```bash
dotnet test
```

## CI/CD & Deploy
- GitHub Actions:
  - docker-publish.yml: build & push image lên GHCR
  - deploy-vps.yml: pull image và deploy qua SSH  
- Tài liệu deploy chi tiết: DEPLOY_PLAN_VPS.md và script docker-helper.sh

## Tài liệu thêm & Ghi chú
- Mô tả chi tiết các workers, flow order/wallet, và API contracts có trong folder tương ứng trong solution.
- Cần điền đầy đủ biến môi trường cho các tích hợp bên thứ ba (payment, AI, storage).

© 2026 Ameko Lab — FPT University Capstone Project
