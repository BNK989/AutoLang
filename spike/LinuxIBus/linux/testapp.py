#!/usr/bin/env python3
"""A GTK 3 text field for the spike tests. Writes its whole text to a file on every change.

usage: testapp.py OUT_FILE [textview|entry]
Run with GTK_IM_MODULE=ibus so keys go through IBus.
"""
import sys

import gi

gi.require_version("Gtk", "3.0")
from gi.repository import Gtk  # noqa: E402

out = sys.argv[1]
kind = sys.argv[2] if len(sys.argv) > 2 else "textview"


def save(text):
    with open(out, "w", encoding="utf-8") as f:
        f.write(text)


save("")
win = Gtk.Window(title="AutoLang test")
win.set_default_size(500, 200)
if kind == "entry":
    widget = Gtk.Entry()
    widget.connect("changed", lambda e: save(e.get_text()))
else:
    widget = Gtk.TextView()
    buf = widget.get_buffer()
    buf.connect("changed", lambda b: save(b.get_text(b.get_start_iter(), b.get_end_iter(), True)))
win.add(widget)
win.connect("destroy", Gtk.main_quit)
win.show_all()
widget.grab_focus()
Gtk.main()
