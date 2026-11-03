#!/usr/bin/env bash
# Builds "Logseq Git Launcher.app" for Apple Silicon. Run on the Mac:  ./scripts/build-mac.sh
set -euo pipefail
cd "$(dirname "$0")/.."

OUT=dist/mac
APP="$OUT/Logseq Git Launcher.app"

rm -rf "$OUT"
# Framework-dependent: the app uses the .NET 10 runtime installed on this Mac.
dotnet publish src/App/App.csproj -c Release -r osx-arm64 --no-self-contained \
    -p:DebugType=None -o "$OUT/publish"

mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp -R "$OUT/publish/." "$APP/Contents/MacOS/"
chmod +x "$APP/Contents/MacOS/LogseqGitLauncher"
cp src/App/Assets/icon.icns "$APP/Contents/Resources/icon.icns"

# Apps started from Finder don't see the shell's PATH, so tell the apphost where .NET lives
# (matters for Homebrew installs; the official installer uses a location it finds on its own).
DOTNET_ROOT_DIR="$(dirname "$(readlink -f "$(command -v dotnet)")")"

cat > "$APP/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleName</key><string>Logseq Git Launcher</string>
  <key>CFBundleDisplayName</key><string>Logseq Git Launcher</string>
  <key>CFBundleIdentifier</key><string>pt.quidgest.logseqgitlauncher</string>
  <key>CFBundleExecutable</key><string>LogseqGitLauncher</string>
  <key>CFBundleIconFile</key><string>icon</string>
  <key>LSEnvironment</key>
  <dict><key>DOTNET_ROOT</key><string>$DOTNET_ROOT_DIR</string></dict>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>CFBundleShortVersionString</key><string>1.0</string>
  <key>CFBundleVersion</key><string>1</string>
  <key>LSMinimumSystemVersion</key><string>12.0</string>
  <key>NSHighResolutionCapable</key><true/>
</dict>
</plist>
PLIST

# Ad-hoc signature: enough for an app built and run on this Mac.
codesign --force --deep -s - "$APP"
# Only matters if the .app was copied from another machine.
xattr -dr com.apple.quarantine "$APP" 2>/dev/null || true

echo "Built: $APP"
echo "Copy it to /Applications, or just double-click it."
