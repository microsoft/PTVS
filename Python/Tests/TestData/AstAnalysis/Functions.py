def f():
    '''f'''
    pass
    def f1(): pass

f2 = f

if True:
    def g(): pass
else:
    def h(): pass

class C:
    def i(self): pass
    
    def j(self):
        def j2(self):
            pass

    class C2:
        def k(self): pass
