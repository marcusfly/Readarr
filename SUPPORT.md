# Readarr Support

This document covers supported versions, the upgrade path, rollback procedure, and
support windows for Readarr.

---

## Supported versions

| Branch  | Docker tag  | Status              | Notes                              |
|---------|-------------|---------------------|------------------------------------|
| master  | `latest`    | Supported (stable)  | Recommended for production         |
| develop | `develop`   | Best-effort         | Pre-release; may contain breakage  |

Only the two most recent stable releases receive bug-fix backports. Older releases
are unsupported and should be upgraded before filing a bug report.

---

## Supported platforms

| Platform        | Architecture | Delivery                  |
|-----------------|--------------|---------------------------|
| Linux           | amd64        | Docker (GHCR), tar.gz     |
| Linux           | arm64        | Docker (GHCR), tar.gz     |
| Windows         | x64          | tar.gz (no installer yet) |
| macOS           | x64 / arm64  | tar.gz                    |

FreeBSD x64 and Linux x86 builds are produced by CI but are community-supported
only. Open an issue with reproduction steps if a platform-specific regression is
found.

---

## Runtime requirements (bare-metal / non-Docker installs)

- .NET 8 runtime (ASP.NET Core) — version 8.0.x or later patch release
- SQLite 3.35 or later (bundled on Windows; provided by the OS on Linux/macOS)
- Optional: PostgreSQL 14 or 15 (external service; see configuration below)

---

## Configuration

All configuration can be provided via:

1. **Environment variables** (recommended for containers) — use double underscores
   as namespace separators, e.g. `READARR__SERVER__PORT=8787`.
2. **config.xml** written to the data directory on first start.
3. **Command-line flags** — `--port`, `--data`, `--nobrowser`, etc.

### Key variables

| Variable                     | Default        | Description                            |
|------------------------------|----------------|----------------------------------------|
| `READARR__APP__DATADIR`      | `/config`      | Path to config, database, and logs     |
| `READARR__SERVER__PORT`      | `8787`         | HTTP listening port                    |
| `READARR__POSTGRES__HOST`    | _(empty)_      | PostgreSQL host (leave empty for SQLite)|
| `READARR__POSTGRES__PORT`    | `5432`         | PostgreSQL port                        |
| `READARR__POSTGRES__USER`    | _(empty)_      | PostgreSQL username                    |
| `READARR__POSTGRES__PASSWORD`| _(empty)_      | PostgreSQL password                    |

### Data volume

Mount a persistent volume at `/config` (Docker) or set `READARR__APP__DATADIR` to
an absolute path. The directory contains:

- `config.xml` — application settings
- `readarr.db` — SQLite main database (if not using PostgreSQL)
- `readarr.db-wal`, `readarr.db-shm` — SQLite WAL files (normal; safe to keep)
- `logs/` — rotating log files

### Book library

Mount your ebook and audiobook collections read-write so Readarr can rename and move files. Keep ebook managers such as Calibre pointed at `/books` only.

```
docker run -v /your/books:/books -v /your/audiobooks:/audiobooks ghcr.io/readarr/readarr:latest
```

---

## Upgrade procedure

### Docker

```bash
# Pull the new image
docker pull ghcr.io/readarr/readarr:latest

# Stop the running container
docker compose down          # if using Compose
# or
docker stop readarr

# Restart (Compose recreates the container automatically)
docker compose up -d
# or
docker run -d ... ghcr.io/readarr/readarr:latest
```

Database schema migrations run automatically on startup. A backup of the SQLite
database is written to `readarr.db.backup` in the data directory before any
destructive migration.

### Bare-metal (tar.gz)

1. Stop Readarr (`systemctl stop readarr` or send SIGTERM).
2. Back up the data directory.
3. Extract the new tar.gz over the existing installation directory.
4. Start Readarr.

---

## Backup procedure

### SQLite (default)

```bash
# While Readarr is running (WAL mode — safe for live backups)
sqlite3 /config/readarr.db ".backup '/backup/readarr-$(date +%F).db'"

# Or stop Readarr first and copy the file
cp /config/readarr.db /backup/readarr-$(date +%F).db
```

The Readarr UI also exposes a one-click backup at **Settings → System → Backup**.
Backups are written to `<datadir>/Backups/` and retained for 28 days by default.

### PostgreSQL

Use `pg_dump` or your provider's snapshot mechanism. Readarr does not manage
PostgreSQL backups.

---

## Rollback procedure

1. Stop Readarr.
2. Restore the data-directory backup taken before the upgrade.
3. Replace the application binaries with the previous version's tar.gz, or pull the
   previous Docker image tag (tags are immutable on GHCR):

   ```bash
   docker pull ghcr.io/readarr/readarr:0.4.19.x   # use the exact previous version
   ```

4. Start Readarr. Schema migrations are not reversed automatically. If a migration
   introduced a breaking schema change, restore the database backup from step 2.

**Note:** Downgrading across a major schema migration may not be possible without
a database restore. Always back up before upgrading.

---

## Verifying a release

Every release attaches:

- `Readarr.<version>.sha256` — SHA256 checksums for all release archives
- `readarr-<version>-sbom.cyclonedx.json` — CycloneDX software bill of materials
- A cosign signature on the GHCR image (keyless / Sigstore)

### Verify checksum

```bash
sha256sum -c Readarr.<version>.sha256
```

### Verify container image signature

```bash
cosign verify \
  --certificate-identity-regexp "https://github.com/Readarr/Readarr/.github/workflows/build-and-publish.yml" \
  --certificate-oidc-issuer https://token.actions.githubusercontent.com \
  ghcr.io/readarr/readarr:<version>
```

---

## Support windows

| Item                       | Window                                     |
|----------------------------|--------------------------------------------|
| Stable release bug fixes   | Until the next stable release              |
| Last two stable releases   | Security fixes only                        |
| Older releases             | No support — please upgrade                |
| develop branch             | Best-effort; no guaranteed fix timeline    |
| .NET runtime               | Follows Microsoft's .NET LTS schedule      |
| Node.js (build-time only)  | Node 20 LTS until April 2026               |

---

## Filing a bug report

Before reporting a bug:

1. Confirm you are on a supported version.
2. Check the [GitHub Issues](https://github.com/Readarr/Readarr/issues) for
   duplicates.
3. Collect the Readarr log from **Settings → System → Logs** or `<datadir>/logs/`.
4. Open a new issue using the **Bug Report** template.
