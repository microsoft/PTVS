class C(metaclass=1): pass
class C(object, metaclass=1): pass
class C(list, object, foo=1): pass
