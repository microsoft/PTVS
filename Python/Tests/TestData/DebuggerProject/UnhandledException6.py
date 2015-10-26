import os

try: raise OSError()    # does not break
except os.error: pass

try: raise OSError()    # does not break
except os.path.os.error: pass

class A:
    error = OSError

try: raise OSError()    # breaks
except A.error: pass
