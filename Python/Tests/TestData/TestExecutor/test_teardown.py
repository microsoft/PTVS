import unittest

class TeardownTests(unittest.TestCase):
    def setUp(self):
        print('doing setUp')

    def tearDown(self):
        print('doing tearDown')

    def test_success(self):
        pass

    def test_failure(self):
        self.fail("failure")

if __name__ == '__main__':
    unittest.main()
