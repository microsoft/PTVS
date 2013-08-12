import thread
l = thread.allocate_lock()
l.acquire()

def my_thread():
   print('100')
   print('200')
   l.release()

thread.start_new_thread(my_thread, ())
l.acquire()