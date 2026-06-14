# syntax=docker/dockerfile:1.7
# ──────────────────────────────────────────────────────────────────────────────
# Stage 1 – frontend build
# ──────────────────────────────────────────────────────────────────────────────
FROM node:20-bookworm-slim AS frontend-build

WORKDIR /src

# Install dependencies first for better layer caching
COPY package.json yarn.lock ./
RUN yarn install --frozen-lockfile --network-timeout 120000

# Copy only the frontend source and config needed by webpack
COPY frontend/ ./frontend/
COPY tsconfig.json ./

RUN yarn run build --env production

# ──────────────────────────────────────────────────────────────────────────────
# Stage 2 – backend build
# Supports both linux/amd64 and linux/arm64 via TARGETARCH build arg
# ──────────────────────────────────────────────────────────────────────────────
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0-bookworm-slim AS backend-build

ARG TARGETARCH
ARG READARRVERSION=0.0.0.0
ARG BUILD_SOURCEBRANCHNAME=develop

WORKDIR /src

# Map Docker arch to .NET Runtime Identifier
RUN case "$TARGETARCH" in \
      amd64)  echo "linux-x64"   > /tmp/rid ;; \
      arm64)  echo "linux-arm64" > /tmp/rid ;; \
      arm)    echo "linux-arm"   > /tmp/rid ;; \
      *)      echo "linux-x64"   > /tmp/rid ;; \
    esac

# Restore NuGet packages separately (cache-friendly)
COPY src/NuGet.config src/
COPY src/*.sln src/
COPY src/Directory.Build.props src/Directory.Build.targets src/Directory.Packages.props src/
# Copy all project files for restore
COPY src/ src/

RUN RID=$(cat /tmp/rid) && \
    if [ -n "$READARRVERSION" ] && [ "$READARRVERSION" != "0.0.0.0" ]; then \
        sed -i "s/<AssemblyVersion>[0-9.*]*<\/AssemblyVersion>/<AssemblyVersion>${READARRVERSION}<\/AssemblyVersion>/g" src/Directory.Build.props; \
        sed -i "s/<AssemblyConfiguration>[A-Za-z()\$-]*<\/AssemblyConfiguration>/<AssemblyConfiguration>${BUILD_SOURCEBRANCHNAME}<\/AssemblyConfiguration>/g" src/Directory.Build.props; \
    fi && \
    dotnet msbuild -restore src/Readarr.sln \
        -p:Configuration=Release \
        -p:Platform=Posix \
        -p:RuntimeIdentifiers="$RID" \
        -t:PublishAllRids

# ──────────────────────────────────────────────────────────────────────────────
# Stage 3 – assemble the release layout
# ──────────────────────────────────────────────────────────────────────────────
FROM backend-build AS assemble

ARG TARGETARCH

RUN RID=$(cat /tmp/rid) && \
    mkdir -p /app && \
    cp -r /src/_output/net10.0/"$RID"/publish/. /app/ && \
    cp -r /src/_output/Readarr.Update/net10.0/"$RID"/publish /app/Readarr.Update && \
    # Remove Windows-only helpers
    rm -f /app/ServiceInstall.* /app/ServiceUninstall.* /app/Readarr.Windows.* && \
    # Copy Mono posix helper for Linux
    cp -f /app/Readarr.Mono.* /app/Readarr.Update/ 2>/dev/null || true

# Copy pre-built frontend assets
COPY --from=frontend-build /src/_output/UI /app/UI

# ──────────────────────────────────────────────────────────────────────────────
# Stage 4 – minimal runtime image
# ──────────────────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0-bookworm-slim AS runtime

LABEL org.opencontainers.image.title="Readarr" \
      org.opencontainers.image.description="Book manager and automation for Usenet and BitTorrent users" \
      org.opencontainers.image.url="https://readarr.com" \
      org.opencontainers.image.source="https://github.com/Readarr/Readarr" \
      org.opencontainers.image.licenses="GPL-3.0"

# Install runtime dependencies
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
        ca-certificates \
        curl \
        sqlite3 && \
    rm -rf /var/lib/apt/lists/*

# Create a non-root user
RUN groupadd --gid 1000 readarr && \
    useradd --uid 1000 --gid readarr --shell /bin/sh --create-home readarr

COPY --from=assemble /app /app

# Data directory — mount a volume here to persist your library and config
VOLUME /config
VOLUME /books

# Default environment variables (can be overridden at runtime)
ENV READARR__APP__DATADIR=/config \
    READARR__SERVER__PORT=8787 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    ASPNETCORE_URLS=""

EXPOSE 8787

# Run as non-root by default. Override with --user root if bind mounts require it.
USER readarr

HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
    CMD curl -sf "http://localhost:${READARR__SERVER__PORT:-8787}/ping" || exit 1

ENTRYPOINT ["/app/Readarr", "-nobrowser", "-data=/config"]
