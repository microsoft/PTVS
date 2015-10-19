class ClassA:
    def func1(self):
        x = ClassC()
    
    def func2(self):
        self.func1()

class ClassC:
    def func1(self):
        x = ClassA()
    
    def func2(self):
        pass

