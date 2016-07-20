import threading, time, nt

print ('main thread', nt.getpid())

class MyThread(threading.Thread):
    def run(self):
        print('running', nt.getpid())
        time.sleep(10)
        print('done running')


mt = MyThread()
mt.start()
print('joining')
mt.join()
print('done joining')
