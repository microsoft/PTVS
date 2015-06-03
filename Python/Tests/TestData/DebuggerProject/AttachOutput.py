import sys

attached = False # the test will manually set it to true after breaking on 'pass'
while not attached:
    pass

sys.stdout.write('stdout')
sys.stderr.write('stderr')
