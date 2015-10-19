def f():
    """hello world"""

if f.__doc__ is not None:
    raise Exception('has doc string')