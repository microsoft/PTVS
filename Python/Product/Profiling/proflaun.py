"""
Starts profiling, expected to start with normal program
to start as first argument and directory to run from as
the second argument.
"""

import sys
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
vspyprof.profile(__file__, globals(), locals(), profdll)
