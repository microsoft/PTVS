# Python Tools for Visual Studio
# Copyright(c) Microsoft Corporation
# All rights reserved.
# 
# Licensed under the Apache License, Version 2.0 (the License); you may not use
# this file except in compliance with the License. You may obtain a copy of the
# License at http://www.apache.org/licenses/LICENSE-2.0
# 
# THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
# OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
# IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
# MERCHANTABILITY OR NON-INFRINGEMENT.
# 
# See the Apache Version 2.0 License for specific language governing
# permissions and limitations under the License.

# This file is run as a startup script when starting a jupyter_client REPL.
# The only global defines should be prefixed with "__ptvs_repl_" to ensure
# they are omitted from global completion lists.

# This file must be ASCII only.

def __ptvs_repl_sig_repr(o):
    import ast
    r = repr(o)
    try:
        ast.literal_eval(r)
    except:
        return type(o).__name__ + '()'
    else:
        return r

def __ptvs_repl_sig(o):
    '''Returns the primary signature for the provided callable object'''
    if not hasattr(o, '__call__'):
        return []

    s1 = getattr(o, '__doc__', None)
    try:
        import inspect
        s2, s3, s4, s5 = getattr(inspect, 'getfullargspec', inspect.getargspec)(o)[:4]
    except:
        s2, s3, s4, s5 = [], None, None, None
    return [(
        str(s1 or ''),
        [str(i2) for i2 in s2],
        str(s3) if s3 else None,
        str(s4) if s4 else None,
        [__ptvs_repl_sig_repr(i5) for i5 in s5] if s5 else []
    )]

def __ptvs_repl_split_args(args):
    a = []
    result = []
    in_quote = False
    escaping = False
    for c in args:
        if c == '"' and not escaping:
            in_quote = not in_quote
        elif c == ' ' and not in_quote:
            result.append(''.join(a))
            a = []
        elif c == '\\':
            if escaping:
                escaping = False
                a.append(c)
            else:
                escaping = True
        else:
            a.append(c)
            escaping = False
    if a:
        result.append(''.join(a))
    return result

def __ptvs_repl_exec_script(filename, args, globals, locals):
    with open(filename, 'rb') as f:
        content = f.read().replace('\\r\\n'.encode('ascii'), '\\n'.encode('ascii'))
    
    import os, sys
    orig_name = globals.get('__name__')
    globals['__name__'] = '__main__'
    orig_file = globals.get('__file__')
    globals['__file__'] = filename
    orig_argv = list(sys.argv)
    sys.argv[:] = [filename]
    sys.argv.extend(__ptvs_repl_split_args(args))
    script_dir = os.path.dirname(filename)
    if script_dir not in sys.path:
        sys.path.insert(0, script_dir)
        pop_path_0 = True
    else:
        pop_path_0 = False
    
    try:
        if sys.version_info[0] == 2:
            # Handle issue on Python 2.x where it will incorrectly encode
            # filename as ASCII instead of the filesystem encoding
            if isinstance(filename, unicode):
                filename = filename.encode(sys.getfilesystemencoding(), 'ignore')
        code = compile(content, filename, 'exec')
        exec(code, globals, locals)
    finally:
        globals['__name__'] = orig_name
        globals['__file__'] = orig_file
        if pop_path_0:
            del sys.path[0]
        sys.argv[:] = orig_argv

def __ptvs_repl_exec_module(module_name, args, globals, locals):
    import os, runpy, sys

    orig_argv = list(sys.argv)
    sys.argv[:] = [filename]
    sys.argv.extend(__ptvs_repl_split_args(args))
    try:
        mod = runpy.run_module(module_name, alter_sys=True)
        for k in mod:
            if not k.startswith('__'):
                globals[k] = mod[k]
    finally:
        sys.argv[:] = orig_argv

def __ptvs_repl_exec_process(filename, args, globals, locals):
    import codecs, subprocess, sys
    out_codec = codecs.lookup(sys.stdout.encoding)

    proc = subprocess.Popen(
        '"%s" %s' % (filename, args),
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        bufsize=0,
    )

    for line in proc.stdout:
        print(out_codec.decode(line, 'replace')[0].rstrip('\r\n'))
    