import unittest

@unittest.skip("Skip over the entire test class")
class TestClassDecoratorsUT(unittest.TestCase):
    def test_ut_fail(self):
        self.fail("Not implemented")
   
    @unittest.skip("Skip over the entire test routine")
    def test_ut_pass(self):
        pass

if __name__ == '__main__':
    unittest.main()
