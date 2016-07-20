from threading import Thread
import time

class ThreadFirst(Thread):
    def run(self):
        for i in range(5):
            time.sleep(1)
            print('hi')


ThreadFirst().start()
