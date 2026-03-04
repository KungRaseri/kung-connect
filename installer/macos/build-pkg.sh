#!/usr/bin/env bash
# build-pkg.sh — build a macOS .pkg installer for the KungConnect Agent.
#
# Usage:
#   build-pkg.sh <binary-dir> <version> <output.pkg> <rid>
#
# Arguments:
#   binary-dir — directory containing the published KungConnect.Agent binary
#                and appsettings.json (output of dotnet publish)
#   version    — semver string, e.g. 0.0.42-abc1234
#   output.pkg — path for the resulting .pkg file
#   rid        — runtime identifier, e.g. osx-arm64 or osx-x64 (informational only)
#
# Requires: pkgbuild, productbuild (both included in macOS developer tools)
set -euo pipefail

BINARY_DIR="$1"
VERSION="$2"
OUTPUT="$3"
RID="$4"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
STAGE=$(mktemp -d)
COMPONENT_PKG="$(mktemp -d)/component.pkg"
trap 'rm -rf "$STAGE" "$(dirname "$COMPONENT_PKG")"' EXIT

INSTALL_DIR="$STAGE/usr/local/lib/kungconnect-agent"
LAUNCHDAEMON_DIR="$STAGE/Library/LaunchDaemons"

mkdir -p "$INSTALL_DIR" "$LAUNCHDAEMON_DIR"

# ── Binary + default config ────────────────────────────────────────────────
cp "$BINARY_DIR/KungConnect.Agent" "$INSTALL_DIR/KungConnect.Agent"
cp "$BINARY_DIR/appsettings.json"  "$INSTALL_DIR/appsettings.json"
chmod +x "$INSTALL_DIR/KungConnect.Agent"

# ── LaunchDaemon plist ─────────────────────────────────────────────────────
cp "$SCRIPT_DIR/com.kungconnect.agent.plist" "$LAUNCHDAEMON_DIR/"
chmod 644 "$LAUNCHDAEMON_DIR/com.kungconnect.agent.plist"

# ── Build component package ────────────────────────────────────────────────
pkgbuild \
    --root          "$STAGE" \
    --identifier    "com.kungconnect.agent" \
    --version       "$VERSION" \
    --scripts       "$SCRIPT_DIR/scripts" \
    --install-location "/" \
    "$COMPONENT_PKG"

# ── Build distributable package ───────────────────────────────────────────
productbuild \
    --distribution   "$SCRIPT_DIR/distribution.xml" \
    --package-path   "$(dirname "$COMPONENT_PKG")" \
    --version        "$VERSION" \
    "$OUTPUT"

echo "Built: $OUTPUT"
