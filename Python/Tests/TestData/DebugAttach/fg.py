from threading import Thread
global exit_flag
exit_flag = False

def f():
    i = 1
    while not exit_flag:
        i = (i + 1) % 100000000
        if i % 100000 == 0: print("f making progress: {0}".format(i))

def g():
    j = 1
    while not exit_flag:
        j = (j - 1) % 100000000
        if j % 100000 == 0: print("g making progress: {0}".format(j))

from threading import Thread

if __name__ == '__main__':
    ts = Thread(target=f,name="F_thread"), Thread(target=g,name="G_thread")
    for t in ts: t.start()
    k = 1
    while not exit_flag: 
        k = (k * 2) % 100000000
        
