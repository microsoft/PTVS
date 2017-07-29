import sys
if '.' not in sys.path: sys.path.insert(0, '.') # so that we can find ptvsd

import ptvsd
ptvsd.enable_attach('secret', redirect_output=False)
ptvsd.wait_for_attach()

import unittest

class Test_test1(unittest.TestCase):
    def test_A(self):
        self.fail("Not implemented")
unittest.main()
