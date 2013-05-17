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

"""
Starts Debugging, expected to start with normal program
to start as first argument and directory to run from as
the second argument.
"""
import sys
import visualstudio_py_debugger
import os

# arguments are working dir, port, normal arguments which should include a filename to execute

# change to directory we expected to start from
os.chdir(sys.argv[1])

port_num = int(sys.argv[2])
debug_id = sys.argv[3]
del sys.argv[0:4]

wait_on_exception = False
redirect_output = False
wait_on_exit = False
break_on_systemexit_zero = False
debug_stdlib = False
django_debugging = False
if len(sys.argv) >= 1 and sys.argv[0] == '--wait-on-exception':
    wait_on_exception = True
    del sys.argv[0]

if len(sys.argv) >= 1 and sys.argv[0] == '--wait-on-exit':
    wait_on_exit = True
    del sys.argv[0]

if len(sys.argv) >= 1 and sys.argv[0] == '--redirect-output':
    redirect_output = True
    del sys.argv[0]

if len(sys.argv) >= 1 and sys.argv[0] == '--break-on-systemexit-zero':
    break_on_systemexit_zero = True
    del sys.argv[0]
    # set file appropriately, fix up sys.argv...
    
if len(sys.argv) >= 1 and sys.argv[0] == '--debug-stdlib':
    debug_stdlib = True
    del sys.argv[0]

if len(sys.argv) >= 1 and sys.argv[0] == '--django-debugging':
    django_debugging = True
    del sys.argv[0]

__file__ = sys.argv[0]

# fix sys.path to be the script file dir
sys.path[0] = os.path.split(sys.argv[0])[0]

# remove all state we imported
del sys, os

# and start debugging
visualstudio_py_debugger.debug(__file__, 
                                port_num, 
                                debug_id, 
                                globals(), 
                                locals(), 
                                wait_on_exception, 
                                redirect_output, 
                                wait_on_exit,
                                break_on_systemexit_zero,
                                debug_stdlib,
                                django_debugging)
