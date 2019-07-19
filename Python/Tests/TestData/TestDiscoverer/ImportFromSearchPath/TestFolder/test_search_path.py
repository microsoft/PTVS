import unittest
import searchpathmodule

class SearchPathTests(unittest.TestCase):
    def test_imported_module(self):
        assert searchpathmodule.CONSTANT == 1
