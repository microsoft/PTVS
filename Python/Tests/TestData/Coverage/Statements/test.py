x = 1
x += 2
assert type(x) == int
del x
pass
exec('x = 1')
from sys import winver
global x
import sys
print(x)
try:
    x = 1
except:
    pass
    
try:
    x = 1
finally:
    pass
    
with open('fob.txt', 'w+'):
    x = 1
