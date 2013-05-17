def f():
    yield

def f():
    foo = yield

def f():
    baz = [(yield bar) for bar in foo]
