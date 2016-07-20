try:
    import _thread
except ImportError:
    import thread as _thread
l = _thread.allocate_lock()
l.acquire()

def my_thread():
   print('100')
   print('200')
   l.release()

_thread.start_new_thread(my_thread, ())
l.acquire()