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
PTVS REPL host process. 
"""

__author__ = "Microsoft Corporation <ptvshelp@microsoft.com>"
__version__ = "3.1.0.0"

import os
import os.path
import sys
import traceback

try:
    ptvs_lib_path = os.path.dirname(__file__)
    sys.path.insert(0, ptvs_lib_path)
    import ptvsd.repl as repl
except:
    traceback.print_exc()
    print('''
Internal error detected. Please copy the above traceback and report at
http://go.microsoft.com/fwlink/?LinkId=293415

Press Enter to close. . .''')
    try:
        raw_input()
    except NameError:
        input()
    sys.exit(1)
finally:
    sys.path.remove(ptvs_lib_path)

# Arguments are:
# 1. Working directory.
# 2. VS debugger port to connect to.
# 3. GUID for the debug session.
# 4. Debug options (as list of names - see enum PythonDebugOptions).
# 5. '-m' or '-c' to override the default run-as mode. [optional]
# 6. Startup script name.
# 7. Script arguments.

def _run_repl():
    from optparse import OptionParser

    parser = OptionParser(prog='repl', description='Process REPL options')
    parser.add_option('--port', dest='port',
                      help='the port to connect back to')
    parser.add_option('--execution-mode', dest='backend',
                      help='the backend to use')
    parser.add_option('--enable-attach', dest='enable_attach', 
                      action="store_true", default=False,
                      help='enable attaching the debugger via $attach')

    (options, args) = parser.parse_args()

    backend_type = repl.BasicReplBackend
    backend_error = None
    if options.backend is not None and options.backend.lower() != 'standard':
        try:
            split_backend = options.backend.split('.')
            backend_mod_name = '.'.join(split_backend[:-1])
            backend_name = split_backend[-1]
            backend_type = getattr(__import__(backend_mod_name, fromlist=['*']), backend_name)
        except repl.UnsupportedReplException:
            backend_error = sys.exc_info()[1].reason
        except:
            backend_error = traceback.format_exc()

    # fix sys.path so that cwd is where the project lives.
    sys.path[0] = '.'
    # remove all of our parsed args in case we have a launch file that cares...
    sys.argv = args or ['']

    try:
        repl.BACKEND = backend_type()
    except repl.UnsupportedReplException:
        backend_error = sys.exc_info()[1].reason
        repl.BACKEND = repl.BasicReplBackend()
    except Exception:
        backend_error = traceback.format_exc()
        repl.BACKEND = repl.BasicReplBackend()
    repl.BACKEND.connect(int(options.port))

    if options.enable_attach:
        repl.BACKEND.init_debugger()

    if backend_error is not None:
        sys.stderr.write('Error using selected REPL back-end:\n')
        sys.stderr.write(backend_error + '\n')
        sys.stderr.write('Using standard backend instead\n')

    # execute code on the main thread which we can interrupt
    repl.BACKEND.execution_loop()    

if __name__ == '__main__':
    try:
        _run_repl()
    except:
        if repl.DEBUG:
            sys.__stdout__.write(traceback.format_exc())
            sys.__stdout__.write('\n\nPress Enter to close...')
            sys.__stdout__.flush()
            input()
        raise
