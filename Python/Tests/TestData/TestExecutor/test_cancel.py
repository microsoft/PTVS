import unittest
import time

class CancelTests(unittest.TestCase):
    def test_sleep_1(self):
        time.sleep(0.1)

    def test_sleep_2(self):
        time.sleep(5)

    def test_sleep_3(self):
        time.sleep(5)

    def test_sleep_4(self):
        time.sleep(0.1)

if __name__ == '__main__':
    unittest.main()
