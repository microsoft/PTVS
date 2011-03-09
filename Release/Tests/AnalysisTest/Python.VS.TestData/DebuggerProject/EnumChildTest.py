d1 = {42 : 100}
d2 = {'abc' : 'foo'}


class C(object):
	abc = 42
	def f(self): pass

cinst = C()

class C2(object):
	abc = 42
	def __init__(self):
		self.bar = 100
	def f(self): pass

c2inst = C2()

l = [1, 2, ]

i = 3

pass