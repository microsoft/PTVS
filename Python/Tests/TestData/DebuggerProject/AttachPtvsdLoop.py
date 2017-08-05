import sys
import ptvsd

if len(sys.argv) > 1:
    open(sys.argv[1], 'w').close()

i = 0
while True:
    if ptvsd.is_attached():
        ptvsd.break_into_debugger()
        if i is None: break
    i += 1

