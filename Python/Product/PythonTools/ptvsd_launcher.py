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

"""
Starts Debugging, expected to start with normal program
to start as first argument and directory to run from as
the second argument.
"""

__author__ = "Microsoft Corporation <ptvshelp@microsoft.com>"
__version__ = "3.2.0.0"

import os
import os.path
import re
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
legacy_ptvsd = True
if sys.argv and sys.argv[0] == '-g':
    legacy_ptvsd = False
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

def parse_version(version):
    """Version format is expected to be <int>.<int>.<int><optional str><optional int>"""
    try:
        regex = r"([0-9]+)\.([0-9]+)\.([0-9]+)([a-zA-Z]*)([0-9]*)"
        match = re.match(regex, version.upper(), re.IGNORECASE)
        parser = [int, int, int, str, int]
        default = [0, 0, 0, 'z', 0]
        result = []
        for n in range(0, len(match.groups())):
            m = n + 1
            v = match.group(m)
            result.append(parser[n](v) if v else default[n])
        return result
    except Exception:
        pass
    return []

def get_bundled_packages_path():
    return os.path.join(os.path.dirname(__file__), 'Packages')

def get_bundled_ptvsd_version():
    try:
        ptvsd_path = os.path.join(get_bundled_packages_path(), 'ptvsd', '__init__.py')
        with open(ptvsd_path, 'r') as f:
            lines = f.readlines()
        for line in lines:
            if line.startswith('__version__'):
                _, version = (s.strip().strip('"\'') for s in line.split('='))
                return version
    except Exception:
        pass
    return None

def verify_version():
    try:
        # This import should load the installed version. If this fails it means we will be using bundled ptvsd
        import ptvsd
        installed_version = ptvsd.__version__
        bundled_version = get_bundled_ptvsd_version()
        if bundled_version and installed_version and parse_version(bundled_version) > parse_version(installed_version):
            print('Warning: Installed ptvsd(%s) does not match VS bundled version ptvsd(%s)' % (installed_version, bundled_version))
    except ImportError:
        pass

# Load the debugger package
try:
    ptvs_lib_path = None
    if legacy_ptvsd:
        ptvs_lib_path = os.path.dirname(__file__)
        sys.path.insert(0, ptvs_lib_path)
    else:
        # verify_version must be called before we change sys.path
        verify_version()
        ptvs_lib_path = get_bundled_packages_path()
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
    if not legacy_ptvsd and not ptvsd_loaded:
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
