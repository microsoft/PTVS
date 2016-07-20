import os, sys
sys.path.append(os.path.abspath('EGG.egg'))

import EGG.import_handled_exception

try:
    import EGG.import_unhandled_exception
except ValueError:
    pass
