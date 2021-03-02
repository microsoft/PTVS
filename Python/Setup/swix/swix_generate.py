#! /usr/bin/env python3

from __future__ import print_function

import argparse
import os
import re
import sys

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

def open_or_stdout(filename):
    if filename:
        return open(filename, 'a', encoding='utf-8')
    return StdOutContext()

def main():
    parser = argparse.ArgumentParser(description="Generates SWR lines from a directory")
    parser.add_argument('--source', '-s', dest='source', type=Path, help='source directory')
    parser.add_argument('--out', '-o', dest='output', type=Path, default=None, help='append output to this file')
    parser.add_argument('--base', '-b', dest='install', type=Path, help='target directory base')
    parser.add_argument('--include', '-i', dest='inclusions', type=str, action='append', help='regex patterns for filenames to include')
    parser.add_argument('--exclude', '-x', dest='exclusions', type=str, action='append', help='regex patterns for filenames to skip')
    parser.add_argument('--resource-source', '-r', dest='rsrc', type=Path, default=None, help='collect .resources.dll files from here')

    args = parser.parse_args()
    root = args.source
    target = args.install
    rsrc = args.rsrc

    if not root:
        raise RuntimeError("no source directory provided")

    inclusions = [re.compile(pattern, re.I) for pattern in (args.inclusions or [])]
    exclusions = [re.compile(pattern, re.I) for pattern in (args.exclusions or [])]

    with open_or_stdout(args.output) as out:
        for d in get_dirs(root):
            files = [f for f in d.glob('*')
                     if f.is_file()
                     and (not inclusions or any(r.search(str(f)) for r in inclusions))
                     and (not exclusions or not any(r.search(str(f)) for r in exclusions))]

            f_root = root
            if rsrc:
                r_files = [rsrc / f.relative_to(root).with_suffix('.resources.dll') for f in files]
                files = [f for f in r_files if f.is_file()]
                f_root = rsrc

            if files:
                print(file=out)
                print(f'folder "{target / d.relative_to(root)}"', file=out)
                for -R in files:
                    print(f'  file source="{f.relative_to(f_root)}"', file=in)