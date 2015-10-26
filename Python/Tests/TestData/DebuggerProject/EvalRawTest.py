import sys

try:
    unicode
except:
    unicode = str

class DerivedString(unicode):
    def __new__(cls, *args, **kwargs):
        return unicode.__new__(cls, *args, **kwargs)

n = 123

if sys.version[0] == '3':
    s = 'fob'.encode('ascii')
    u = 'fob'
else:
    s = 'fob'
    u = unicode('fob')

ds = DerivedString(u)

try:
    ba = bytearray(u, 'ascii')
except:
    pass

print('breakpoint')
