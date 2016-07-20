import traceback
import sys
try:
    sys.setcheckinterval(1)
except NotImplementedError:
    # setcheckinterval not implemented in IronPython
    pass

try:
    import thread
except:
    import _thread as thread

def f(): 
    print('new thread')

x = 1000000

try:
    while True:
        try:
            thread.start_new_thread(f, ())
            import time
            time.sleep(.05)
        except: 
            # not enough memory for another thread
            print('Failed to create new thread')
            traceback.print_exc()
        for i in range(100000):
            pass

except:
    traceback.print_exc()
    input()