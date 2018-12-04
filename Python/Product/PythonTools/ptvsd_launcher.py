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

"""
Starts Debugging, expected to start with normal program
to start as first argument and directory to run from as
the second argument.
"""

__author__ = "Microsoft Corporation <ptvshelp@microsoft.com>"
__version__ = "3.2.0.0"

import os
import os.path
import sys
import traceback

# Arguments are:
# 1. Working directory.
# 2. VS debugger port to connect to.
# 3. GUID for the debug session.
# 4. Debug options (as list of names - see enum PythonDebugOptions).
# 5. '-g' to use the installed ptvsd package, rather than bundled one.
# 6. '-m' or '-c' to override the default run-as mode. [optional]
# 7. Startup script name.
# 8. Script arguments.

# change to directory we expected to start from
os.chdir(sys.argv[1])

port_num = int(sys.argv[2])
debug_id = sys.argv[3]
debug_options = set([opt.strip() for opt in sys.argv[4].split(',')])

del sys.argv[0:5]

# Use bundled ptvsd or not?
bundled_ptvsd = True
if sys.argv and sys.argv[0] == '-g':
    bundled_ptvsd = False
    del sys.argv[0]

# set run_as mode appropriately
run_as = 'script'
if sys.argv and sys.argv[0] == '-m':
    run_as = 'module'
    del sys.argv[0]
if sys.argv and sys.argv[0] == '-c':
    run_as = 'code'
    del sys.argv[0]

# preserve filename before we del sys
filename = sys.argv[0]

# fix sys.path to be the script file dir
sys.path[0] = ''

if not bundled_ptvsd and (sys.platform == 'cli' or sys.version_info < (2, 7) or
    (sys.version_info >= (3, 0) and sys.version_info < (3, 4))):
    # This is experimental debugger incompatibility. Exit immediately.
    # This process will be killed by VS since it does not see a debugger
    # connect to it. The exit code we will get there will be wrong.
    # 687: ERROR_DLL_MIGHT_BE_INCOMPATIBLE
    sys.exit(687)

# Load the debugger package
try:
    ptvs_lib_path = None
    if bundled_ptvsd:
        ptvs_lib_path = os.path.dirname(__file__)
        sys.path.insert(0, ptvs_lib_path)
    else:
        ptvs_lib_path = os.path.join(os.path.dirname(__file__), 'Packages')
        sys.path.append(ptvs_lib_path)
    try:
        import ptvsd
        import ptvsd.debugger as vspd
        ptvsd_loaded = True
    except ImportError:
        ptvsd_loaded = False
        raise
    vspd.DONT_DEBUG.append(os.path.normcase(__file__))
except:
    traceback.print_exc()
    if not bundled_ptvsd and not ptvsd_loaded:
        # This is experimental debugger import error. Exit immediately.
        # This process will be killed by VS since it does not see a debugger
        # connect to it. The exit code we will get there will be wrong.
        # 126 : ERROR_MOD_NOT_FOUND
        sys.exit(126)
    print('''
Internal error detected. Please copy the above traceback and report at
https://go.microsoft.com/fwlink/?LinkId=293415

Press Enter to close. . .''')
    try:
        raw_input()
    except NameError:
        input()
    sys.exit(1)
finally:
    if ptvs_lib_path:
        sys.path.remove(ptvs_lib_path)

# and start debugging
vspd.debug(filename, port_num, debug_id, debug_options, run_as)
