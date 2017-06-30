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
# MERCHANTABLITY OR NON-INFRINGEMENT.
# 
# See the Apache Version 2.0 License for specific language governing
# permissions and limitations under the License.

__author__ = "Microsoft Corporation <ptvshelp@microsoft.com>"
__version__ = "3.0.0.0"

"""
Starts Debugging, expected to start with normal program
to start as first argument and directory to run from as
the second argument.
"""
import os
import sys
import ptvsd
import ptvsd.debugger as vspd

def debug_remote(
    file, 
    port_num,
    debug_id,
    wait_on_exception,
    redirect_output, 
    wait_on_exit,
    break_on_systemexit_zero,
    debug_stdlib,
    run_as
):
    global BREAK_ON_SYSTEMEXIT_ZERO, DEBUG_STDLIB
    BREAK_ON_SYSTEMEXIT_ZERO = break_on_systemexit_zero
    DEBUG_STDLIB = debug_stdlib

    import datetime
    print('%s: Remote launcher starting ptvsd attach wait with File: %s, Port: %d, Id: %s\n' % (datetime.datetime.now(), file, port_num, debug_id))

    ptvsd.enable_attach(debug_id, address = ('0.0.0.0', port_num), redirect_output = redirect_output)
    try:
        import _ptvsdhelper
        if _ptvsdhelper.ping_debugger_for_attach():
            ptvsd.wait_for_attach()
    except ImportError:
        _ptvsdhelper = None

    # now execute main file
    globals_obj = {'__name__': '__main__'}
    if run_as == 'module':
        vspd.exec_module(file, globals_obj)
    elif run_as == 'code':
        vspd.exec_code(file, '<string>', globals_obj)
    else:
        vspd.exec_file(file, globals_obj)

# arguments are port, debug id, normal arguments which should include a filename to execute

# change to directory we expected to start from
port_num = int(sys.argv[1])
debug_id = sys.argv[2]

del sys.argv[0:3]

wait_on_exception = False
redirect_output = False
wait_on_exit = False
break_on_systemexit_zero = False
debug_stdlib = False
run_as = 'script'

for opt in [
    # Order is important for these options.
    'redirect_output',
    'wait_on_exception',
    'wait_on_exit',
    'break_on_systemexit_zero',
    'debug_stdlib'
]:
    if sys.argv and sys.argv[0] == '--' + opt.replace('_', '-'):
        globals()[opt] = True
        del sys.argv[0]

# set run_as mode appropriately
if sys.argv and sys.argv[0] == '-m':
    run_as = 'module'
    del sys.argv[0]

if sys.argv and sys.argv[0] == '-c':
    run_as = 'code'
    del sys.argv[0]

# preserve filename before we del sys
file = sys.argv[0]

# fix sys.path to be the script file dir
sys.path[0] = ''

# exclude ourselves from being debugged
vspd.DONT_DEBUG.append(os.path.normcase(__file__))

# remove all state we imported
del sys, os

import sys

debug_remote(
    file, 
    port_num,
    debug_id,
    wait_on_exception,
    redirect_output, 
    wait_on_exit,
    break_on_systemexit_zero,
    debug_stdlib,
    run_as
)
