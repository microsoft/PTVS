import sys
sys.path.append('.') # so that we can find ptvsd

import ptvsd
ptvsd.enable_attach('secret', redirect_output=False)
ptvsd.wait_for_attach()

sys.stdout.write('stdout')
sys.stderr.write('stderr')
x = 1
