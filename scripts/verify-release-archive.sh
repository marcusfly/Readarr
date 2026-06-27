#!/usr/bin/env bash
set -euo pipefail

archive_path="${1:-}"
archive_label="${2:-release}"
archive_platform="${3:-}"

if [[ -z "$archive_path" || ! -f "$archive_path" ]]; then
  echo "Usage: $0 <archive.tar.gz> [label]" >&2
  exit 1
fi

staging_dir="$(mktemp -d)"
install_dir="$staging_dir/install"
data_dir="$staging_dir/data"
cleanup() {
  if [[ -n "${container_name:-}" ]]; then
    docker rm -f "$container_name" >/dev/null 2>&1 || true
  fi
  rm -rf "$staging_dir"
}
trap cleanup EXIT

mkdir -p "$install_dir" "$data_dir"
tar xzf "$archive_path" -C "$install_dir"

test -x "$install_dir/Readarr"
test -d "$install_dir/Readarr.Update"
test -x "$install_dir/Readarr.Update/Readarr.Update"
test -d "$install_dir/UI"
test -f "$install_dir/LICENSE.md"

if compgen -G "$install_dir/Readarr.Windows.*" >/dev/null; then
  echo "Unexpected Windows helpers found in $archive_label archive" >&2
  exit 1
fi

if [[ -n "$archive_platform" ]]; then
  if ! command -v docker >/dev/null 2>&1; then
    echo "Docker is required to smoke-test $archive_label on $archive_platform" >&2
    exit 1
  fi

  case "$archive_platform" in
    linux/amd64) host_port=9871 ;;
    linux/arm64) host_port=9872 ;;
    *) host_port=9870 ;;
  esac

  container_name="readarr-release-verify-${host_port}"

  docker rm -f "$container_name" >/dev/null 2>&1 || true
  docker run -d \
    --name "$container_name" \
    --platform "$archive_platform" \
    -p "127.0.0.1:${host_port}:8787" \
    -e READARR__APP__DATADIR=/config \
    -e READARR__SERVER__PORT=8787 \
    -e READARR__AUTH__METHOD=Forms \
    -e READARR__AUTH__REQUIRED=DisabledForLocalAddresses \
    -v "$install_dir:/app" \
    -v "$data_dir:/config" \
    mcr.microsoft.com/dotnet/aspnet:10.0 \
    /app/Readarr -nobrowser -data=/config >/dev/null

  for _ in $(seq 1 60); do
    if curl -fsS "http://127.0.0.1:${host_port}/ping" >/dev/null 2>&1; then
      break
    fi

    if ! docker ps --format '{{.Names}}' | grep -qx "$container_name"; then
      docker logs "$container_name" >&2 || true
      exit 1
    fi

    sleep 2
  done

  curl -fsS "http://127.0.0.1:${host_port}/ping" >/dev/null
  docker rm -f "$container_name" >/dev/null 2>&1 || true
  container_name=""
fi

echo "Verified $archive_label archive layout: $archive_path"
