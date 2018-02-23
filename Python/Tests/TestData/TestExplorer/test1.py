import unittest

class Test_test1(unittest.TestCase):
    def test_failure(self):
        self.helper()

    def test_success(self):
        print('Hello World')

    def helper(self):
        self.fail('Not implemented')

if __name__ == '__main__':
    unittest.main()
