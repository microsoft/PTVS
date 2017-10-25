def r_a(a, b):
    return a

def r_b(a, b):
    return b

def r_str():
    return ''

def r_object():
    return object()

class A:
    def r_A(self):
        return type(self)()
