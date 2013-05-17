print('hello world')

class C(object):
	abc = 42
	def __init__(self):
		self.bar = 100


def f(a, b, c):
	i = 42
	l = [2,3,4]
	d = {'abc':'foo'}
	o = C()
	print('in f')

f(2, [3,4,5], {'x':42})
