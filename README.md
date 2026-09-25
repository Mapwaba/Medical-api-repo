# LandaDoc API

The backend of LandaDoc, a platform that helps patients and doctors manage appointments.

It's made up of 9 ASP.NET Core services that talk to each other over RabbitMQ (MassTransit):

| Service | Local port | Stores data in |
|---|---|---|
| Identity | 5138 | Postgres |
| Appointment | 5001 | Postgres |
| Availability | 5088 | Postgres + Redis |
| Payment | 5114 | Postgres |
| Notification | 5286 | Postgres (+ Hangfire) |
| Admin | 5115 | Postgres |
| Search | 5078 | Redis |
| Document | 5198 | Postgres + S3/MinIO |
| Review | 5253 | Postgres |

## Related repos

| Repo | Contents |
|---|---|
| [Patient-repo](https://github.com/Mapwaba/Patient-repo) | Patient web app (Blazor WASM) |
| [Doctor-repo](https://github.com/Mapwaba/Doctor-repo) | Doctor web app (Blazor WASM) |
| [Admin-repo](https://github.com/Mapwaba/Admin-repo) | Admin web app (Blazor WASM) |

## Shared contracts: `src/LandaDoc.Shared`

This repo holds the master copy of `src/LandaDoc.Shared`: DTOs, events and enums. Each frontend repo has its own copy at the same path.

When you change a DTO or enum here, copy `src/LandaDoc.Shared` into all three frontend repos in the same change. Otherwise the frontends will send or read the old shape.

## Running locally

Requires the .NET 9 SDK and Docker.

```powershell
.\run-all.ps1        # starts docker-compose.yml (Postgres, Redis, RabbitMQ, MinIO), then the 9 services
.\stop-all.ps1
```

Connection strings come from each service's `appsettings.json` or from user secrets (`ConnectionStrings:Conx`).

In Development, each service applies its own EF Core migrations on startup.

## Deploying

- **Render (free-tier demo):** [DEPLOY-RENDER.md](DEPLOY-RENDER.md), using [render.yaml](render.yaml).
- **Single VPS with Docker Compose and Caddy:** [DEPLOY.md](DEPLOY.md).

Coding conventions for the services are in [CONVENTIONS.md](CONVENTIONS.md).
