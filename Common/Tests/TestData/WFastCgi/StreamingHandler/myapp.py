import os
import time

def handler(environment, start_response):
    start_response('200', [])
    yield 'Hello world!\r\n'

    while 1:
      try:
        os.stat('progress.txt')
        break
      except:
        time.sleep(1)
      
    yield 'goodbye world!\r\n'
