import os, sys
with open('file3.txt', 'w') as f:
    f.write(os.curdir + '\n')
    for p in sys.path:
        f.write(p + '\n')
