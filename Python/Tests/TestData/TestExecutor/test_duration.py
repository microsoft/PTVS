import unittest
import time

class DurationTests(unittest.TestCase):
    def test_sleep_0_1(self):
        time.sleep(0.1)

    def test_sleep_0_3(self):
        time.sleep(0.3)

    def test_sleep_0_5(self):
        time.sleep(0.5)
        self.assertTrue(False)

if __name__ == '__main__':
    unittest.main()
