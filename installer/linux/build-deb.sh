#!/usr/bin/env bash
# build-deb.sh — build a Debian .deb package for the KungConnect Agent.
#
# Usage:
#   build-deb.sh <binary-dir> <version> <output.deb> <arch>
#
# Arguments:
#   binary-dir  — directory containing the published KungConnect.Agent binary
#                 and appsettings.json (output of dotnet publish)
#   version     — semver string, e.g. 0.0.42-abc1234
#   output.deb  — path for the resulting .deb file
#   arch        — Debian architecture, e.g. amd64 or arm64
#
# Example:
#   ./build-deb.sh dist/agent-linux-x64 0.0.42-abc1234 \
#       KungConnect-Agent-linux-x64.deb amd64
set -euo pipefail

BINARY_DIR="$1"
VERSION="$2"
OUTPUT="$3"
ARCH="$4"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
STAGE=$(mktemp -d)
trap 'rm -rf "$STAGE"' EXIT

INSTALL_DIR="$STAGE/opt/kungconnect/agent"
SYSTEMD_DIR="$STAGE/lib/systemd/system"
DEBIAN_DIR="$STAGE/DEBIAN"

mkdir -p "$INSTALL_DIR" "$SYSTEMD_DIR" "$DEBIAN_DIR"

# ── Binary + default config ────────────────────────────────────────────────
cp "$BINARY_DIR/KungConnect.Agent" "$INSTALL_DIR/kungconnect-agent"
cp "$BINARY_DIR/appsettings.json"  "$INSTALL_DIR/appsettings.json"
chmod +x "$INSTALL_DIR/kungconnect-agent"

# ── systemd service unit ───────────────────────────────────────────────────
cp "$SCRIPT_DIR/debian/kungconnect-agent.service" "$SYSTEMD_DIR/"

# ── DEBIAN control files ───────────────────────────────────────────────────
# Substitute version and architecture placeholders in the control file.
sed -e "s/VERSION_PLACEHOLDER/$VERSION/g" \
    -e "s/ARCH_PLACEHOLDER/$ARCH/g" \
    "$SCRIPT_DIR/debian/control" > "$DEBIAN_DIR/control"

# Copy maintainer scripts (must be executable).
for f in postinst prerm postrm config templates; do
    cp "$SCRIPT_DIR/debian/$f" "$DEBIAN_DIR/$f"
done
chmod 755 "$DEBIAN_DIR/postinst" "$DEBIAN_DIR/prerm" "$DEBIAN_DIR/postrm" "$DEBIAN_DIR/config"

# ── Build ──────────────────────────────────────────────────────────────────
dpkg-deb --build --root-owner-group "$STAGE" "$OUTPUT"

echo "Built: $OUTPUT"
