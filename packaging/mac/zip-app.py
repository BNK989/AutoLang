"""Zips AutoLang.app the way ditto would, from a machine without unix file modes: directories 0755, files 0644,
and the executable in Contents/MacOS 0755. Usage: zip-app.py <path/to/AutoLang.app> <out.zip>"""
import os
import sys
import zipfile

app, out = os.path.abspath(sys.argv[1]), sys.argv[2]
parent = os.path.dirname(app)

with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as z:
    for dirpath, dirnames, filenames in os.walk(app):
        dirnames.sort()
        rel_dir = os.path.relpath(dirpath, parent).replace(os.sep, "/")
        info = zipfile.ZipInfo(rel_dir + "/")
        info.create_system = 3  # unix, so external_attr carries the mode
        info.external_attr = (0o40755 << 16) | 0x10
        z.writestr(info, b"")
        for name in sorted(filenames):
            path = os.path.join(dirpath, name)
            rel = rel_dir + "/" + name
            mode = 0o100755 if rel_dir.endswith("Contents/MacOS") else 0o100644
            info = zipfile.ZipInfo.from_file(path, rel)
            info.create_system = 3
            info.external_attr = mode << 16
            info.compress_type = zipfile.ZIP_DEFLATED
            with open(path, "rb") as f:
                z.writestr(info, f.read())
