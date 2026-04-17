# VPS Deployment Plan (Docker Compose)

This plan is aligned with the current repository setup:

- 1 compose file: `docker-compose.yml`
- API service name: `api`
- Nginx Proxy Manager service name: `npm`
- NPM container name: `amkcollective-npm`

Choose one mode before deploy:

- Mode A: You already run NPM separately on VPS (outside this compose stack).
- Mode B: You run NPM from this compose stack.

## 0) Variables To Set Before Deploy

Update these values for each release:

```bash
DOCKERHUB_USER=your_dockerhub_user
TAG=2026-04-16.1
IMAGE=$DOCKERHUB_USER/fptu-capstone-amkcollective:$TAG
APP_DIR=/root/amekolab/ameko-api
DOMAIN=api.your-domain.com
```

Notes:

- `IMAGE` must be written into `.env` on VPS (`IMAGE=...`).
- Keep `.env` private. Do not commit it to git.

## 1) Deploy With GitHub Actions (Recommended)

Workflow file:

- `.github/workflows/deploy-vps.yml`

Current safety conditions in workflow:

- Only runs when selected branch is `develop`.
- Requires manual confirmation input `confirm=true`.

### 1.1) Required GitHub Secrets

Create these in GitHub repository settings:

- `DOCKERHUB_USERNAME`
- `DOCKERHUB_TOKEN`
- `VPS_HOST`
- `VPS_USER`
- `VPS_SSH_KEY`

### 1.2) One-Time VPS Setup

```bash
mkdir -p /root/amekolab
git clone <your-repo-url> /root/amekolab/ameko-api
cd /root/amekolab/ameko-api
cp .env.example .env
nano .env
```

Make sure `.env` contains real runtime values (DB/JWT/Cloudinary/Redis/Email/CORS and optional NPM ports).

### 1.3) Run Deploy From GitHub Actions

1. Push your code to branch `develop`.
2. Open GitHub -> `Actions` -> `Deploy API To VPS`.
3. Click `Run workflow`.
4. Select branch `develop`.
5. Set input `confirm` to `true`.
6. Wait for both jobs (`build-and-push`, `deploy`) to become green.

### 1.4) What The Workflow Does Automatically

- Build image from `FPTU.Capstone.AMKCollective.Api/Dockerfile`.
- Push two tags: `latest` and `${github.sha}`.
- SSH to VPS at `/root/amekolab/ameko-api`.
- Pull latest code of selected branch.
- Update `IMAGE=` in VPS `.env` to `${DOCKERHUB_USERNAME}/fptu-capstone-amkcollective:${github.sha}`.
- Run:
  - `docker compose --env-file .env pull api`
  - `docker compose --env-file .env up -d --no-build api`

### 1.5) Verify After Action Succeeds

```bash
ssh your-user@your-vps
cd /root/amekolab/ameko-api
docker compose --env-file .env ps
docker compose --env-file .env logs --tail=120 api
curl -fsS http://127.0.0.1:8080/health
```

### 1.6) Fast Rollback (When Needed)

```bash
ssh your-user@your-vps
cd /root/amekolab/ameko-api
PREV_IMAGE=your-dockerhub-user/fptu-capstone-amkcollective:<previous-tag-or-sha>
sed -i "s|^IMAGE=.*|IMAGE=$PREV_IMAGE|" .env
docker compose --env-file .env pull api
docker compose --env-file .env up -d --no-build api
docker compose --env-file .env ps
```

Important:

- Current workflow deploys only `api` service.
- If you run NPM in this stack and need to update it, run manually:

```bash
docker compose --env-file .env up -d npm
```

## 2) Manual Build And Push Image (Local Machine)

```bash
docker login -u "$DOCKERHUB_USER"

docker build \
  -f FPTU.Capstone.AMKCollective.Api/Dockerfile \
  -t "$IMAGE" \
  --build-arg CONFIGURATION=Release \
  .

docker push "$IMAGE"
```

Optional `latest` tag:

```bash
docker tag "$IMAGE" "$DOCKERHUB_USER/fptu-capstone-amkcollective:latest"
docker push "$DOCKERHUB_USER/fptu-capstone-amkcollective:latest"
```

## 3) Prepare VPS (Manual Path)

SSH to VPS and go to deploy directory:

```bash
ssh your-user@your-vps
cd "$APP_DIR"
```

Backup current env before editing:

```bash
cp .env ".env.backup.$(date +%F-%H%M)"
```

Set new image tag:

```bash
sed -i "s|^IMAGE=.*|IMAGE=$IMAGE|" .env
grep '^IMAGE=' .env
```

## 4) Deploy On VPS (Manual Path)

### Mode A: NPM runs separately on VPS

Use this mode if another NPM container/project already owns ports 80/443/81.

```bash
docker compose --env-file .env pull api
docker compose --env-file .env up -d api
docker compose --env-file .env ps
```

### Mode B: NPM runs in this compose stack

Use this mode if you want API + NPM managed together here.

```bash
docker compose --env-file .env pull api
docker compose --env-file .env up -d api npm
docker compose --env-file .env ps
```

## 5) Quick Verify After Deploy (Manual Path)

Check container status:

```bash
docker compose --env-file .env ps
```

Check API logs:

```bash
docker compose --env-file .env logs --tail=120 api
```

Health endpoint from VPS:

```bash
curl -fsS http://127.0.0.1:8080/health
```

If you are using Mode B, check NPM logs:

```bash
docker logs amkcollective-npm --tail=80
```

If domain and SSL are configured in NPM:

```bash
curl -Ik "https://$DOMAIN/health"
```

## 6) Rollback Plan

### Option A: Rollback by env backup (recommended)

```bash
cp .env.backup.YYYY-MM-DD-HHMM .env
docker compose --env-file .env pull api
docker compose --env-file .env up -d api
docker compose --env-file .env ps
```

### Option B: Rollback by previous tag

```bash
PREV_TAG=2026-04-15.2
PREV_IMAGE=$DOCKERHUB_USER/fptu-capstone-amkcollective:$PREV_TAG

sed -i "s|^IMAGE=.*|IMAGE=$PREV_IMAGE|" .env
docker compose --env-file .env pull api
docker compose --env-file .env up -d api
docker compose --env-file .env ps
```

## 7) NPM SSL Checklist

In NPM Admin (`http://<VPS-IP>:81`):

1. Proxy Hosts -> Add Proxy Host
2. Domain Names: your API domain
3. Forward Hostname/IP: `api`
4. Forward Port: `80`
5. SSL tab -> Request a new SSL Certificate
6. Enable `Force SSL` and `HTTP/2 Support`

Routing target by mode:

- Mode A (external NPM): forward to VPS IP + API published port (example: `127.0.0.1:8080` or `<VPS-IP>:8080`).
- Mode B (same compose stack): forward to `api:80`.

## 8) Release Checklist

- [ ] New image built successfully
- [ ] New image pushed to Docker Hub
- [ ] `.env` updated on VPS with new `IMAGE`
- [ ] GitHub Actions deploy successful OR manual deploy successful
- [ ] If using GitHub Actions: branch is `develop` and input `confirm=true`
- [ ] Selected Mode A or Mode B before manual deploy command
- [ ] `docker compose pull` and `up -d` successful
- [ ] `curl /health` successful
- [ ] Domain HTTPS check successful
- [ ] No port conflict on 80/443/81
- [ ] Rollback command tested/ready
