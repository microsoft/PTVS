def f(*a, x): pass
def f(*a, x = 1): pass

def f(a: 1): pass
def f(*a: 1): pass
def f(**a: 1): pass
def f(a: 0, *b: 1, **c: 2): pass 

def f() -> 1: pass

def f(a: 1) -> 1: pass

def f(a: 1 = 2): pass

def f(*, a): pass