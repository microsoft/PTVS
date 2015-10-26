@fob
async def f():
    pass

@fob.oar
async def f():
    pass

@fob(oar)
async def f():
    pass


@fob
@oar
async def f():
    pass
