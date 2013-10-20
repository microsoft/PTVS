def foo():
  global x
  x = 1
  pass

foo()

import LocalGlobalsTestImported

print('done')
