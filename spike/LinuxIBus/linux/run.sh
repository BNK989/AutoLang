#!/bin/sh
# What ibus-daemon starts. Test settings live in ~/.config/autolang/spike.env (KEY=value lines), so a test can
# switch modes with an ibus restart instead of a reinstall.
ENV="${XDG_CONFIG_HOME:-$HOME/.config}/autolang/spike.env"
if [ -f "$ENV" ]; then
    set -a
    . "$ENV"
    set +a
fi
exec /usr/local/lib/autolang-spike/autolang-ibus "$@"
