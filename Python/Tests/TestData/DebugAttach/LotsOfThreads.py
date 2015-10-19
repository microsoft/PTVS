from threading import Thread, current_thread

num_threads = 40

def loop_forever():
    i = 0
    while True: 
        i = (i + 1) % 100000000
        if i % 10000 == 0: print("Thread {0} [{1}] is making progress.".format(current_thread().name, current_thread().ident))

if __name__ == "__main__":
    ts = []
    for i in xrange(num_threads):
        ts.append(Thread(target=loop_forever, name="Thread-{0}".format(i)))
    for t in ts: t.start()
    for t in ts: t.join()
