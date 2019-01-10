class C(object):
    def method(self):
        pass

def global_func(c):
    c.method()

c = C()
global_func(c)
 