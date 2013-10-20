import unittest

class TestsInPackage(unittest.TestCase):
    def test_in_package_pass(self):
        pass

    def test_in_package_fail(self):
        self.assertTrue(False, "Force a failure in test.")

if __name__ == '__main__':
    unittest.main()
