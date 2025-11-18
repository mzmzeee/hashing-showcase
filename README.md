dotnet restore
# Hashing & Signatures Showcase

This walkthrough project now covers the full flow of hashing a message, signing it with RSA, delivering it to another user, and visualising the verification in a Manim animation. The stack includes:

- ASP.NET Core Web API (.NET 9) with EF Core + PostgreSQL
- React SPA with a messaging dashboard and embedded video player
- Python Flask + Manim service that renders “Digital Signature Journey” animations on demand
- Pure C# and Python SHA-256 implementations to highlight the underlying primitives

## Repository layout

- `backend/src/Api` – ASP.NET Core API (auth + messaging + visualization proxy)
- `backend/src/Data` – EF Core context, entities, migrations (users & messages)
- `backend/src/Logic` – hashing helpers, RSA utilities, visualization payload builder
- `backend/tests` – unit tests and integration flow that exercises the animation service
- `frontend` – React application (registration/login, send messages, verify & watch video)
- `animation_service` – Flask microservice that wraps Manim and the Python SHA-256
- `scripts` – helper scripts for building/starting/stopping the demo
- `docker-compose.yml` – PostgreSQL, API, and animation service orchestration

## Prerequisites

- .NET SDK 9.0+
- Node.js 18+
- Python 3.10+ (only required if you run the animation service outside Docker)
- Docker + Docker Compose
- `npm`, `curl`, and `docker` available in `PATH` for the helper scripts

## Quick start (recommended)

```bash
./scripts/build-demo.sh    # one-time build & test pass (optional but useful)
./scripts/start-demo.sh    # launches Docker services + React dev server
```

The start script will:

1. Create `.env` from `.env.example` if missing.
2. Install/update frontend dependencies via `npm ci`/`npm install`.
3. Spin up PostgreSQL, the ASP.NET API, and the animation Flask service via Docker Compose.
4. Wait for the API (`http://localhost:8080/swagger/index.html`) and the animation service (`http://localhost:5000/generate-animation`).
5. Launch the React development server on `http://localhost:3000` (press `Ctrl+C` to stop; containers are torn down automatically).

To stop the stack manually (and optionally purge persisted data):

```bash
./scripts/stop-demo.sh          # stop services, keep database volume
./scripts/stop-demo.sh --purge  # stop services and drop database volume
```

## Demo accounts

Three demo accounts are automatically seeded after the API starts. All share the password `asdfasdf`:

- `alice` – standard user whose signatures verify successfully.
- `bob` – standard user to help demonstrate inbox flows.
- `evil_bob` – intentionally corrupted signer; every message he sends is flagged as **Invalid** so you can test negative paths and animations that show mismatched hashes.

You can still register additional accounts through the UI or API if you’d like.

## Application flow

1. **Register** – the API creates a user, hashes their password (pure C# SHA-256 + stretching), and generates a 2048-bit RSA keypair stored in the database for visibility.
2. **Send message** – an authenticated sender posts content to `/api/messages`. The API hashes the content, signs the hash with the sender’s private key, and stores both message and signature.
3. **Inbox** – recipients call `/api/messages/inbox` to pull recent messages along with sender usernames.
4. **Verify & Visualize** – clicking the button on the frontend calls `/api/visualize/signature`, which:
   - Builds a visualization payload using `SignatureVisualizationService`.
   - Proxies the payload to the Flask Manim service.
   - Streams the returned MP4 back to the browser for playback.

The animation highlights hashing, signing, verification, recomputation, and final comparison.

## Manual backend run

```bash
cd backend
dotnet restore
dotnet ef database update --project src/Data/Data.csproj --startup-project src/Api/Api.csproj
cd src/Api
ASPNETCORE_URLS=http://localhost:8080 dotnet run
```

## Manual frontend run

```bash
cd frontend
npm install    # or npm ci if you prefer
npm start
```

Set `REACT_APP_API_BASE` if you proxy to a non-default API URL.

## Animation service (standalone)

```bash
cd animation_service
pip install -r requirements.txt
python app.py  # runs on http://127.0.0.1:5000
```

The service expects a POST payload with `message`, `message_hash_hex`, `signature_base64`, `decrypted_hash_hex`, `recomputed_hash_hex`, and an optional `hashes_match` flag.

## Configuration matrix

All settings can be supplied via `appsettings*.json`, environment variables, or `.env` (Docker Compose). Use `__` to represent `:` when exporting environment variables.

| Key | Purpose | Default |
| --- | --- | --- |
| `POSTGRES_DB` | PostgreSQL database name | `hashing_demo` |
| `POSTGRES_USER` | PostgreSQL user | `user` |
| `POSTGRES_PASSWORD` | PostgreSQL password | `password` |
| `POSTGRES_PORT` | Host port exposure for PostgreSQL | `5432` |
| `API_PORT` | Host port exposure for ASP.NET API | `8080` |
| `ANIMATION_API_PORT` | Host port exposure for Flask animation service | `5000` |
| `ConnectionStrings__DefaultConnection` | API connection string | Points to Docker `db` service |
| `PasswordHashing__Iterations` | Stretching iterations for demo password hashing | `10000` |
| `AnimationService__BaseUrl` | Base URL the API uses to reach the animation service | `http://animation-api:5000/` in Docker, `http://127.0.0.1:5000/` locally |

## Tests

```bash
cd backend
dotnet test

cd ../frontend
CI=true npm test -- --watch=false
```

Integration tests spin up the Flask process automatically and assert that `/api/visualize/signature` streams an MP4.

## API reference

- `POST /api/auth/register`
  - `{ "username": "string", "password": "string" }`
  - Returns `201 Created` on success.
- `POST /api/auth/login`
  - Returns `{ token, userId, username, publicKey }` for use in `Authorization: Bearer <token>` headers.
- `GET /api/auth/public_keys`
  - Returns `{ username, publicKey }[]` for populating the recipient dropdown.
- `POST /api/messages`
  - Auth required. `{ "recipient_username": "string", "content": "string" }`
  - Stores the message and signature, returning `{ messageId }`.
- `GET /api/messages/inbox`
  - Auth required. Returns an array of `{ message_id, sender_username, content, created_at_utc }` ordered by newest first.
- `POST /api/messages/{messageId}/reanimate`
  - Auth required. Deletes the cached MP4 for the message (if present) and kicks off a fresh animation render.
- `DELETE /api/messages/{messageId}`
  - Auth required. Recipients or senders can remove a message (and its animation) from their inbox/history.
- `POST /api/visualize/signature`
  - Auth required. `{ "message_id": "guid" }`
  - Streams an MP4 animation visualising signature verification.

## Sample workflow

```bash
# Login as Alice (pre-seeded demo user, password: asdfasdf)
TOKEN=$(curl -s -X POST http://localhost:8080/api/auth/login -H "Content-Type: application/json" -d '{"username":"alice","password":"asdfasdf"}' | jq -r .token)

# Send Bob a signed message
curl -s -X POST http://localhost:8080/api/messages \
  -H "Authorization: Bearer ${TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{"recipient_username":"bob","content":"Hello from Alice"}'
```

Bob can then log in, open the inbox, and trigger the visualisation from the frontend.

## Replacing the demo hashing (production guidance)

Swap out `PasswordHasher` for a supported algorithm before shipping anything real:

- **BCrypt** – [`BCrypt.Net-Next`](https://www.nuget.org/packages/BCrypt.Net-Next/)
  ```csharp
  var hash = BCrypt.Net.BCrypt.HashPassword(password);
  var isValid = BCrypt.Net.BCrypt.Verify(password, hash);
  ```
- **Argon2** – [`Isopoh.Cryptography.Argon2`](https://www.nuget.org/packages/Isopoh.Cryptography.Argon2/)
  ```csharp
  var config = new Argon2Config { Password = Encoding.UTF8.GetBytes(password) };
  var hash = Argon2.Hash(config);
  var isValid = Argon2.Verify(hash, password);
  ```

Remove the pure SHA-256 helpers once you adopt one of these libraries.

## Troubleshooting

- **Containers exit immediately** – run `docker compose logs` to check for migration issues or missing environment variables.
- **Animation endpoint fails** – ensure port `5000` is free or update `ANIMATION_API_PORT`/`AnimationService__BaseUrl` accordingly.
- **Integration test warnings** – EF Core emits a version conflict warning when different packages pull slightly different `Microsoft.EntityFrameworkCore.Relational` builds; the tests still pass but you can align package versions if it becomes distracting.

Happy demoing!
