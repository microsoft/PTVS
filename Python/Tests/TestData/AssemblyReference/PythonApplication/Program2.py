print('Hello World')
import clr
clr.AddReference('ClassLibrary')
clr.AddReference('ClassLibrary2')

from ClassLibrary1 import Class1
a = Class1().X

from ClassLibrary2 import Class2
b = Class2().X

Class1.Fob()







