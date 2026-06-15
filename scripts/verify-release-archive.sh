#!/usr/bin/env bash
set -euo pipefail

archive_path="${1:-}"
archive_label="${2:-release}"

if [[ -z "$archive_path" || ! -f "$archive_path" ]]; then
  echo "Usage: $0 <archive.tar.gz> [label]" >&2
  exit 1
fi

staging_dir="$(mktemp -d)"
cleanup() {
  rm -rf "$staging_dir"
}
trap cleanup EXIT

tar xzf "$archive_path" -C "$staging_dir"

test -x "$staging_dir/Readarr"
test -d "$staging_dir/Readarr.Update"
test -x "$staging_dir/Readarr.Update/Readarr.Update"
test -d "$staging_dir/UI"

echo "Verified $archive_label archive layout: $archive_path"
