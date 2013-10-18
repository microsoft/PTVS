import unittest

class LinkedTests(unittest.TestCase):
    def test_linked_pass(self):
        pass

    def test_linked_fail(self):
        self.assertTrue(False, "Force a failure in linked test.")

if __name__ == '__main__':
    import sys
    import os
    print(os.getcwd())
    print(sys.argv)
    print(sys.path)
    unittest.main()
