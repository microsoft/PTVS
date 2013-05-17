@foo
def f():
    pass

@foo.bar
def f():
    pass

@foo(bar)
def f():
    pass


@foo
@bar
def f():
    pass
