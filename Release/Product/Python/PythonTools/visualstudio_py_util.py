 # ############################################################################
 #
 # Copyright (c) Microsoft Corporation. 
 #
 # This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 # copy of the license can be found in the License.html file at the root of this distribution. If 
 # you cannot locate the Apache License, Version 2.0, please send an email to 
 # vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 # by the terms of the Apache License, Version 2.0.
 #
 # You must not remove this notice, or any other, from this software.
 #
 # ###########################################################################

import sys
import struct

# Py3k compat - alias unicode to str
try:
    unicode
except:
    unicode = str

if sys.version_info[0] >= 3:
    def to_bytes(cmd_str):
        return bytes(cmd_str, 'ascii')
else:
    def to_bytes(cmd_str):
        return cmd_str


UNICODE_PREFIX = to_bytes('U')
ASCII_PREFIX = to_bytes('A')
NONE_PREFIX = to_bytes('N')


def read_bytes(conn, count):
    b = to_bytes('')
    while len(b) < count:
        b += conn.recv(count - len(b))
    return b


def write_bytes(conn, b):
    conn.sendall(b)


def read_int(conn):
    return struct.unpack('!q', read_bytes(conn, 8))[0]


def write_int(conn, i):
    write_bytes(conn, struct.pack('!q', i))


def read_string(conn):
    """ reads length of text to read, and then the text encoded in UTF-8, and returns the string"""
    strlen = read_int(conn)
    if not strlen:
        return ''
    res = to_bytes('')
    while len(res) < strlen:
        res = res + conn.recv(strlen - len(res))

    res = res.decode('utf8')
    if sys.version_info[0] == 2 and sys.platform != 'cli':
        # Py 2.x, we want an ASCII string if possible
        try:
            res = res.encode('ascii')
        except UnicodeEncodeError:
            pass

    return res


def write_string(conn, s):
    if s is None:
        write_bytes(conn, NONE_PREFIX)
    elif isinstance(s, unicode):
        b = s.encode('utf8')
        b_len = len(b)
        write_bytes(conn, UNICODE_PREFIX)
        write_int(conn, b_len)
        if b_len > 0:
            write_bytes(conn, b)
    else:
        s_len = len(s)
        write_bytes(conn, ASCII_PREFIX)
        write_int(conn, s_len)
        if s_len > 0:
            write_bytes(conn, s)
