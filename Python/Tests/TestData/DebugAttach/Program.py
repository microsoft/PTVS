from threading import Thread
import threading
import time


class C(object): pass
global thread_abort
thread_abort = False

def exception_storm():
    my_name = threading.current_thread().name
    my_ident = threading.current_thread().ident
    i = 0
    from weakref import WeakValueDictionary

    d = WeakValueDictionary()

    k = C()
    v = C()
    d[k] = v

    x = C()
    while not thread_abort:
        d.__contains__(x)
        i += 1
        if i % 10000 == 0: print('{} [{}] processed {} exceptions'.format(my_name, my_ident, i))
    print("Exiting")

def lazy_sleeper(sleep_seconds=1):
    my_name = threading.current_thread().name
    my_ident = threading.current_thread().ident
    i = 0
    while not thread_abort:
        time.sleep(sleep_seconds)
        i += 1
        if i % 10 == 0: print('{} [{}] woke up after {} naps'.format(my_name, my_ident, i*sleep_seconds))


def wait_for_threads(threads, timeout=10):
    for t in threads:
        print('joining {} ...'.format(t.name))
        t.join(timeout)
        if t.is_alive(): print('\ttimed out joining {}'.format(t.name))
        else: print('\t{} exited normally'.format(t.name))

if __name__ == '__main__':
        threads = []
        for i in xrange(20):
            threads.append(Thread(target=exception_storm, name='Exceptions-{}'.format(i)))
            threads.append(Thread(target=lazy_sleeper, name='Sleeper-{}'.format(i)))

        for t in threads: t.start()

        try:
            while True:
                wait_for_threads(threads)
        except KeyboardInterrupt:
            thread_abort = True
            wait_for_threads(threads)
