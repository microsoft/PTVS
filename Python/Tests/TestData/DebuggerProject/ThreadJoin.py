from threading import Thread
global exit_flag
exit_flag = False
def g():
    i = 1
    while not exit_flag:
        i = (i + 1) % 100000000
        if i % 100000 == 0: print("f making progress: {0}".format(i))

def f():
    g()

def n():
    t1 = Thread(target=f,name="F_thread")
    t1.start()
    t1.join()

def m():
    n()

if __name__ == '__main__':
    m()
