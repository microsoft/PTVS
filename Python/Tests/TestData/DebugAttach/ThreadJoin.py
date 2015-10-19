from threading import Thread
exit_flag = False
def f():
    i = 1
    while not exit_flag:
        i = (i + 1) % 100000000
        if i % 100000 == 0: print("f making progress: {0}".format(i))

from threading import Thread

if __name__ == '__main__':
    t1 = Thread(target=f,name="F_thread")
    t1.start()
    t1.join()
