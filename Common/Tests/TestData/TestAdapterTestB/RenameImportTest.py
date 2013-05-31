from unittest import TestCase as MyTest
import unittest

class RenamedImportTests(MyTest):
    def test_renamed_import_pass(self):
        pass

    def test_renamed_import_fail(self):
        self.assertTrue(False, "Force a failure in renamed import test.")

if __name__ == '__main__':
    unittest.main()
