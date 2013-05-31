import unittest
import InheritanceBaseTest

class DerivedClassTests(InheritanceBaseTest.BaseClassTests):
    def test_derived_pass(self):
        pass

    def test_derived_fail(self):
        self.assertTrue(False, "Force a failure in derived class test.")

if __name__ == '__main__':
    unittest.main()
