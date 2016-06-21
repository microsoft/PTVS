#! /usr/bin/env python3

from __future__ import print_function

import argparse
import os
import re
import sys

def adjust_path(path, root, base):
    rel = os.path.relpath(path, start=root)
    return base if rel == '.' else os.path.join(base, rel)

class StdOutContext(object):
    def __enter__(self): return sys.stdout
    def __exit__(self, et, ev, etb): pass

def open_or_stdout(filename):
    if filename:
        return open(filename, 'a', encoding='utf-8')
    return StdOutContext()

def main():
    parser = argparse.ArgumentParser(description="Generates SWR lines from a directory")
    parser.add_argument('--source', '-s', dest='source', type=str, help='source directory')
    parser.add_argument('--out', '-o', dest='output', type=str, default=None, help='append output to this file')
    parser.add_argument('--base', '-b', dest='install', type=str, help='target directory base')
    parser.add_argument('--include', '-i', dest='inclusions', type=str, action='append', help='regex patterns for filenames to include')
    parser.add_argument('--exclude', '-x', dest='exclusions', type=str, action='append', help='regex patterns for filenames to skip')
    
    args = parser.parse_args()
    
    inclusions = [re.compile(pattern, re.I) for pattern in (args.inclusions or [])]
    exclusions = [re.compile(pattern, re.I) for pattern in (args.exclusions or [])]
    
    with open_or_stdout(args.output) as out:
        for root, dirs, files in os.walk(args.source):
            files = [f for f in files if (not inclusions or any(r.search(f) for r in inclusions)) and not any(r.search(f) for r in exclusions)]
            
            if files:
                print(file=out)
                print('folder "', adjust_path(root, args.source, args.install), '"', sep='', file=out)
                for f in files:
                    print('  file source="', os.path.join(root, f), '"', sep='', file=out)

if __name__ == '__main__':
    sys.exit(main() or 0)