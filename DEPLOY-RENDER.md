# Deploying the LandaDoc API to Render (free-tier demo)

This setup is for demos. For production, use the VPS setup in [DEPLOY.md](DEPLOY.md).

| Piece | Where it runs |
|---|---|
| 9 backend services | Render free web services, built from `docker/Dockerfile.service` (defined in [render.yaml](render.yaml)) |
| Postgres (all 8 database-backed services share it) | Render free Postgres |
| Redis | Render free Key Value |
| RabbitMQ | CloudAMQP free plan |
| Document file storage | Cloudflare R2, or any S3-compatible bucket |
| Patient / Doctor / Admin web apps | Vercel, from their own repos (Patient-repo, Doctor-repo, Admin-repo). Each has a `DEPLOY-VERCEL.md`. |

## Free-tier limits

- **Services sleep after 15 minutes idle.** The first request after that takes 30–60 seconds. A sleeping service doesn't consume RabbitMQ messages, run the booking-expiry sweep or run Hangfire jobs. Messages wait in CloudAMQP until the service wakes.
- **Free Postgres expires after 30 days.** Render deletes it after a grace period, so upgrade it or recreate it.
- **Free instance hours are shared.** A workspace gets 750 per month across all services. Sleeping services don't use any.

## 1. Accounts outside Render

1. **CloudAMQP:** create a free instance, choosing the AWS `eu-central-1` region (close to Render's Frankfurt). From its details page, note the **host**, **user**, **password** and **vhost**. On CloudAMQP the vhost usually has the same value as the user.
2. **Cloudflare R2:**
   1. Create a bucket named `landadoc-documents`.
   2. Create an R2 API token with Object Read & Write permission on that bucket. Note the **Access Key ID** and **Secret Access Key**.
   3. Note the S3 endpoint: `https://<account-id>.r2.cloudflarestorage.com`.

## 2. Backend on Render

1. Render → **New → Blueprint** → connect `Mapwaba/Medical-api-repo`. The Render GitHub app has to be installed on the **Mapwaba** account, so the repo owner may need to approve it.
2. Render reads `render.yaml` and asks for every `sync: false` value:
   - `S3__ServiceUrl`, `S3__AccessKey`, `S3__SecretKey`: the R2 values from step 1.
   - `Frontend__PatientBaseUrl`: `https://landadoc-patient.vercel.app`, or whatever URL the Patient app gets on Vercel. You can change it later.
   - Stripe, MokoAfrika, Twilio, SendGrid: test keys, or leave them blank for now.
3. Click **Apply**. The first deploy of each service fails because RabbitMQ isn't configured yet. That's expected.
4. Go to **Env Groups → landadoc-shared** and add:

   | Key | Value |
   |---|---|
   | `RabbitMq__Host` | CloudAMQP host, e.g. `rat.rmq2.cloudamqp.com` |
   | `RabbitMq__VirtualHost` | CloudAMQP vhost |
   | `RabbitMq__Username` | CloudAMQP user |
   | `RabbitMq__Password` | CloudAMQP password |
   | `Cors__AllowedOrigins__0` | `https://landadoc-patient.vercel.app` |
   | `Cors__AllowedOrigins__1` | `https://landadoc-doctor.vercel.app` |
   | `Cors__AllowedOrigins__2` | `https://landadoc-admin.vercel.app` |

   Blueprint syncs keep keys you add in the dashboard, so you only do this once.
5. Redeploy every service: open each one → **Manual Deploy → Deploy latest commit**. Each service creates its own tables on first boot (`Database__MigrateOnStartup=true`).
6. Check `https://landadoc-identity.onrender.com/health` and the other services' `/health` endpoints. Each should return `{"status":"healthy"}`.

**If Render added a suffix to a name** (for example `landadoc-identity-x7k2.onrender.com`, because the plain name was taken):
- Update that URL in `deploy/vercel/appsettings.Production.json` in each of the three frontend repos.
- If the service is Appointment, update `Services__AppointmentBaseUrl` on the Review service.
- If the service is Payment, update `MokoAfrika__CallbackBaseUrl` on the Payment service.

## 3. Frontends

Deploy the three web apps from their own repos. Follow `DEPLOY-VERCEL.md` in Patient-repo, Doctor-repo and Admin-repo.

If a frontend ends up with a different URL than the table in step 2.4 assumes, update the matching `Cors__AllowedOrigins__*` value in the env group. If it is the Patient app, also update `Frontend__PatientBaseUrl` on Payment.

## 4. Webhooks (only if you're testing payments)

- Stripe → webhook endpoint `https://landadoc-payment.onrender.com/api/payments/webhook/stripe`.
- MokoAfrika sends its callback to `MokoAfrika__CallbackBaseUrl` + `/api/payments/webhook/moko`. That's already set in `render.yaml`.

## Redeploying

Pushing to `main` redeploys automatically: Render rebuilds all 9 services.
