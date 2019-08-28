import numpy as np
import unittest

class Test_activate(unittest.TestCase):
    def test_A(self):
        a = np.ones((1,2))
        self.assertTrue( a.any() )

if __name__ == '__main__':
    unittest.main()