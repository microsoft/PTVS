# coding: utf-8
d1 = {42: 100}
d2 = {'abc': 'fob'}
d3 = {1e1000: d1}

s = set([frozenset([2,3,4])])
class C(object):
    abc = 42

    def f(self): pass

cinst = C()

class C2(object):
    abc = 42
    def __init__(self):
        self.oar = 100
        self.self = self
    def __repr__(self):
        return 'myrepr'
    def __hex__(self):
        return 'myhex'
    def f(self): pass

c2inst = C2()

class C3(object):
    def __init__(self):
        self.abc = 42
        self._contents = [1,2]
    def __iter__(self):
        return iter(self._contents)
    def __len__(self):
        return len(self._contents)
    def __getitem__(self, index):
        return self._contents[index]

c3inst = C3()

l = [1, 2, ]

i = 3

pass