#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DIST_DIR="$ROOT_DIR/dist"
CONFIG="Release"

echo "==> Cleaning dist..."
rm -rf "$DIST_DIR"
mkdir -p "$DIST_DIR"

echo "==> Publishing linux-x64..."
dotnet publish "$ROOT_DIR" -c "$CONFIG" -r linux-x64 --self-contained \
    -p:PublishSingleFile=true -o "$DIST_DIR/linux-x64"
cp -r "$DIST_DIR/linux-x64/learn-Assist" "$DIST_DIR/learn-assist-linux-x64"
chmod +x "$DIST_DIR/learn-assist-linux-x64"
tar -czf "$DIST_DIR/learn-assist-linux-x64.tar.gz" -C "$DIST_DIR" learn-assist-linux-x64

echo "==> Publishing win-x64..."
dotnet publish "$ROOT_DIR" -c "$CONFIG" -r win-x64 --self-contained \
    -p:PublishSingleFile=true -o "$DIST_DIR/win-x64"
cp "$DIST_DIR/win-x64/learn-Assist.exe" "$DIST_DIR/learn-assist-win-x64.exe"
(cd "$DIST_DIR" && zip -9 -q learn-assist-win-x64.zip learn-assist-win-x64.exe)

echo "==> Cleaning intermediate folders..."
rm -rf "$DIST_DIR/linux-x64" "$DIST_DIR/win-x64"

echo "==> Done. Artifacts in:"
ls -lh "$DIST_DIR"
