import sys

while type(sys.stdout).__name__ != '_DebuggerOutput': pass

sys.stdout.write('stdout')
sys.stderr.write('stderr')
