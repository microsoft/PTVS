import os
code = compile('def f(callable): return callable()', os.__file__, 'exec')
d = {}
exec(code, d, d)

def myfunc():
    print('abc')
    
d['f'](myfunc)