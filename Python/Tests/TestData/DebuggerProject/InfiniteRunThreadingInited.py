try:
    import thread
except ImportError:
    import _thread as thread

thread.start_new_thread(lambda: 42, ())

x = 1000000

while True:
    y = x
    z = x + 1
    x = z + 1
