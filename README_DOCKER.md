**README — Docker & CI/CD Quickstart**

Mục đích: file này hướng dẫn nhanh cho người mới trong team cách build, push, pull và chạy project dưới dạng Docker image, cũng như cấu hình CI (GitHub Actions) để tự động cập nhật image khi push.

**Prerequisites:**

- **Docker** đã cài (Docker Desktop trên Windows).
- Có tài khoản Docker Hub (hoặc GitHub nếu dùng GHCR).
- Quyền push vào repository GitHub của project.

**Vị trí file quan trọng**

- `FPTU.Capstone.AMKCollective/FPTU.Capstone.AMKCollective.Api/Dockerfile` — Dockerfile (đã chuẩn hoá cho project này).
- `.github/workflows/docker-publish.yml` — workflow GH Actions: build + push image lên Docker Hub khi push vào `main`.

**Chạy project cục bộ (không Docker)**

1. Mở PowerShell tại gốc repo:

```powershell
dotnet build "FPTU.Capstone.AMKCollective.sln"
dotnet run --project FPTU.Capstone.AMKCollective.Api\FPTU.Capstone.AMKCollective.API.csproj
```

**Build image local (kiểm tra trước khi push)**

1. Vào thư mục API:

```powershell
cd FPTU.Capstone.AMKCollective\FPTU.Capstone.AMKCollective.Api
```

2. Build image:

```powershell
docker build -t youruser/fptu-capstone-amkcollective:local .
```

3. Run container:

```powershell
docker run -d --name amkcollective-local -p 8080:80 youruser/fptu-capstone-amkcollective:local
```

Mở `http://localhost:8080` để kiểm tra.

**Push image lên Docker Hub (manual)**

1. Tag image (nếu cần):

```powershell
docker tag youruser/fptu-capstone-amkcollective:local youruser/fptu-capstone-amkcollective:latest
```

2. Đăng nhập (lần đầu hoặc nếu private repo):

```powershell
docker login -u YOUR_DOCKERHUB_USERNAME
# nhập password hoặc access token
```

3. Push:

```powershell
docker push youruser/fptu-capstone-amkcollective:latest
```

**Workflow tự động (đã có sẵn)**

- File workflow: `.github/workflows/docker-publish.yml`.
- Hành vi: khi có `push` vào `main`, workflow sẽ build image (multi-arch) và push lên Docker Hub tags:
  - `youruser/fptu-capstone-amkcollective:latest`
  - `youruser/fptu-capstone-amkcollective:<commit-sha>`

**Cài secrets cho workflow**

1. Vào GitHub repo → Settings → Secrets and variables → Actions → New repository secret.
2. Thêm:

- `DOCKERHUB_USERNAME` = Docker Hub username
- `DOCKERHUB_TOKEN` = Docker Hub access token (tạo trên Docker Hub → Account Settings → Security → New Access Token)

Sau khi merge/push vào `main`, kiểm tra tab Actions → job để xem build & push thành công.

**Pull & chạy image (với image public)**

```powershell
docker pull youruser/fptu-capstone-amkcollective:latest
docker run -d --name amkcollective -p 8080:80 youruser/fptu-capstone-amkcollective:latest
```

**Private image**

- Nếu repo private, người dùng cần `docker login` trước khi `docker pull`.

**Cập nhật image & container**

- Thủ công: `docker pull` → stop & rm container → `docker run` lại.
- Tự động: dùng `containrrr/watchtower` để auto-pull + restart khi image mới xuất hiện (cân nhắc bảo mật và cấu hình).

**Best practices & bảo mật**

- KHÔNG commit secrets (connection strings, API keys) vào code hoặc Dockerfile.
- Sử dụng biến môi trường cho secrets khi chạy container.
- Dùng tags immutable (ví dụ commit SHA) cho production deployments.
- Xem xét image signing (cosign) và vulnerability scanning.
- Nếu bạn không muốn public image, để repo Docker Hub là `private`.

**Troubleshooting nhanh**

- Kiểm tra logs: `docker logs -f amkcollective`.
- Kiểm tra container đang chạy: `docker ps`.
- Nếu port bị chiếm: đổi cổng host `-p HOSTPORT:80`.
- Nếu Action thất bại: Actions → job logs; thường là lỗi login (secrets), Dockerfile path, hoặc restore/build dotnet.

---

Nếu bạn muốn, tôi có thể:

- Thêm file `docker-compose.yml` mẫu để dễ `docker-compose up` (kèm watchtower nếu muốn auto-update).
- Thêm README chi tiết hơn (mục release/tagging, GHCR alternative, cosign example).

**_End of file_**
