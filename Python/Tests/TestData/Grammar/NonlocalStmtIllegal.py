def g():
    def f():
        nonlocal a


def f():
    x = 42
    def g():
            global x
            nonlocal x

def f():
    x = 42
    def g(x):
            nonlocal x

nonlocal fob

globalvar = 42
def g():
    nonlocal globalvar


class C:
	x = 42
	def f():
		nonlocal x


def h():
    x = 42
    def f():
        global x
        def g():
            nonlocal x