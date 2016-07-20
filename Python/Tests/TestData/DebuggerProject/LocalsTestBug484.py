class weirdlist(list):
    def items(self):
        return []


x = weirdlist([2,3,4])
print(x)