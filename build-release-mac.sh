#!/usr/bin/env bash
# Builds a distributable ModelGenerator.app for macOS with an embedded
# self-contained ModelGenerator.Host and Help content.
#
# Usage:
#   ./build-release-mac.sh              # auto RID (arm64 or x64)
#   ./build-release-mac.sh osx-arm64
#   ./build-release-mac.sh osx-x64
#   SKIP_TESTS=1 ./build-release-mac.sh
#
# Output:
#   dist/ModelGenerator.app
#   dist/ModelGenerator-v<version>-<rid>.zip
#
# Notarization / Developer ID signing are NOT performed here — see notes at end.

set -euo pipefail

ROOT="$(cd "$(dirname "$0")" && pwd)"
cd "$ROOT"

ARCH="$(uname -m)"
if [[ "${1:-}" != "" ]]; then
  RID="$1"
elif [[ "$ARCH" == "arm64" ]]; then
  RID="osx-arm64"
else
  RID="osx-x64"
fi

VERSION="$(grep -m1 '<Version>' src/ModelGenerator.UI/ModelGenerator.UI.csproj | sed -E 's/.*<Version>([^<]+)<\/Version>.*/\1/' || true)"
VERSION="${VERSION:-0.8.0}"

DIST="$ROOT/dist"
APP="$DIST/ModelGenerator.app"
CONTENTS="$APP/Contents"
MACOS="$CONTENTS/MacOS"
RESOURCES="$CONTENTS/Resources"
HOST_PUBLISH="$DIST/host-$RID"
SWIFT_BIN="$ROOT/mac/ModelGeneratorMac/.build/release/ModelGeneratorMac"
ZIP="$DIST/ModelGenerator-v${VERSION}-${RID}.zip"

echo "=== 3D Model Generator v${VERSION} — macOS release ($RID) ==="

if [[ "${SKIP_TESTS:-0}" != "1" ]]; then
  echo "Running tests..."
  dotnet test tests/ModelGenerator.Tests/ModelGenerator.Tests.csproj -c Release
fi

echo "Publishing self-contained Host ($RID)..."
rm -rf "$HOST_PUBLISH"
dotnet publish src/ModelGenerator.Host/ModelGenerator.Host.csproj \
  -c Release \
  -r "$RID" \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true \
  -o "$HOST_PUBLISH"

echo "Building SwiftUI app (release)..."
swift build -c release --package-path mac/ModelGeneratorMac
if [[ ! -x "$SWIFT_BIN" ]]; then
  # SPM may place binary under .build/<triple>/release/
  SWIFT_BIN="$(find mac/ModelGeneratorMac/.build -type f -name ModelGeneratorMac -perm +111 | head -1)"
fi
if [[ ! -x "$SWIFT_BIN" ]]; then
  echo "error: could not find ModelGeneratorMac binary" >&2
  exit 1
fi

echo "Assembling $APP ..."
rm -rf "$APP"
mkdir -p "$MACOS" "$RESOURCES/Help/images"

# Main UI binary
cp "$SWIFT_BIN" "$MACOS/ModelGeneratorMac"
chmod +x "$MACOS/ModelGeneratorMac"

# Embedded host (prefer single-file publish name)
if [[ -x "$HOST_PUBLISH/ModelGenerator.Host" ]]; then
  cp "$HOST_PUBLISH/ModelGenerator.Host" "$MACOS/ModelGenerator.Host"
elif [[ -x "$HOST_PUBLISH/ModelGenerator.Host.dll" ]]; then
  # Framework-dependent fallback — copy whole publish dir
  mkdir -p "$MACOS/host"
  cp -R "$HOST_PUBLISH/"* "$MACOS/host/"
  # Wrapper script
  cat > "$MACOS/ModelGenerator.Host" << 'WRAP'
#!/bin/sh
DIR="$(cd "$(dirname "$0")" && pwd)"
exec dotnet "$DIR/host/ModelGenerator.Host.dll" "$@"
WRAP
else
  # Copy entire publish folder contents next to a launcher
  cp -R "$HOST_PUBLISH/"* "$MACOS/"
  # Ensure executable bit on host binary if present under alternate name
  find "$MACOS" -maxdepth 1 -type f -name 'ModelGenerator.Host*' -exec chmod +x {} \;
fi
chmod +x "$MACOS/ModelGenerator.Host" 2>/dev/null || true

# Help content (single source of truth: docs/)
cp docs/HOW_TO_USE.md "$RESOURCES/Help/"
cp docs/images/*.png "$RESOURCES/Help/images/" 2>/dev/null || true

# Info.plist
cat > "$CONTENTS/Info.plist" << PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleName</key>
  <string>3D Model Generator</string>
  <key>CFBundleDisplayName</key>
  <string>3D Model Generator</string>
  <key>CFBundleIdentifier</key>
  <string>com.johndovey.ModelGenerator</string>
  <key>CFBundleVersion</key>
  <string>${VERSION}</string>
  <key>CFBundleShortVersionString</key>
  <string>${VERSION}</string>
  <key>CFBundlePackageType</key>
  <string>APPL</string>
  <key>CFBundleExecutable</key>
  <string>ModelGeneratorMac</string>
  <key>LSMinimumSystemVersion</key>
  <string>14.0</string>
  <key>NSHighResolutionCapable</key>
  <true/>
  <key>NSPrincipalClass</key>
  <string>NSApplication</string>
</dict>
</plist>
PLIST

# PkgInfo
echo -n 'APPL????' > "$CONTENTS/PkgInfo"

# Zip for distribution
rm -f "$ZIP"
(
  cd "$DIST"
  ditto -c -k --sequesterRsrc --keepParent "ModelGenerator.app" "$(basename "$ZIP")"
)

echo ""
echo "Built:"
echo "  $APP"
echo "  $ZIP"
echo ""
echo "Open with: open \"$APP\""
echo ""
echo "Signing / notarization (optional, for Gatekeeper):"
echo "  codesign --deep --force --options runtime --sign \"Developer ID Application: …\" \"$APP\""
echo "  xcrun notarytool submit \"$ZIP\" --apple-id … --team-id … --password … --wait"
echo "  xcrun stapler staple \"$APP\""
