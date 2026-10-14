#!/usr/bin/env bash
# Builds AutoLang.app for Apple silicon (arm64) and Intel (x64) and zips each into dist/.
# Runs on macOS or on Windows (Git Bash with the .NET 8 SDK and Python 3).
#
# On macOS the app is ad-hoc signed and zipped with ditto. Elsewhere the zip is made with Python so the executable
# keeps its unix mode 0755; the tester then ad-hoc signs it on their Mac (see INSTALL-mac-testers.md). Either way
# it is NOT Developer ID signed or notarized: Gatekeeper will refuse it until the tester clears the quarantine flag.
set -euo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
root="$(cd "$here/../.." && pwd)"
build="$root/packaging/.build/mac"
dist="$root/dist"
version="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$root/src/AutoLang.Mac/AutoLang.Mac.csproj")"

rm -rf "$build"
mkdir -p "$build" "$dist"

for arch in arm64 x64; do
  echo "== osx-$arch"
  out="$build/publish-$arch"
  dotnet publish "$root/src/AutoLang.Mac/AutoLang.Mac.csproj" -c Release -r "osx-$arch" --self-contained \
    -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:DebugType=none -o "$out" --nologo -v q

  app="$build/$arch/AutoLang.app"
  mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources"
  cp "$out/AutoLang" "$app/Contents/MacOS/AutoLang"
  chmod 755 "$app/Contents/MacOS/AutoLang"
  cp "$here/Info.plist" "$app/Contents/Info.plist"
  cp "$here/AutoLang.icns" "$app/Contents/Resources/AutoLang.icns"
  printf 'APPL????' > "$app/Contents/PkgInfo"

  zip="$dist/AutoLang-$version-mac-$arch.zip"
  rm -f "$zip"
  if [[ "$(uname)" == "Darwin" ]]; then
    codesign --force --deep --sign - "$app"
    ditto -c -k --keepParent "$app" "$zip"
  else
    py="$(command -v python3 || command -v python)"
    "$py" "$here/zip-app.py" "$app" "$zip"
  fi
  echo "   $zip"
done
cp "$here/INSTALL-mac-testers.md" "$dist/"
