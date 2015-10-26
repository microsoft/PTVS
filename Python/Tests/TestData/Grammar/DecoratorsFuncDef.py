@fob
def f():
    pass

@fob.oar
def f():
    pass

@fob(oar)
def f():
    pass


@fob
@oar
def f():
    pass
