import os
code = ''.join(file(os.path.join(os.path.dirname(__file__), 'CompiledCodeFile.py')).readlines())

comp_code = compile(code, 'CompiledCodeFile.py', 'exec')
x = {'__builtins__':__builtins__}

exec comp_code in x


x['f']()
x['g']()
	
	 