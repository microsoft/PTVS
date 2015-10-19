def outer(x):
  y = x
  def inner(z):
     return x+y+z
  return inner

outer(1)(2)

import LocalClosureVarsTestImported

print('done')
