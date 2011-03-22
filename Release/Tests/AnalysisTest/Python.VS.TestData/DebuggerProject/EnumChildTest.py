d1 = {42 : 100}
d2 = {'abc' : 'foo'}
u1 = u"привет мир"

class C(object):
    abc = 42
    uc = u"привет мир"
    def f(self): pass

cinst = C()

class C2(object):
    abc = 42
    def __init__(self):
        self.bar = 100
        self.self = self
    def __repr__(self):
        return 'myrepr'
    def __hex__(self):
        return 'myhex'
    def f(self): pass

c2inst = C2()

l = [1, 2, ]

i = 3

pass