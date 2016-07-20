def outer(x):
    outer_val = x * 2
    inner(outer_val)
    pass

def inner(y):
    inner_val = y * 5
    innermost(inner_val)
    pass

def innermost(z):
    innermost_val = z + 1
    pass

global_val = 5
outer(5)
