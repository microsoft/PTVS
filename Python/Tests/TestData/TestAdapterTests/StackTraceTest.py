import unittest

class StackTraceTests(unittest.TestCase):
    def test_bad_import(self):
        obj = Utility()
        obj.instance_method_a()

    def test_not_equal(self):
        self.assertEqual(1, 2)

def global_func():
    def local_func():
        import not_a_module # trigger exception
    local_func()

class Utility(object):
    @staticmethod
    def class_static():
        global_func()

    def instance_method_b(self):
        Utility.class_static()

    def instance_method_a(self):
        self.instance_method_b()

if __name__ == '__main__':
    unittest.main()

