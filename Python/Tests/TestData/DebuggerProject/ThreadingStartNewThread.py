from threading import *

class MyThread(Thread):
    def run(self):
        print('hi')

x = True
while x:
    pass

MyThread().start()