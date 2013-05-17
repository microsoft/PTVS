def g():
    foo = 1
    bar = 1
    def f():
        nonlocal foo
        nonlocal foo, bar


def g():
    def f():
        nonlocal foo

    foo = 1


def f():
    class C:
        nonlocal foo
        foo = 1
    foo = 2
