class ClassA:
    def func1(self):
        x = ClassB()
    
    def func2(self):
        self.func1()

class ClassB:
    def func1(self):
        x = ClassA()
    
    def func2(self):
        pass

