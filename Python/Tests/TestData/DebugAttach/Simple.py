from threading import Thread, current_thread, Lock
from time import sleep
import time

report_progress_now = []
progress_lock = Lock()

def check_report_progress(me, id):
    global report_progress_now, progress_lock
    if report_progress_now[id]:
        progress_lock.acquire()
        print("{} [{}] is making progress.".format(me.name, me.ident))
        time.sleep(1)
        progress_lock.release()

def exception_spam(id):
    me = current_thread()
    while True:
        try:
            raise Exception()
        except Exception:
            pass
        check_report_progress(me, id)

def sleep_forever(id):
    me = current_thread()
    while True:
        sleep(10)
    check_report_progress(me, id)

def busy_loop(id):
    me = current_thread()
    i = 0
    while True:
        i = (i % 100000000) + 1
        check_report_progress(me, id)
        # if i % 10000000 == 0: raise Exception()


if __name__ == '__main__':
    
    num_threads = 10
    thread_list = []
    thread_fun, main_fun =  exception_spam, busy_loop

    for i in range(num_threads):
        thread_list.append(Thread(target=thread_fun,args=(i,)))
        report_progress_now.append(True)
    for t in thread_list:
        t.start()
    report_progress_now.append(True)
    
    me, id = current_thread(), num_threads
    while True:
        try:
            main_fun(id)
        except KeyboardInterrupt:
            progress_lock.acquire()
            for i, _ in enumerate(report_progress_now):
                report_progress_now[i] = True
            progress_lock.release()

