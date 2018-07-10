def test1():
    return (fob async for fob in [])

def test2():
    return (fob for fob in [] if await fob)
