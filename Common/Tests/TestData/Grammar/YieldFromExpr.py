def f():
    yield from foo
    bar = yield from foo
    baz = [(yield from bar) for bar in foo]