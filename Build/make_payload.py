'''Quick and dirty script to generate vs.payload blocks for a set of URLs.

Usage:
    make_payload.py URL [URL ...]


'''

__author__ = 'Steve Dower <steve.dower@microsoft.com>'
__version__ = '0.1'

import hashlib
import os
import urllib.request
import sys

for u in sys.argv[1:]:
    is_temp = False
    if os.path.isfile(u):
        p = u
        name = None
    else:
        p, r = urllib.request.urlretrieve(u)
        try:
            name = r.get_filename()
        except:
            name = None
        is_temp = True

    if not name:
        try:
            _, name = os.path.split(u)
        except:
            try:
                _, name = os.path.split(p)
            except:
                name = '<unknown>'

    f_len = 0
    f_hash = hashlib.sha256()
    with open(p, 'rb') as f:
        data = f.read(1024 * 1024)
        while data:
            f_len += len(data)
            f_hash.update(data)
            data = f.read(1024 * 1024)

    if is_temp:
        try:
            os.unlink(p)
        except:
            pass

    print(f'  vs.payload size={f_len}')
    print(f'             url={u}')
    print(f'             fileName={name}')
    print(f'             sha256={f_hash.hexdigest()}')
    print()
