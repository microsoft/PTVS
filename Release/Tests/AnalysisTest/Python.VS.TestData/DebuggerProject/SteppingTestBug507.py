def add_two_numbers(x, y):
    return x + y

class Z(object):
    @property
    def foo(self):
        return 7

p = Z()
print add_two_numbers(p.foo, 3)

print "Done"