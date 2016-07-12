#! /usr/bin/env python3

from __future__ import print_function

import binascii
import json
import glob
import hashlib
import os
import sys
import traceback

def process_payload(payload):
    sha256 = hashlib.new('sha256')
    sha1 = hashlib.new('sha1')
    with open(payload['fileName'], 'rb') as f:
        all_data = f.read()
    sha256.update(all_data)
    sha1.update(all_data)
    
    payload['size'] = len(all_data)
    try:
        payload['_buildInfo']['crc'] = str(binascii.crc32(all_data))
    except KeyError:
        pass
    try:
        payload['_buildInfo']['sha1'] = sha1.hexdigest().upper()
    except KeyError:
        pass
    payload['sha256'] = sha256.hexdigest().upper()

def process(data):
    for package in data['packages']:
        for p in package['payloads']:
            process_payload(p)
    return data

def main():
    success = 0
    files = []
    for n in sys.argv[1:]:
        if '*' in n or '?' in n:
            files.extend(glob.glob(n))
        else:
            files.append(n)
    
    for n in files:
        try:
            with open(n, 'r', encoding='utf-8') as f:
                data = process(json.load(f))
        except Exception:
            print('Unable to process', n, file=sys.stderr)
            traceback.print_exc()
            continue
        
        with open(n, 'w', encoding='utf-8') as f:
            json.dump(data, f, indent=2, sort_keys=True)
        success += 1
    
    print('Processed', success, 'file(s)')

if __name__ == '__main__':
    sys.exit(main() or 0)