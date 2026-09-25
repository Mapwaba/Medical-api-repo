# Deploying LandaDoc to an OVH VPS

Everything (9 backend services, 3 Blazor WASM frontends, Postgres, Redis, RabbitMQ, MinIO, Caddy) runs via `docker-compose.prod.yml` on one VPS.
`src/LandaDoc.Gateway` and the `.Mobile` (MAUI) projects are not part of this
deployment — the gateway is an unused stub, and the mobile apps ship through
app stores, not a web server.

## 1. Provision the VPS

- Ubuntu 24.04 LTS.
- At least 4 vCPU / 8 GB RAM / 80 GB NVMe or SSD — this box runs as+database, a broker, object storage, and 12 .NET/nginx containers at once.
- Note the public IPv4 address.

## 2. DNS

In the DNS zone that manages `landadoc.fr`, add A records pointing at the
VPS's IP:

| Name | Type | Value |
|---|---|---|
| `patient` | A | `<vps-ip>` |
| `doctor` | A | `<vps-ip>` |
| `admin` | A | `<vps-ip>` |
| `api` | A | `<vps-ip>` |

## 3. Firewall

Only 22 (SSH), 80, and 443 should be reachable from the internet — everything
else (Postgres, RabbitMQ, MinIO, the service ports) is internal-only in
`docker-compose.prod.yml` (no `ports:` mapping), so this is mostly about
locking down at the network level too (OVH's firewall panel, or `ufw` on the
box) as defense in depth.

```bash
sudo ufw allow 22/tcp
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp
sudo ufw enable
```

## 4. Install Docker

```bash
curl -fsSL https://get.docker.com | sudo sh
sudo usermod -aG docker $USER
# log out/in for the group change to take effect
```

## 5. Clone and configure

```bash
git clone <this-repo-url> /opt/landadoc
cd /opt/landadoc
cp .env.example .env
```

Edit `.env` and fill in **every** value — generate fresh secrets, do not
reuse the values committed in the dev `appsettings.json` files:

```bash
openssl rand -base64 32   # run this for JWT_SECRET, POSTGRES_PASSWORD,
                           # RABBITMQ_PASSWORD,
                           # MINIO_ROOT_PASSWORD
```

`STRIPE_*`, `MOKOAFRIKA_*`, `TWILIO_*`, `SENDGRID_*` come from those
providers' live/production dashboards.

## 6. First deploy

```bash
docker compose -f docker-compose.prod.yml up -d --build
docker compose -f docker-compose.prod.yml ps
```

Each backend service runs its own EF Core migrations on startup
(`Database__MigrateOnStartup=true` in the compose file) — no separate
migration step needed. Check logs if a service restarts in a loop:

```bash
docker compose -f docker-compose.prod.yml logs -f identity
```

Caddy obtains Let's Encrypt certificates automatically on first request to
each subdomain — this requires DNS (step 2) to already be pointing at the
VPS. Confirm with:

```bash
docker compose -f docker-compose.prod.yml logs caddy
```

## 7. Verify

- `https://patient.landadoc.fr`, `https://doctor.landadoc.fr`,
  `https://admin.landadoc.fr` load over HTTPS.
- Browser dev tools Network tab on `patient.landadoc.fr`: API calls go to
  `https://api.landadoc.fr/identity/...` etc. with no CORS errors.
- A login and a booking flow round-trip successfully (Identity + Appointment
  + Availability).

## 8. Register live webhooks

Once `api.landadoc.fr` is reachable:
- Stripe dashboard → webhook endpoint `https://api.landadoc.fr/payment/api/payments/webhook/stripe`.
- MokoAfrika merchant portal → callback URL `https://api.landadoc.fr/payment/api/payments/webhook/moko`
  (already set via `MokoAfrika__CallbackBaseUrl` in the compose file).

## Redeploying after a code change

```bash
cd /opt/landadoc
git pull
docker compose -f docker-compose.prod.yml up -d --build
```

Only the containers whose image actually changed get rebuilt/restarted;
databases and their volumes are untouched.

## Backups

Postgres data lives in a named Docker volume (`pgdata`).
At minimum, cron a nightly dump off the box:

```bash
docker compose -f docker-compose.prod.yml exec -T postgres \
  pg_dump -U postgres landadoc_db | gzip > /backups/postgres-$(date +%F).sql.gz
```

Copy `/backups` off the VPS regularly (OVH object storage, or `rsync` to
another host).
