#! /usr/bin/env python3

from __future__ import print_function

import argparse
import json
import os
import re
import sys
import zipfile

from pathlib import Path

def adjust_path(path, root, base):
    rel = os.path.relpath(path, start=root)
    return base if rel == '.' else os.path.join(base, rel)

class StdOutContext(object):
    def __enter__(self): return sys.stdout
    def __exit__(self, et, ev, etb): pass

def get_dirs(root):
    if not root:
        return
    yield root
    for d in root.glob("*"):
        if d.is_dir():
            yield from get_dirs(d)

def quote(s):
    if not s:
        return '""'
    if not isinstance(s, str):
        s = str(s)
    if ' ' not in s:
        return s
    if '"' not in s:
        return '"' + s + '"'
    if "'" not in s:
        return "'" + s + "'"
    return '"' + s.replace('"', '\\"') + '"'

def open_or_stdout(filename):
    if filename:
        return open(filename, 'w', encoding='utf-8')
    return StdOutContext()

def main():
    parser = argparse.ArgumentParser(description="Generates SWR lines from a directory")
    parser.add_argument('--payload', '-p', dest='payload', type=Path, help='path to VSIX file')
    parser.add_argument('--name', '-n', dest='name', type=str, help='name of the generated package')
    parser.add_argument('--version', dest='version', type=str, help='version of the generated package')
    parser.add_argument('--out', '-o', dest='output', type=Path, default=None, help='append output to this file')

    args = parser.parse_args()

    with open_or_stdout(args.output) as out:
        with zipfile.ZipFile(str(args.payload), 'r') as z:
            with z.open('manifest.json') as f:
                manifest = json.load(f)
        
        print("use vs", file=out)
        print("", file=out)
        print("package name={}".format(quote(args.name)), file=out)
        print("        version={}".format(quote(args.version)), file=out)
        print("        vs.package.type=vsix", file=out)

        try:
            install_sizes = manifest['installSizes']
        except LookupError:
            install_sizes = {'targetDrive': manifest['installSize']}

        if install_sizes:
            print("", file=out)
            print("vs.installSize", file=out)
            for i in install_sizes.items():
                print("  {}={}".format(*i), file=out)

        print("", file=out)

        print("vs.payloads", file=out)
        print("  vs.payload source={}".format(quote(args.payload.resolve())), file=out)
        print("             size={}".format(args.payload.stat().st_size), file=out)

if __name__ == '__main__':
    sys.exit(main() or 0)