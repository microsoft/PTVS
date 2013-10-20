import unittest
import TestAdapterLibraryBar

class BarTests(unittest.TestCase):
    def test_calculate_pass(self):
        obj = TestAdapterLibraryBar.Bar()
        result = obj.calculate_total(2, 3, 4)
        self.assertEqual(9, result)
        self.write_output("\nhappy\n")

    def test_calculate_fail(self):
        obj = TestAdapterLibraryBar.Bar()
        result = obj.calculate_total(2, 3, 4)
        self.assertEqual(10, result)

    def write_output(self, text):
        print(text)

if __name__ == '__main__':
    import sys
    import os
    print(os.getcwd())
    print(sys.argv)
    print(sys.path)
    unittest.main()
