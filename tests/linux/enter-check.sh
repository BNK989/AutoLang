#!/bin/bash
# Is a passed-through Enter lost because of the AutoLang engine, or in any IBus engine under xdotool/WSLg?
#   enter-check.sh ENGINE   e.g. xkb:us::eng  or  autolang
HERE="$(cd "$(dirname "$0")" && pwd)"
OUT=/tmp/autolang-enter.txt
export GTK_IM_MODULE=ibus GDK_BACKEND=x11
ibus engine "$1" 2>/dev/null; sleep 0.5
echo "engine: $(ibus engine)"
python3 "$HERE/testapp.py" "$OUT" textview 2>/dev/null & PID=$!
for _ in $(seq 50); do WIN=$(xdotool search --name "AutoLang test" | head -1); [ -n "$WIN" ] && break; sleep 0.1; done
xdotool windowactivate --sync "$WIN" 2>/dev/null; sleep 0.5
xdotool type --delay 60 "hi"; sleep 0.2
xdotool key --delay 60 Return; sleep 0.2
xdotool type --delay 60 "yo"; sleep 0.6
printf 'text: [%s]\n' "$(sed -z 's/\n/\\n/g' "$OUT")"
kill $PID
