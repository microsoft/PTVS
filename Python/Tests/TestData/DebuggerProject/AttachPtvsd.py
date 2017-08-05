import sys
if '.' not in sys.path: sys.path.insert(0, '.') # so that we can find ptvsd

if 'ptvsd' not in sys.modules:
    import ptvsd
    eval('ptvsd.enable_attach(' + sys.argv[1] + ')')
    ptvsd.wait_for_attach()

sys.stdout.write('stdout')
sys.stderr.write('stderr')
x = 1
