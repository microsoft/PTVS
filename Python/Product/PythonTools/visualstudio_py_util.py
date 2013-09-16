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

import imp
import os
import sys
import struct

# Import encodings early to avoid import on the debugger thread, which may cause deadlock
from encodings import utf_8, ascii

# WARNING: Avoid imports beyond this point, specifically on the debugger thread, as this may cause
# deadlock where the debugger thread performs an import while a user thread has the import lock

# Py3k compat - alias unicode to str
try:
    unicode
except:
    unicode = str

if sys.version_info[0] >= 3:
    def to_bytes(cmd_str):
        return ascii.Codec.encode(cmd_str)[0]
else:
    def to_bytes(cmd_str):
        return cmd_str

def exec_file(file, global_variables):
    '''Executes the provided script as if it were the original script provided
    to python.exe. The functionality is similar to `runpy.run_path`, which was
    added in Python 2.7/3.2.

    The following values in `global_variables` will be set to the following
    values, if they are not already set::
        __name__ = '<run_path>'
        __file__ = file
        __package__ = __name__.rpartition('.')[0] # 2.6 and later
        __cached__ = None # 3.2 and later
        __loader__ = None # 3.3 and later

    The `sys.modules` entry for ``__name__`` will be set to a new module, and
    ``sys.path[0]`` will be changed to the value of `file` without the filename.
    Both values are restored when this function exits.
    '''
    global_variables = dict(global_variables)
    mod_name = global_variables.setdefault('__name__', '<run_path>')
    mod = sys.modules[mod_name] = imp.new_module(mod_name)
    mod.__dict__.update(global_variables)
    global_variables = mod.__dict__
    global_variables.setdefault('__file__', file)
    if sys.version_info[0] > 2 or sys.version_info[1] >= 6:
        global_variables.setdefault('__package__', mod_name.rpartition('.')[0])
    if sys.version_info[0] >= 3:
        if sys.version_info[0] > 3 or sys.version_info[1] >= 2:
            global_variables.setdefault('__cached__', None)
        if sys.version_info[0] > 3 or sys.version_info[1] >= 3:
            global_variables.setdefault('__loader__', None)

    sys.path[0] = os.path.split(file)[0]
    f = open(file, "rb")
    try:
        code_obj = compile(f.read().replace(to_bytes('\r\n'), to_bytes('\n')) + to_bytes('\n'), file, 'exec')
    finally:
        f.close()
    exec(code_obj, global_variables)


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

    res = utf_8.decode(res)[0]
    if sys.version_info[0] == 2 and sys.platform != 'cli':
        # Py 2.x, we want an ASCII string if possible
        try:
            res = ascii.Codec.encode(res)[0]
        except UnicodeEncodeError:
            pass

    return res


def write_string(conn, s):
    if s is None:
        write_bytes(conn, NONE_PREFIX)
    elif isinstance(s, unicode):
        b = utf_8.encode(s)[0]
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
