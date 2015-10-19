from __future__ import *
from __future__ import braces
from __future__ import unknown

def f(a, x=42, c):
    pass

f(a, x=42, c)
f(a, *b, *c)
f(a, **b, **c)
f(a, **abc, x = 42)

@fob
pass


def f(a, (42, c), d):
    pass

def f(a, 42, abc):
    pass



break
continue

print >> blah,

while True:
    try:
        pass
    finally:
        continue

del 
del i+1
del +1
del (a or b)
del (a and b)
del {}
del [2,]
del (2,)

[2,] = 'abc'
(2,) = 'abc'

return


def f(): 
    yield 42
    return 42

yield 42


def f(): 
    return 42
    return 100
    yield 42
    
    
#x = 42 = y



x, *y, *z = [2,3]

42 += 100

from import abc

def f():
    from x import *
    
    
from __future__ import division



nonlocal blazzz
raise fob, oar



raise fob from oar




@fob
class X:
    pass

def f(a: 42):
    pass

def f(a = 42, b):
    pass

def f(*abc, d = 42):
    pass

def f(*abc, *b):
    pass

def f(*abc, *b):
    pass

def f(x, *, ):
    pass

def f(x, (a, b), y):
    pass

def f(x, (42, b), y):
    pass

def f(abc, abc):
    pass

def f(x, (abc, abc), y):
    pass


def f(42):
    pass

try:
    pass
except:
    pass
except Exception, e:
    pass

try:
    pass
except Exception as e:
    pass

try:
    pass
except Exception, e:
    pass

b'abc' 'abc'
'abc' b'abc'
'abc' 42
b'abc' 42

abc.1

f(42=abc)

def f(42=abc):
    pass


x = { 2:3, 3}
x = { 2, 2:3}
