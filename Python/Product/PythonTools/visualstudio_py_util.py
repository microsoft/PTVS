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

def exec_file(file, global_variables):
    '''Executes the provided script as if it were the original script provided
    to python.exe. The functionality is similar to `runpy.run_path`, which was
    added in Python 2.7/3.2.

    The following values in `global_variables` will be set to the following
    values, if they are not already set::
        __name__ = '<run_path>'
        __file__ = file
        __cached__ = None
        __loader__ = None
        __package__ = __name__.rpartition('.')[0]

    The `sys.modules` entry for ``__name__`` will be set to a new module, and
    ``sys.path[0]`` will be changed to the value of `file` without the filename.
    Both values are restored when this function exits.
    '''
    mod_name = global_variables.setdefault('__name__', '<run_path>')
    prev_syspath0 = sys.path[0]
    try:
        try:
            prev_sysmodule = sys.modules[mod_name]
        except KeyError:
            prev_sysmodule = None
        mod = sys.modules[mod_name] = imp.new_module(mod_name)
        mod.__dict__.update(global_variables)
        global_variables = mod.__dict__
        global_variables.setdefault('__file__', file)
        global_variables.setdefault('__cached__', None)
        global_variables.setdefault('__loader__', None)
        global_variables.setdefault('__package__', mod_name.rpartition('.')[0])

        sys.path[0] = os.path.split(file)[0]
        try:
            f = open(file, "rb")
            code_obj = compile(f.read().replace(to_bytes('\r\n'), to_bytes('\n')) + to_bytes('\n'), file, 'exec')
        finally:
            f.close()
        exec(code_obj, global_variables)
    finally:
        sys.path[0] = prev_syspath0
        if prev_sysmodule:
            sys.modules[mod_name] = prev_sysmodule
        else:
            del sys.modules[mod_name]


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
