# Educational Hashing Demo

> **Security warning:** This repository is for educational purposes only. The custom SHA-256 password hashing shown here must **not** be used in production systems. Always rely on vetted password hashing libraries such as bcrypt, scrypt, or Argon2 when building real applications.

This project demonstrates a full-stack password hashing demo built with:

- ASP.NET Core Web API (.NET 9)
- Entity Framework Core + PostgreSQL (via Docker Compose)
- React single-page frontend
- Pure C# SHA-256 implementation for learning purposes only

## Project structure

- `backend/src/Api` — ASP.NET Core Web API
- `backend/src/Data` — EF Core context, migrations, entities
- `backend/src/Logic` — pure C# SHA-256 and password helper utilities
- `backend/tests` — unit and integration test projects
- `frontend` — React SPA for registration/login
- `docker-compose.yml` — API + PostgreSQL services

## Prerequisites

- .NET SDK 9.0
- Node.js 18+
- Docker + Docker Compose

## Running with Docker Compose

1. Copy `.env.example` to `.env` and adjust values if necessary.
2. Build and start the stack:
   ```bash
   docker compose up --build
   ```
3. API available at `http://localhost:8080`, PostgreSQL at `localhost:5432`.
4. Frontend dev server (optional) can run separately via `npm start` in `frontend/`.

The API container automatically uses the connection string provided via environment variables. Apply EF Core migrations before first run (see below) or let the API apply them on startup if you enable automatic migration.
The API now applies pending migrations on startup; if you change the schema, rebuild the container or restart the local API so the new migration is executed.

## Scripts

- `./scripts/build-demo.sh` — restores, builds, and tests the backend; installs & tests the frontend; and builds the Docker images.
- `./scripts/start-demo.sh` — ensures `.env` exists, installs frontend dependencies if needed, starts Docker services, waits for the API, then launches the React dev server (Ctrl+C to stop; services shut down automatically).
- `./scripts/stop-demo.sh [--purge]` — stops the Docker services started by the demo; pass `--purge` to remove the PostgreSQL volume as well.

Run the build script before the first start (or after code changes) to make sure everything compiles and tests pass.

## Local development

### Backend API

```bash
cd backend
dotnet restore
cd src/Api
ASPNETCORE_URLS=http://localhost:8080 dotnet run
```

### React frontend

```bash
cd frontend
npm install
npm start
```

The CRA dev server proxies API calls to `http://localhost:8080`.

## Database & migrations

- Update the database locally:
  ```bash
  cd backend
  dotnet ef database update --project src/Data/Data.csproj --startup-project src/Api/Api.csproj
  ```
- Migrations live in `backend/src/Data/Migrations/`.
- Default connection string: `Host=localhost;Port=5432;Database=hashing_demo;Username=user;Password=password` (override via configuration).

## Configuration

All settings can be provided via `appsettings.json` or environment variables (using `__` to denote `:` in keys).

| Setting | Description | Default |
| --- | --- | --- |
| `POSTGRES_DB` | Database name for PostgreSQL | `hashing_demo` |
| `POSTGRES_USER` | Database username | `user` |
| `POSTGRES_PASSWORD` | Database password | `password` |
| `POSTGRES_PORT` | Host port exposed for PostgreSQL | `5432` |
| `API_PORT` | Host port exposed for the API when using Docker | `8080` |
| `ConnectionStrings__DefaultConnection` | Connection string for the API | Points to local Postgres |
| `PasswordHashing__Iterations` | Password stretching iterations | `10000` |
| `ASPNETCORE_URLS` | Binding for the API when self-hosting | `http://localhost:8080` |

## Tests

Backend tests (unit + integration):
```bash
cd backend
dotnet test
```

Frontend tests:
```bash
cd frontend
CI=true npm test -- --watch=false
```

## GitHub Actions CI

The workflow in `.github/workflows/ci.yml` restores dependencies, builds the backend, runs backend tests, installs frontend dependencies, and builds the React app. It runs on pushes and pull requests to ensure the demo stays healthy.

## API endpoints

- `POST /api/auth/register`
  - Body: `{ "username": "<string>", "password": "<string>" }`
  - Minimum password length 8 characters.
  - Response: `201 Created` on success, `400` on validation failure.
- `POST /api/auth/login`
  - Body: `{ "username": "<string>", "password": "<string>" }`
  - Response: `200 OK` or `401 Unauthorized`.
  - Uses constant-time comparison to avoid timing leaks.

## Sample curl commands

```bash
curl -X POST http://localhost:8080/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"username":"demo-user","password":"SuperSecret123"}'

curl -X POST http://localhost:8080/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"demo-user","password":"SuperSecret123"}'
```

## Replacing the custom hashing

For any production use, replace the custom hashing with a mature library. Two straightforward options:

1. **BCrypt (via [BCrypt.Net-Next](https://www.nuget.org/packages/BCrypt.Net-Next/))**
   ```csharp
   var hash = BCrypt.Net.BCrypt.HashPassword(password);
   var isValid = BCrypt.Net.BCrypt.Verify(password, hash);
   ```
2. **Argon2 (via [Isopoh.Cryptography.Argon2](https://www.nuget.org/packages/Isopoh.Cryptography.Argon2/))**
   ```csharp
   var config = new Argon2Config { Password = Encoding.UTF8.GetBytes(password) };
   var hash = Argon2.Hash(config);
   var isValid = Argon2.Verify(hash, password);
   ```

Update the `PasswordHasher` helper to delegate to one of these libraries, store the returned hash string verbatim, and remove the custom SHA-256 implementation when you switch.

## Tooling helpers

- `backend/src/Api/Api.http` contains ready-to-run HTTP requests for VS Code/REST Client.
- `frontend/src/App.js` surfaces the security warning to users.

## Troubleshooting

- Ensure Docker containers can resolve each other; the API expects the database host to be `db` inside Docker.
- Double-check `PasswordHashing__Iterations` when overriding via environment variables; the API will fall back to 10,000 if the supplied value is invalid.
- Integration tests use an in-memory database; real PostgreSQL is only required for manual end-to-end testing.
