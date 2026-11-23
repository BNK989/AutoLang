#!/bin/sh
# Installs the spike engine for IBus on this machine. Run from the publish folder's parent:
#   sudo sh linux/install-dev.sh /path/to/publish
# Puts the binary in /usr/local/lib/autolang-spike, the component file in /usr/share/ibus/component,
# and refreshes the IBus cache. Nothing else on the system changes. Undo: sudo sh linux/install-dev.sh --remove
set -eu

LIB=/usr/local/lib/autolang-spike
COMPONENT=/usr/share/ibus/component/autolang-spike.xml

if [ "${1:-}" = "--remove" ]; then
    rm -rf "$LIB" "$COMPONENT"
    echo "removed $LIB and $COMPONENT; run: ibus write-cache; ibus restart"
    exit 0
fi

PUBLISH="${1:?usage: install-dev.sh <publish dir> | --remove}"
HERE="$(dirname "$0")"

mkdir -p "$LIB"
# Copy then rename: replaces the file even while a running engine has it open.
cp "$PUBLISH/autolang-ibus" "$LIB/autolang-ibus.new"
chmod 755 "$LIB/autolang-ibus.new"
mv -f "$LIB/autolang-ibus.new" "$LIB/autolang-ibus"
install -m 755 "$HERE/run.sh" "$LIB/run.sh"
install -m 644 "$HERE/autolang-spike.xml" "$COMPONENT"
echo "installed to $LIB and $COMPONENT"
echo "as your user, run: ibus write-cache; ibus restart"
