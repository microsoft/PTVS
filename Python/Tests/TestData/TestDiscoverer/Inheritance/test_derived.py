import unittest
import test_base

class DerivedClassTests(test_base.BaseClassTests):
    def test_derived_pass(self):
        pass

    def test_derived_fail(self):
        self.assertTrue(False, "Force a failure in derived class code.")

if __name__ == '__main__':
    unittest.main()
