class Foo(dict):
    def __getitem__(self, index):
        return True
foo = Foo()
foo['bar'] = False
print()
