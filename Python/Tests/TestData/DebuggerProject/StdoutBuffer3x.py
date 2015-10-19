import sys, codecs
x = codecs.getwriter('cp437')(sys.stdout.buffer, errors='ignore')
x.write('fob')
