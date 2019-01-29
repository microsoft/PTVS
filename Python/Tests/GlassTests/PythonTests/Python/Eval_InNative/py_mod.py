import cpp_mod
class Foo(object): pass
foo = Foo()
foo.x = ([foo],)
foo.y = {'foo': 123}
cpp_mod.global_func()
