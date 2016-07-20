def g():
    fob = 1
    oar = 1
    def f():
        nonlocal fob
        nonlocal fob, oar


def g():
    def f():
        nonlocal fob

    fob = 1


def f():
    class C:
        nonlocal fob
        fob = 1
    fob = 2

class X:
    def f(x):
        nonlocal __class__
