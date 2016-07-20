def add_two_numbers(x, y):
    return x + y

class Z(object):
    @property
    def fob(self):
        return 7

p = Z()
print(add_two_numbers(p.fob, 3))

print("Done")