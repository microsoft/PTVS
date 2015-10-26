"""
Starts profiling, expected to start with normal program
to start as first argument and directory to run from as
the second argument.
"""

import sys

if sys.platform == 'cli':
    print('Python profiling is not supported on IronPython, press enter to exit...')
    raw_input()
    sys.exit(1)

import vspyprof
import os

# arguments are path to profiling DLL, working dir, normal arguments which should include a filename to execute

# change to directory we expected to start from
os.chdir(sys.argv[2])
profdll = sys.argv[1]

# fix sys.path to be our real starting dir, not this one
sys.path[0] = sys.argv[2]
del sys.argv[0:3]	

# set file appropriately, fix up sys.argv...
__file__ = sys.argv[0]

# remove all state we imported
del sys, os

# and start profiling
try:
    vspyprof.profile(__file__, globals(), locals(), profdll)
except SystemExit:
    import sys, msvcrt, os
    if sys.exc_info()[1].code:
        env_var = 'VSPYPROF_WAIT_ON_ABNORMAL_EXIT'
    else:
        env_var = 'VSPYPROF_WAIT_ON_NORMAL_EXIT'
    if env_var in os.environ:
        sys.stdout.write('Press any key to continue . . .')
        sys.stdout.flush()
        msvcrt.getch()
except:
    import sys, msvcrt, os, traceback
    if 'VSPYPROF_WAIT_ON_ABNORMAL_EXIT' in os.environ:
        traceback.print_exc()
        sys.stdout.write('Press any key to continue . . .')
        sys.stdout.flush()
        msvcrt.getch()
    else:
        raise
else:
    import sys, msvcrt, os
    if 'VSPYPROF_WAIT_ON_NORMAL_EXIT' in os.environ:
        sys.stdout.write('Press any key to continue . . .')
        sys.stdout.flush()
        msvcrt.getch()
