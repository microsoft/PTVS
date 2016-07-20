import os
import time

def handler(environment, start_response):
    if 'fob' in environment['PATH_INFO']:
        f = open('progress.txt', 'w')
        f.close()
        start_response('200', [])
        yield b'Hello world!\r\n'
        return
      
    start_response('200', [])
    yield b'Hello world!\r\n'

    while 1:
      try:
        os.stat('progress.txt')
        break
      except:
        time.sleep(1)
      
    yield b'goodbye world!\r\n'
