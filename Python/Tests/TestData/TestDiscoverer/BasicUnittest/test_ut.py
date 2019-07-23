import unittest

class TestClassUT(unittest.TestCase):
    def test_ut_fail(self):
        self.fail("Not implemented")

    def test_ut_pass(self):
        pass

if __name__ == '__main__':
    unittest.main()
