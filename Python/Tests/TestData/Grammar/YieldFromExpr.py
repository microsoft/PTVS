def f():
    yield from fob
    oar = yield from fob
    baz = [(yield from oar) for oar in fob]