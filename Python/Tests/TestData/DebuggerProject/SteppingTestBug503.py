def x1(y):
    if y < 10:
        z = x1(y+1)
        z += 1
        return z + 3
    return y

def x2(y):
    if y < 10:
        z = x2(y+1)
        return z + 3
    return y

x1(5)
x2(5)