# Inflan API Backend

.NET 8 Web API with PostgreSQL database.

## Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) installed and running

## Getting Started

```bash
# Clone the repository
git clone https://github.com/asifraza1013/dartford_backend.git
cd dartford_backend

# Start the API and database
docker compose up -d --build
```

The API will be available at **http://localhost:8080**

## Services

| Service    | URL                    | Description          |
|------------|------------------------|----------------------|
| API        | http://localhost:8080   | .NET 8 Web API       |
| PostgreSQL | localhost:5432          | Database             |
| pgAdmin    | http://localhost:5050   | DB management UI (optional) |

### Database Credentials

- **Host:** localhost
- **Port:** 5432
- **Database:** inflan_db
- **Username:** postgres
- **Password:** postgres123

## Common Commands

```bash
# Start all services
docker compose up -d --build

# View API logs
docker compose logs -f api

# Stop all services
docker compose down

# Rebuild after code changes (important: don't use 'restart')
docker compose up -d --build api

# Start with pgAdmin (database management UI)
docker compose --profile tools up -d --build
# Login: admin@example.com / admin123
```

## Important Notes

- Always use `docker compose up -d --build api` after code changes. `docker compose restart api` will **not** pick up code changes.
- Uploaded files are stored in `./wwwroot/uploads` and `./wwwroot/campaignDocs` (mounted as a volume).
