# 🐳 FPTU Capstone AMKCollective - Docker & CI/CD

> **Tự động build và deploy Docker image khi commit/PR lên GitHub**

[![Docker Image](https://img.shields.io/docker/v/your-username/fptu-capstone-amkcollective?label=Docker%20Image)](https://hub.docker.com/r/your-username/fptu-capstone-amkcollective)
[![CI/CD](https://github.com/your-org/your-repo/actions/workflows/docker-publish.yml/badge.svg)](https://github.com/your-org/your-repo/actions)

---

## 📚 Documentation Quick Links

### 🚀 Getting Started

- **[QUICKSTART.md](QUICKSTART.md)** - Chạy trong 3 bước (< 5 phút) ⚡
- **[TUTORIAL_DOCKER_CICD.md](TUTORIAL_DOCKER_CICD.md)** - Hướng dẫn chi tiết từng bước 📖

### 🔧 Setup & Configuration

- **[README_DOCKER_SETUP.md](README_DOCKER_SETUP.md)** - Setup đầy đủ và tối ưu hóa 🛠️
- **[GITHUB_SECRETS_GUIDE.md](GITHUB_SECRETS_GUIDE.md)** - Cấu hình GitHub Secrets 🔐

### 📊 Overview

- **[DOCKER_CICD_SUMMARY.md](DOCKER_CICD_SUMMARY.md)** - Tổng quan CI/CD pipeline 📈

---

## ⚡ Quick Start (3 bước)

### Bước 1: Clone & Setup

```powershell
git clone https://github.com/your-org/your-repo.git
cd FPTU.Capstone.AMKCollective
docker-helper.bat setup
```

### Bước 2: Configure

```powershell
# Chỉnh sửa .env với thông tin của bạn
notepad .env
```

### Bước 3: Run

```powershell
docker-helper.bat start
```

✅ **API running at http://localhost:8080**
✅ **Nginx Proxy Manager admin at http://localhost:81**

Mặc định NPM login lần đầu:

- Email: `admin@example.com`
- Password: `changeme`

Xem chi tiết: [QUICKSTART.md](QUICKSTART.md)

---

## 🎯 Cho ai?

### 👨‍💼 Team Leader / Admin

- Xem: [GITHUB_SECRETS_GUIDE.md](GITHUB_SECRETS_GUIDE.md) để setup GitHub Secrets
- Xem: [TUTORIAL_DOCKER_CICD.md](TUTORIAL_DOCKER_CICD.md) phần 1 để setup lần đầu

### 👨‍💻 Developer

- Xem: [QUICKSTART.md](QUICKSTART.md) để chạy ngay
- Xem: [TUTORIAL_DOCKER_CICD.md](TUTORIAL_DOCKER_CICD.md) phần 2 cho workflow hàng ngày

### 🔍 Code Reviewer

- Xem: [TUTORIAL_DOCKER_CICD.md](TUTORIAL_DOCKER_CICD.md) phần 2, kịch bản 3

---

## 🔄 CI/CD Workflow

### Tự động build & push khi:

1. **Push to `main` or `developer`**

   ```bash
   git push origin developer
   # → Image tag: developer, developer-{sha}
   ```

2. **Create Pull Request**

   ```bash
   # → Image tag: pr-{number}
   # → Bot comment Docker pull command
   ```

3. **Manual trigger**
   - GitHub Actions → Run workflow

### Image tags:

- `latest` - Main branch mới nhất
- `developer` - Developer branch mới nhất
- `pr-{number}` - Pull request specific
- `{branch}-{sha}` - Commit cụ thể

---

## 🛠️ Helper Commands

### Windows:

```powershell
docker-helper.bat setup      # Setup lần đầu
docker-helper.bat start      # Start containers
docker-helper.bat stop       # Stop containers
docker-helper.bat logs       # View logs
docker-helper.bat update     # Pull latest & restart
docker-helper.bat health     # Check health
```

### Linux/Mac:

```bash
chmod +x docker-helper.sh
./docker-helper.sh setup
./docker-helper.sh start
./docker-helper.sh logs
./docker-helper.sh update
```

---

## 🔒 SSL với Nginx Proxy Manager

1. Trỏ domain/subdomain về server đang chạy Docker (A record).
2. Mở NPM Admin: `http://localhost:81`.
3. Vào **Proxy Hosts** → **Add Proxy Host**:
   - Domain Names: domain của bạn
   - Forward Hostname / IP: `api`
   - Forward Port: `80`
   - Bật `Websockets Support` (nếu dùng SignalR realtime)
4. Tab **SSL**:
   - Chọn **Request a new SSL Certificate**
   - Bật `Force SSL`
   - Bật `HTTP/2 Support`
   - Điền email và đồng ý Let's Encrypt Terms
5. Save, sau đó test HTTPS domain.

Port mặc định:

- `80` HTTP (public)
- `443` HTTPS (public)
- `81` NPM admin

Bạn có thể đổi qua `.env`: `NPM_HTTP_PORT`, `NPM_HTTPS_PORT`, `NPM_ADMIN_PORT`.

---

## 📜 Team Logs với Dozzle

Dozzle đã được thêm vào `docker-compose.yml` dưới tên service `dozzle`.

### 1) Start Dozzle

```bash
docker compose --env-file .env up -d dozzle
docker compose --env-file .env ps
```

### 2) Public qua NPM (khuyến nghị)

Không expose Dozzle trực tiếp ra Internet. Hãy đi qua NPM:

1. Vào **Proxy Hosts** -> **Add Proxy Host**
2. Domain: `logs.your-domain.com`
3. Forward Hostname / IP: `dozzle`
4. Forward Port: `8080`
5. SSL tab: Request cert + bật `Force SSL` + `HTTP/2`
6. Gán **Access List** (Basic Auth) để chặn truy cập trái phép

### 3) (Optional) Bật login nội bộ Dozzle

Nếu muốn thêm lớp bảo mật thứ 2 trong Dozzle:

1. Set trong `.env`:

```bash
DOZZLE_AUTH_PROVIDER=simple
```

2. Tạo user file:

```bash
mkdir -p dozzle-data
docker run --rm amir20/dozzle:latest generate admin --password "CHANGE_ME_STRONG" --email "devops@example.com" --name "DevOps Admin" > dozzle-data/users.yml
```

3. Restart Dozzle:

```bash
docker compose --env-file .env up -d --force-recreate dozzle
```

### 4) Security checklist

- Không map cổng Dozzle ra host (đã cấu hình private bằng `expose`)
- Luôn đi qua NPM + Access List
- Không bật container actions/shell nếu chưa cần

---

## 📁 Project Structure

```
FPTU.Capstone.AMKCollective/
├── 📄 .env.example              # Environment variables template
├── 📄 docker-compose.yml        # Docker Compose configuration
├── 📄 docker-helper.bat         # Helper script (Windows)
├── 📄 docker-helper.sh          # Helper script (Linux/Mac)
├── 📄 .dockerignore             # Docker ignore patterns
│
├── 📖 QUICKSTART.md             # Quick start guide (< 5 min)
├── 📖 TUTORIAL_DOCKER_CICD.md   # Step-by-step tutorial
├── 📖 README_DOCKER_SETUP.md    # Detailed setup guide
├── 📖 GITHUB_SECRETS_GUIDE.md   # GitHub Secrets configuration
├── 📖 DOCKER_CICD_SUMMARY.md    # CI/CD overview
│
├── 🐳 FPTU.Capstone.AMKCollective.Api/
│   └── Dockerfile               # Optimized multi-stage Dockerfile
│
└── ⚙️ .github/workflows/
    └── docker-publish.yml       # GitHub Actions workflow
```

---

## ✨ Features

### 🐳 Docker Optimization

- ✅ Multi-stage build (image size: ~200MB vs ~1GB)
- ✅ Layer caching (build nhanh hơn 3-5x)
- ✅ Non-root user security
- ✅ Health check tự động
- ✅ .dockerignore optimization

### 🔄 CI/CD Automation

- ✅ Auto build on commit/PR
- ✅ Multi-platform support (amd64, arm64)
- ✅ BuildKit cache
- ✅ Auto comment on PR
- ✅ Version tagging strategy

### 🔐 Security

- ✅ GitHub Secrets for credentials
- ✅ Environment variables
- ✅ No hardcoded secrets
- ✅ Private Docker Hub support

---

## 🆘 Troubleshooting

### Common Issues:

**"Cannot pull image"**

```powershell
docker login
docker pull your-username/fptu-capstone-amkcollective:latest
```

**"Port 8080 already in use"**

```powershell
# Change port in .env
API_PORT=8081
```

**"Health check failed"**

```powershell
docker-helper.bat logs
```

Xem thêm: [README_DOCKER_SETUP.md](README_DOCKER_SETUP.md#troubleshooting)

---

## 📊 Benefits

### Trước:

- ❌ Build Docker image thủ công
- ❌ Share credentials qua chat/email
- ❌ Không có CI/CD automation
- ❌ Image size lớn (~1GB+)

### Sau:

- ✅ Tự động build và push
- ✅ Credentials được quản lý an toàn
- ✅ CI/CD tự động
- ✅ Image size nhỏ (~200MB)
- ✅ Team member pull và chạy trong < 5 phút

---

## 🤝 Contributing

1. Fork repository
2. Create feature branch: `git checkout -b feature/amazing-feature`
3. Commit changes: `git commit -m 'feat: add amazing feature'`
4. Push to branch: `git push origin feature/amazing-feature`
5. Open Pull Request
6. CI/CD sẽ tự động build Docker image với tag `pr-{number}`
7. Reviewer có thể pull và test

---

## 📞 Support

- 📖 Documentation: Xem các file `.md` trong repo
- 🐛 Issues: [GitHub Issues](https://github.com/your-org/your-repo/issues)
- 💬 Team Chat: Contact team members

---

## 📜 License

[Your License Here]

---

**Made with ❤️ by FPTU Team**

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
