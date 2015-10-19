import os
code = ''.join(open(os.path.join(os.path.dirname(__file__), 'A/relpath.py')).readlines()).replace('\r\n', '\n')
comp_code = compile(code, 'A/relpath.py', 'exec')
x = {'__builtins__':__builtins__}
exec(comp_code, x)
x['do_something']()
# Do it one more time to test path mapping cache
x['do_something']()
