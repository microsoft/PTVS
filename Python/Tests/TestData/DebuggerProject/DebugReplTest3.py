import threading

stop_requested = False
t1_done = None
t2_done = None

def thread1():
    global t1_done
    t1_done = False
    t1_val = 'thread1'
    while not stop_requested:
        pass
    t1_done = True

def thread2():
    global t2_done
    t2_done = False
    t2_val = 'thread2'
    while not stop_requested:
        pass
    t2_done = True

def threadmain():
    global stop_requested

    t1 = threading.Thread(target=thread1)
    t1.daemon = False
    t1.start()

    t2 = threading.Thread(target=thread2)
    t2.daemon = False
    t2.start()

    # wait until both threads are executing (IronPython needs this)
    while (t1_done is None or t2_done is None):
        pass

    # Set breakpoint here
    stop_requested = True

    while not (t1_done and t2_done):
        pass

threadmain()
