class OuterClass(object):
    def outer_method(self):
        def nested_function():
            class InnerClass(object):
                class InnermostClass(object):
                    def innermost_method(self):
                        print()
            InnerClass.InnermostClass().innermost_method()
        nested_function()

OuterClass().outer_method()
