# eShop (aeai-dotnet-brownfield) — Dev Setup Notes

## Requirements
- .NET 10 SDK (project uses `global.json` pinned to `10.0.100+`)
- Docker via **Colima** (not Docker Desktop)

## First-Time Setup Steps

### 1. Install .NET 10 via Homebrew
```bash
brew install dotnet
# If symlink conflicts with old dotnet:
rm /usr/local/bin/dotnet
brew link --overwrite dotnet
dotnet --version  # should show 10.x
```

### 2. Fix Docker credential helper (Colima incompatibility)
Docker Desktop leaves `"credsStore": "desktop"` in `~/.docker/config.json`, which breaks image pulls with Colima.

Edit `~/.docker/config.json` — remove the `credsStore` line, keep `currentContext`:
```json
{
  "auths": {},
  "currentContext": "colima"
}
```

### 3. Trust HTTPS dev cert
```bash
dotnet dev-certs https --trust
```

### 4. Start Colima (if not running)
```bash
colima start
# Verify:
colima status
docker info --format '{{.ServerVersion}}'
```

### 5. Run the app
```bash
cd aeai-dotnet-brownfield
dotnet run --project src/eShop.AppHost/eShop.AppHost.csproj
```
Then open the Aspire dashboard URL printed in the console (e.g. `https://localhost:19888/login?t=...`).

## Notes
- `order-processor` waits for `ordering-api` to become healthy — this is normal, just takes a minute on first start
- All services (RabbitMQ, PostgreSQL w/ pgvector, Redis, all .NET APIs) are managed by Aspire
- Web store URL is shown as "Online Store" link on the Aspire dashboard resources page
