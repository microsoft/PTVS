import os

try: raise os.error()   # does not break
except: pass

try: raise os.error()   # does not break
except Exception: pass

try: raise os.error()   # does not break
except os.error: pass

os_error = os.error
try: raise os.error()   # does not break
except os_error: pass

def a():
    raise os.error()

try: a()    # does not break
except: pass

try: a()    # does not break
except Exception: pass

try: a()    # does not break
except os.error: pass

try: a()    # does not break
except os_error: pass

def b(var):
    try: a()
    except var: pass

b(Exception)    # does not break
b(os.error)     # does not break

exc = os.error()

try: raise exc      # does not break
except: pass

try: raise exc      # does not break
except Exception: pass

try: raise exc      # does not break
except os.error: pass

try: b(ValueError)  # does not break
except: pass

try: b(os.error)    # does not break
except ValueError: pass

b(ValueError)       # breaks at line 17

