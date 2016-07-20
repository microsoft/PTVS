class class1(object):
    """description of class"""
    def outer(self, x):
      y = x
      def inner(z):
         return x+y+z
      return inner

class1().outer(1)(2)
