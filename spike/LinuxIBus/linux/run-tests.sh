#!/bin/bash
# Spike tests: types physical keys into a real app through IBus and the AutoLang engine, then reads the text back.
#   run-tests.sh [app] [delete modes...]
#   app: gtk-textview (default) | gtk-entry | chrome
#   delete modes: surrounding forward auto (default: all three)
# Needs an X display (WSLg gives one), xdotool, ibus, and the engine installed with install-dev.sh.
set -u
APP="${1:-gtk-textview}"; shift || true
MODES=("$@"); [ ${#MODES[@]} -eq 0 ] && MODES=(surrounding forward auto)
HERE="$(cd "$(dirname "$0")" && pwd)"
OUT=/tmp/autolang-test.txt
CONF="${XDG_CONFIG_HOME:-$HOME/.config}/autolang"
LOG="${XDG_STATE_HOME:-$HOME/.local/state}/autolang/spike.log"
mkdir -p "$CONF"
export GTK_IM_MODULE=ibus QT_IM_MODULE=ibus XMODIFIERS=@im=ibus GDK_BACKEND=x11

start_ibus() { # $1 = delete mode
    printf 'AUTOLANG_LAYOUTS=us,il\nAUTOLANG_DELETE=%s\nAUTOLANG_LOG_TEXT=1\nAUTOLANG_FORWARD_DELAY_MS=%s\nAUTOLANG_FOCUS_RESETS_LANG=1\n' "$1" "${FORWARD_DELAY_MS:-0}" > "$CONF/spike.env"
    ibus exit >/dev/null 2>&1; pkill -f autolang-ibus; sleep 0.5
    ibus-daemon -drx --panel=disable
    for _ in $(seq 50); do ibus engine >/dev/null 2>&1 && break; sleep 0.1; done
    ibus engine autolang 2>/dev/null
    [ "$(ibus engine 2>/dev/null)" = autolang ] || { echo "cannot select the autolang engine"; exit 1; }
    for _ in $(seq 100); do grep -q "languages ready" "$LOG" 2>/dev/null && break; sleep 0.1; done
    sleep 2 # the first app launched right after a daemon restart sometimes loses its first keys
}

open_app() {
    : > "$OUT"
    case "$APP" in
        gtk-textview) python3 "$HERE/testapp.py" "$OUT" textview & ;;
        gtk-entry) python3 "$HERE/testapp.py" "$OUT" entry & ;;
        chrome)
            google-chrome --user-data-dir=/tmp/autolang-chrome --no-first-run --no-default-browser-check \
                --ozone-platform="${CHROME_PLATFORM:-x11}" --password-store=basic \
                --new-window "file://$HERE/testpage.html" >/dev/null 2>&1 & ;;
        vscode)
            mkdir -p /tmp/autolang-vscode/User
            printf '%s\n' '{"editor.quickSuggestions":{"other":false,"comments":false,"strings":false},"editor.wordBasedSuggestions":"off","editor.acceptSuggestionOnEnter":"off","editor.suggestOnTriggerCharacters":false,"files.autoSave":"off","workbench.startupEditor":"none","security.workspace.trust.enabled":false,"update.mode":"none","telemetry.telemetryLevel":"off","window.restoreWindows":"none","editor.autoClosingQuotes":"never","editor.autoClosingBrackets":"never"}' > /tmp/autolang-vscode/User/settings.json
            : > /tmp/vscode-test.txt
            /usr/share/code/code --user-data-dir=/tmp/autolang-vscode --extensions-dir=/tmp/autolang-vscode/ext \
                --ozone-platform=x11 --password-store=basic --new-window /tmp/vscode-test.txt >/dev/null 2>&1 & ;;
    esac
    APP_PID=$!
    local name="AutoLang test"; [ "$APP" = chrome ] && name="^AL:"; [ "$APP" = vscode ] && name="vscode-test"
    for _ in $(seq 300); do WIN=$(xdotool search --name "$name" 2>/dev/null | head -1); [ -n "$WIN" ] && break; sleep 0.1; done
    [ -z "${WIN:-}" ] && { echo "app window did not appear"; return 1; }
    xdotool windowactivate --sync "$WIN" 2>/dev/null || xdotool windowfocus --sync "$WIN"
    [ "$APP" = chrome ] && { sleep 1; xdotool mousemove --window "$WIN" 200 150 click 1; }
    [ "$APP" = vscode ] && { sleep 3; xdotool mousemove --window "$WIN" 400 200 click 1; }
    sleep 0.5
}

read_text() {
    sleep 0.6
    if [ "$APP" = chrome ]; then
        xdotool getwindowname "$WIN" | sed -E 's/^AL://; s/ - Google Chrome$//' | python3 -c 'import sys,urllib.parse;print(urllib.parse.unquote(sys.stdin.read().rstrip("\n")),end="")'
    else
        if [ "$APP" = vscode ]; then xdotool key ctrl+s; sleep 1; cat /tmp/vscode-test.txt; else cat "$OUT"; fi
    fi
}

close_app() { kill "$APP_PID" 2>/dev/null; pkill -f autolang-chrome 2>/dev/null; pkill -f autolang-vscode 2>/dev/null; wait "$APP_PID" 2>/dev/null; sleep 0.3; }

# A step: "t:keys" types keys, "k:keysyms" presses keys (xdotool key syntax), "s:2" taps Shift twice.
run_case() { # name expected steps...
    local name="$1" expected="$2"; shift 2
    open_app || { echo "FAIL $name: no window"; FAILS=$((FAILS+1)); return; }
    for step in "$@"; do
        case "$step" in
            t:*) xdotool type --delay 60 "${step#t:}" ;;
            k:*) xdotool key --delay 60 ${step#k:} ;;
            s:2) xdotool key --delay 80 Shift_L Shift_L ;;
        esac
        sleep 0.2
    done
    # GTK widgets hold preedit outside their text; End commits it (a no-op for the text itself: the caret is at the end).
    [ "$CURRENT_MODE" = preedit ] && [ "$APP" != chrome ] && { xdotool key End; sleep 0.2; }
    local got; got="$(read_text; echo x)"; got="${got%x}" # the x keeps a trailing newline from $(...)
    close_app
    if [ "$got" = "$expected" ]; then echo "ok   $name"; else echo "FAIL $name: expected [${expected//$'\n'/\\n}] got [${got//$'\n'/\\n}]"; FAILS=$((FAILS+1)); fi
}

FAILS=0
NL=$'\n'
for mode in "${MODES[@]}"; do
    echo "== $APP, delete mode $mode"
    CURRENT_MODE="$mode"
    start_ibus "$mode"
    run_case "plain english"        "hello world "      "t:hello world "
    run_case "one word to hebrew"   "שלום "             "t:akuo "
    run_case "there and back"       "שלום hello "       "t:akuo hello "
    run_case "two words"            "שלום לכולם "       "t:akuo kfuko "
    [ "$APP" != gtk-entry ] && run_case "plain enter" "hi${NL}" "t:hi" "k:Return"
    [ "$APP" != gtk-entry ] && run_case "enter" "שלום${NL}" "t:akuo" "k:Return"
    run_case "backspace in word"    "שלום "             "t:akuoo" "k:BackSpace" "t: "
    run_case "arrow key mid word"   "hellxo"            "t:hello" "k:Left" "t:x"
    run_case "double shift undo"    "akuo "             "t:akuo " "s:2"
    run_case "double shift convert" "הי"                "t:vh" "s:2"
done
echo "failures: $FAILS"
grep -E "via|capabilities|purpose|warn" "$LOG" | tail -40
exit $FAILS
