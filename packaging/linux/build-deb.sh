#!/bin/bash
# Wraps a published Linux build in a .deb.
#   packaging/linux/build-deb.sh <publish dir> <version> [arch]
#   e.g. packaging/linux/build-deb.sh dist/linux-x64 0.6.0~tester1 amd64
# Needs dpkg-deb (any Debian or Ubuntu, WSL included). build-deb.ps1 publishes on Windows and runs this in WSL.
#
# Contents: /usr/lib/autolang/autolang (the self-contained binary), /usr/bin/autolang (symlink, the CLI),
# /usr/share/ibus/component/autolang.xml, an icon, and the copyright file. No services, no setuid, no udev rules.
set -euo pipefail

PUBLISH="${1:?usage: build-deb.sh <publish dir> <version> [arch]}"
VERSION="${2:?usage: build-deb.sh <publish dir> <version> [arch]}"
ARCH="${3:-amd64}"
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../.." && pwd)"
STAGE="$(mktemp -d)"
trap 'rm -rf "$STAGE"' EXIT

# Staged on the Linux filesystem: modes on a Windows-mounted drive are not real.
install -Dm755 "$PUBLISH/autolang" "$STAGE/usr/lib/autolang/autolang"
mkdir -p "$STAGE/usr/bin"
ln -s ../lib/autolang/autolang "$STAGE/usr/bin/autolang"
mkdir -p "$STAGE/usr/share/ibus/component"
sed "s/@VERSION@/$VERSION/" "$HERE/autolang.xml" > "$STAGE/usr/share/ibus/component/autolang.xml"
install -Dm644 "$ROOT/docs/icon.png" "$STAGE/usr/share/autolang/autolang.png"
install -Dm644 "$HERE/copyright" "$STAGE/usr/share/doc/autolang/copyright"
chmod 644 "$STAGE/usr/share/ibus/component/autolang.xml"

mkdir -p "$STAGE/DEBIAN"
SIZE="$(du -sk "$STAGE/usr" | cut -f1)"
sed -e "s/@VERSION@/$VERSION/" -e "s/@ARCH@/$ARCH/" -e "s/@SIZE@/$SIZE/" "$HERE/control" > "$STAGE/DEBIAN/control"
install -m755 "$HERE/postinst" "$HERE/postrm" "$STAGE/DEBIAN/"

mkdir -p "$ROOT/dist"
# "~" sorts a tester build before the release in dpkg, but is awkward in URLs: the file name uses "-".
OUT="$ROOT/dist/autolang_${VERSION//\~/-}_${ARCH}.deb"
dpkg-deb --root-owner-group -Zxz --build "$STAGE" "$OUT" >/dev/null
echo "$OUT"
dpkg-deb --info "$OUT" | sed -n 's/^ \(Package\|Version\|Architecture\|Installed-Size\):/  \1:/p'
ls -la "$OUT" | awk '{print "  size: " $5 " bytes"}'
