class C1: pass
class C2(object): pass
class C3(C2): pass
class C4(C1, C2, C3): pass

if True:
    class D: pass

if False:
    class E: pass

class F1:
    class F2: pass
    class F3:
        if True:
            class F4: pass
        else:
            class F5: pass

def f():
    class X: pass
