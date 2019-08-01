import unittest

class BaseClassTests(unittest.TestCase):
    def test_base_pass(self):
        pass

    def test_base_fail(self):
        self.assertTrue(False, "Force a failure in base class code.")

if __name__ == '__main__':
    unittest.main()
