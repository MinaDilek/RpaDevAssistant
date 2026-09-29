# On-premise deployment

This deployment packages the React UI and .NET 8 API into one non-root container. Central catalog/history data is stored in the `rpada-data` volume and governed UiPath projects are mounted read-only at `/projects`.

1. Copy `.env.example` to `.env` and replace the bootstrap key with `openssl rand -base64 48`.
2. Set `RPADA_PROJECTS_ROOT` to the host folder containing the UiPath projects that administrators may register.
3. Run `docker compose --env-file .env up --build -d` from this directory.
4. Open `http://127.0.0.1:8080`, then use the bootstrap key in **Company Workspace** to create the first tenant and tenant administrator.

The default binding is loopback-only. For company network access, terminate TLS in an approved reverse proxy and set `RPADA_ALLOWED_ORIGIN` to its HTTPS origin. Do not expose the container directly to the internet. Back up the named data volume and retain the one-time user API keys in the company secret manager.

OIDC can be selected with `RPADA_AUTH_MODE=Oidc` or `Hybrid`; both authority and audience are then mandatory and the authority must use HTTPS. OIDC users must first be provisioned in the central catalog with their provider subject and tenant mapping.

Commercial/offline deployments can set `RPADA_LICENSE_REQUIRED=true` and mount a signed `license.json` plus the vendor public key under `/licenses`. The API verifies RSA-PSS/SHA-256 signatures, expiry, maximum active tenants, plan level, and monthly quota before allowing central operations. The private signing key must never be deployed with the application. Vendor operators can issue a license with `node scripts/issue-central-license.mjs --private-key=... --output=... --license-id=... --customer-id=... --expires=...`.
