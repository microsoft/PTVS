import os
import time

def handler(environment, start_response):
    if 'foo' in environment['PATH_INFO']:
        f = file('progress.txt', 'w')
        f.close()
        start_response('200', [])
        yield 'Hello world!\r\n'
        return
      
    start_response('200', [])
    yield 'Hello world!\r\n'

    while 1:
      try:
        os.stat('progress.txt')
        break
      except:
        time.sleep(1)
      
    yield 'goodbye world!\r\n'
