import unittest

from package1 import partial_coverage, full_coverage, no_coverage

class TestCoverage(unittest.TestCase):
    def test_one(self):
        partial_coverage(1)
        partial_coverage(-1)

    def test_two(self):
        full_coverage()

def test_global():
    partial_coverage(1)

if __name__ == '__main__':
    unittest.main()
