import unittest
from .helper import initialize

class RelativeImportTests(unittest.TestCase):
    def test_relative_import(self):
        initialize();

if __name__ == '__main__':
    unittest.main()
