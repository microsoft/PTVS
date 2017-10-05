from Values import x, y, z
l = []
t = ()
s = {1}

XY = x
XY = y
XYZ = z
XYZ = XY

D = l
D = t
D = s
D = s   # ensure multiple assignments do not clear it
from Values import D
