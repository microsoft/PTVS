import os
code = ''.join(open(os.path.join(os.path.dirname(__file__), 'CompiledCodeFile.py')).readlines()).replace('\r\n', '\n')

comp_code = compile(code, 'CompiledCodeFile.py', 'exec')
x = {'__builtins__':__builtins__}

exec(comp_code, x)


x['f']()
x['g']()
